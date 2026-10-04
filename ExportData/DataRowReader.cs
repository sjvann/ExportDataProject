using Dapper;
using System.Data;

namespace ExportData
{
    internal static class DataRowReader
    {
        public static async Task<List<Dictionary<string, object>>> ReadAsync(
            IDbConnection connection,
            string sql,
            int commandTimeout)
        {
            var rows = await connection.QueryAsync(sql, commandTimeout: commandTimeout).ConfigureAwait(false);
            var list = new List<Dictionary<string, object>>();
            foreach (var row in rows)
            {
                if (row is not IDictionary<string, object> source)
                {
                    throw new InvalidOperationException("查詢結果無法轉成資料列");
                }

                list.Add(Copy(source));
            }

            return list;
        }

        internal static Dictionary<string, object> Copy(IDictionary<string, object> source)
        {
            var copy = new Dictionary<string, object>(source.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var pair in source)
            {
                object? value = pair.Value;
                copy[pair.Key] = value ?? DBNull.Value;
            }

            return copy;
        }
    }
}
