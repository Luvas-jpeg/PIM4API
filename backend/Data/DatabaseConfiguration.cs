using Microsoft.Extensions.Configuration;
using Npgsql;

namespace EquipamentosMedicosApi.Data;

public static class DatabaseConfiguration
{
    public static string GetConnectionString(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;

        var host = configuration["Postgres:Host"];
        var database = configuration["Postgres:Database"];
        var username = configuration["Postgres:Username"];
        var password = configuration["Postgres:Password"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(database) ||
            string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Configure ConnectionStrings:DefaultConnection ou Postgres:Host, Database, Username e Password.");

        var port = configuration.GetValue<int?>("Postgres:Port") ?? 5432;
        if (port is < 1 or > 65535)
            throw new InvalidOperationException("Postgres:Port deve estar entre 1 e 65535.");

        var sslValue = configuration["Postgres:SslMode"] ?? "Prefer";
        if (!Enum.TryParse<SslMode>(sslValue, true, out var sslMode) || !Enum.IsDefined(sslMode))
            throw new InvalidOperationException("Postgres:SslMode invalido.");

        return new NpgsqlConnectionStringBuilder
        {
            Host = host, Port = port, Database = database,
            Username = username, Password = password, SslMode = sslMode
        }.ConnectionString;
    }
}