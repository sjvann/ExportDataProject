using ExportData.Models.Config;
using ExportData.Models.EnumType;
using ExportData.Services;
using Xunit;

namespace ExportData.Tests;

public sealed class ConnectionSecretMemoryTests
{
    [Fact(DisplayName = "測試連線後密碼欄被清空時，同一主機與帳號沿用已記住的密碼")]
    public void EmptyPost_ReusesPassword_WhenLoginMatches()
    {
        var form = Sample();
        var identity = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);

        var decision = ConnectionSecretMemory.Resolve(null, "kept", identity, "secret", identity, false);

        Assert.Equal("secret", decision.Password);
        Assert.False(decision.Store);
        Assert.False(decision.Forget);
    }

    [Fact(DisplayName = "換資料庫仍沿用同一組登入密碼")]
    public void DifferentDatabase_KeepsTheSameLoginIdentity()
    {
        var form = Sample();
        var before = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);
        form.Database = "other";

        Assert.Equal(before, ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form));
    }

    [Fact(DisplayName = "帳號不同就不沿用舊密碼")]
    public void DifferentUser_DoesNotReusePassword()
    {
        var form = Sample();
        var stored = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);
        form.Username = "other";
        var current = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);

        var decision = ConnectionSecretMemory.Resolve(null, "kept", stored, "secret", current, false);

        Assert.Null(decision.Password);
        Assert.False(decision.Forget);
    }

    [Fact(DisplayName = "重新輸入的密碼會取代已記住的密碼")]
    public void PostedPassword_ReplacesStoredPassword()
    {
        var form = Sample();
        var identity = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);

        var decision = ConnectionSecretMemory.Resolve("new-secret", "kept", identity, "secret", identity, false);

        Assert.Equal("new-secret", decision.Password);
        Assert.True(decision.Store);
        Assert.False(decision.Forget);
    }

    [Fact(DisplayName = "使用者清掉密碼後不再沿用")]
    public void ClearedPassword_ForgetsStoredPassword()
    {
        var form = Sample();
        var identity = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);

        var decision = ConnectionSecretMemory.Resolve("", "cleared", identity, "secret", identity, false);

        Assert.Null(decision.Password);
        Assert.True(decision.Forget);
    }

    [Fact(DisplayName = "連線字串模式不把表單密碼寫進工作階段")]
    public void RawMode_DoesNotStoreOrReuseFormPassword()
    {
        var form = Sample();
        form.UseRawConnectionString = true;
        var identity = ConnectionSecretMemory.Identity(EnumDbType.PostgreSql, form);

        var decision = ConnectionSecretMemory.Resolve(null, "kept", identity, "secret", identity, true);

        Assert.Null(decision.Password);
        Assert.False(decision.Store);
        Assert.False(decision.Forget);
    }

    private static DbConnectionForm Sample() => new()
    {
        Host = "localhost",
        Port = 5432,
        Database = "postgres",
        Username = "postgres",
        Password = "secret"
    };
}
