using EPS.Discount.Core.Interfaces;
using EPS.Discount.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace EPS.Discount.Data;

public class PostgresBulkInserter : IBulkInserter
{
    private readonly string _connectionString;
    private readonly ILogger<PostgresBulkInserter> _logger;

    public PostgresBulkInserter(IConfiguration config, ILogger<PostgresBulkInserter> logger)
    {
        var connStr = config.GetConnectionString("DefaultConnection");
        if (connStr is null)
            throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _connectionString = connStr;
        _logger = logger;
    }

    public async Task InsertAsync(IEnumerable<BulkInsertCode> entities)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Using COPY ... (binary) for speed.
        // This writes directly into the table. If you need upsert semantics or need to avoid duplicates,
        // consider COPY into a temp table then INSERT ... ON CONFLICT DO NOTHING.
        var copyCommand = "COPY public.\"DiscountCodes\" (\"Code\",\"Length\",\"CreatedAt\") FROM STDIN (FORMAT BINARY)";

        try
        {
            await using var writer = conn.BeginBinaryImport(copyCommand);
            foreach (var e in entities)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(e.Code, NpgsqlTypes.NpgsqlDbType.Text);
                await writer.WriteAsync(e.Length, NpgsqlTypes.NpgsqlDbType.Integer);
                await writer.WriteAsync(e.CreatedAt, NpgsqlTypes.NpgsqlDbType.TimestampTz);
            }
            await writer.CompleteAsync();
        }
        catch (PostgresException ex)
        {
            _logger.LogDebug(ex, "Postgres COPY error - likely uniqueness collision.");
            throw;
        }
    }
}
