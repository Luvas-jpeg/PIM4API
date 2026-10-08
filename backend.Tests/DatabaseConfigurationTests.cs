using EquipamentosMedicosApi.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace backend.Tests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void ExistingConnectionStringTakesPrecedence()
    {
        var config = Build(new() { ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=local" });
        Assert.Equal("Host=localhost;Database=local", DatabaseConfiguration.GetConnectionString(config));
    }

    [Fact]
    public void RenderPropertiesPreserveSpecialCharactersInPassword()
    {
        var result = new NpgsqlConnectionStringBuilder(DatabaseConfiguration.GetConnectionString(RenderConfig()));
        Assert.Equal("db.internal", result.Host);
        Assert.Equal("demo", result.Database);
        Assert.Equal("pim4", result.Username);
        Assert.Equal("secret;= \"with symbols\"", result.Password);
        Assert.Equal(5432, result.Port);
        Assert.Equal(SslMode.Require, result.SslMode);
    }

    [Fact]
    public void MissingDatabaseConfigurationFailsWithoutExposingSecrets()
    {
        var config = Build(new() { ["Postgres:Password"] = "private-secret" });
        var error = Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.GetConnectionString(config));
        Assert.DoesNotContain("private-secret", error.Message);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    public void InvalidPortIsRejected(string port)
    {
        var config = RenderConfig(new() { ["Postgres:Port"] = port });
        Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.GetConnectionString(config));
    }

    [Fact]
    public void InvalidSslModeIsRejected()
    {
        var config = RenderConfig(new() { ["Postgres:SslMode"] = "unexpected" });
        Assert.Throws<InvalidOperationException>(() => DatabaseConfiguration.GetConnectionString(config));
    }

    private static IConfiguration RenderConfig(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Postgres:Host"] = "db.internal",
            ["Postgres:Database"] = "demo",
            ["Postgres:Username"] = "pim4",
            ["Postgres:Password"] = "secret;= \"with symbols\"",
            ["Postgres:SslMode"] = "Require"
        };
        if (overrides != null)
            foreach (var item in overrides) values[item.Key] = item.Value;
        return Build(values);
    }

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}