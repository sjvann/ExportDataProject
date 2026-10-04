using System.Data.SQLite;
using System.Globalization;
using Dapper;
using Xunit;

namespace ExportData.Tests;

public sealed class DataRowReaderTests
{
    [Fact(DisplayName = "資料列保留欄位值，空值記成 DBNull")]
    public async Task ReadAsync_CopiesValuesAndNulls()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        connection.Execute("""
            CREATE TABLE Users (
                UserID INTEGER PRIMARY KEY,
                Username TEXT NOT NULL,
                Notes TEXT
            );
            INSERT INTO Users (UserID, Username, Notes) VALUES (7, 'ada', NULL);
            """);

        var rows = await DataRowReader.ReadAsync(connection, "SELECT * FROM Users", 30);

        var row = Assert.Single(rows);
        Assert.Equal(7, Convert.ToInt32(row["UserID"], CultureInfo.InvariantCulture));
        Assert.Equal("ada", row["username"]);
        Assert.Equal(DBNull.Value, row["Notes"]);
    }

    [Fact(DisplayName = "直接映射成 Dictionary 會丟掉欄位值")]
    public async Task QueryDictionary_LeavesCellsEmpty()
    {
        using var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        connection.Execute("""
            CREATE TABLE Users (
                UserID INTEGER PRIMARY KEY,
                Username TEXT NOT NULL
            );
            INSERT INTO Users (UserID, Username) VALUES (7, 'ada');
            """);

        var rows = (await connection.QueryAsync<Dictionary<string, object>>("SELECT * FROM Users")).ToList();

        var row = Assert.Single(rows);
        Assert.Empty(row);
    }
}
