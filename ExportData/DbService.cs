using Dapper;
using ExportData.Interfaces;
using ExportData.Models.Config;
using ExportData.Models.Database;
using ExportData.SqlGen;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Data.SqlClient;

namespace ExportData
{
    public class DbService : IDbService
    {
        private readonly ISqlGenerater? _sqlProvider;
        private readonly string? _sqlGetAllTableNameList;
        private readonly string? _sqlGetRecords;
        private readonly ILogger<DbService> _logger;

        public DbService(ConfigDbControlSection dbControlSection, ILogger<DbService> logger)
        {
            _sqlProvider = DbChoicer.ChoiceProverder(dbControlSection);
            _sqlGetAllTableNameList = dbControlSection.SqlAllTable;
            _sqlGetRecords = dbControlSection.SqlOneTable;
            _logger = logger;
        }

        public async Task<IEnumerable<Dictionary<string, object>>?> GetDataSetAsync(string tableName)
        {
            try
            {
                string? sql = string.IsNullOrEmpty(_sqlGetRecords) ? _sqlProvider?.GetSqlRecords(tableName) : _sqlGetRecords;
                _logger.LogInformation("Executing SQL: {Sql}", sql);

                if (_sqlProvider == null || string.IsNullOrEmpty(sql))
                {
                    _logger.LogWarning("SQL provider or SQL query is null for table: {TableName}", tableName);
                    return null;
                }

                using var connection = _sqlProvider.GetConnection();
                var result = await connection.QueryAsync<Dictionary<string, object>>(sql, commandTimeout: 300);
                _logger.LogInformation("Retrieved {Count} records from table: {TableName}", result.Count(), tableName);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving data from table: {TableName}", tableName);
                return null;
            }
        }

        public async Task<IEnumerable<Dictionary<string, object>>> GetAllDataAsync(string tableName)
        {
            if (_sqlProvider == null)
            {
                throw new InvalidOperationException("無法建立資料庫查詢");
            }

            var sql = _sqlProvider.GetSqlAllRecords(tableName);
            if (string.IsNullOrEmpty(sql))
            {
                throw new InvalidOperationException("無法組成資料查詢");
            }

            _logger.LogInformation("Executing SQL: {Sql}", sql);
            using var connection = _sqlProvider.GetConnection();
            var result = await connection.QueryAsync<Dictionary<string, object>>(sql, commandTimeout: 300);
            _logger.LogInformation("Retrieved {Count} records from table: {TableName}", result.Count(), tableName);
            return result;
        }

        public async Task<string[]?> GetTableNamesAsync()
        {
            try
            {
                string? sql = string.IsNullOrEmpty(_sqlGetAllTableNameList) ? _sqlProvider?.GetSqlAllTableNameList() : _sqlGetAllTableNameList;
                _logger.LogInformation("Executing SQL for table names: {Sql}", sql);

                if (_sqlProvider == null || string.IsNullOrEmpty(sql))
                {
                    _logger.LogWarning("SQL provider or SQL query is null for getting table names");
                    return null;
                }

                using var connection = _sqlProvider.GetConnection();
                var result = await connection.QueryAsync<string>(sql);
                var tableNames = result.ToArray();
                _logger.LogInformation("Found {Count} tables", tableNames.Length);
                return tableNames;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table names");
                return null;
            }
        }

        public async Task<TableSchema?> GetTableSchemaAsync(string tableName)
        {
            try
            {
                if (_sqlProvider == null) return null;

                using var connection = _sqlProvider.GetConnection();
                var schema = await _sqlProvider.GetTableSchemaAsync(connection, tableName);
                _logger.LogInformation("Retrieved schema for table: {TableName}", tableName);
                return schema;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving schema for table: {TableName}", tableName);
                return null;
            }
        }

        public async Task<IEnumerable<TableRelation>?> GetTableRelationsAsync()
        {
            try
            {
                if (_sqlProvider == null) return null;

                using var connection = _sqlProvider.GetConnection();
                var relations = await _sqlProvider.GetTableRelationsAsync(connection);
                _logger.LogInformation("Retrieved table relations");
                return relations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving table relations");
                return null;
            }
        }

        public async Task<DatabaseInfo?> GetDatabaseInfoAsync()
        {
            try
            {
                if (_sqlProvider == null) return null;

                using var connection = _sqlProvider.GetConnection();
                var dbInfo = await _sqlProvider.GetDatabaseInfoAsync(connection);
                _logger.LogInformation("Retrieved database info: {DatabaseName}", dbInfo?.DatabaseName);
                return dbInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving database info");
                return null;
            }
        }
    }
}