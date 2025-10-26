using EPS.Discount.Core.Interfaces;
using EPS.Discount.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;

namespace EPS.Discount.Data;

public class SqlServerBulkInserter : IBulkInserter
{
    private readonly string _connectionString;
    private readonly ILogger<SqlServerBulkInserter> _logger;

    public SqlServerBulkInserter(IConfiguration config, ILogger<SqlServerBulkInserter> logger)
    {
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (connectionString is null)
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task InsertAsync(IEnumerable<BulkInsertCode> entities)
    {
        // Convert to DataTable for SqlBulkCopy
        var table = new DataTable();
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Length", typeof(int));
        table.Columns.Add("CreatedAt", typeof(DateTime));

        foreach (var e in entities)
        {
            var row = table.NewRow();
            row["Code"] = e.Code;
            row["Length"] = e.Length;
            row["CreatedAt"] = e.CreatedAt;
            table.Rows.Add(row);
        }

        using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();

        using var bulk = new SqlBulkCopy(conn)
        {
            DestinationTableName = "dbo.DiscountCodes",
            BatchSize = 1000,
            BulkCopyTimeout = 60
        };

        // Map columns - ensure these match your DB names
        bulk.ColumnMappings.Add("Code", "Code");
        bulk.ColumnMappings.Add("Length", "Length");
        bulk.ColumnMappings.Add("CreatedAt", "CreatedAt");

        try
        {
            await bulk.WriteToServerAsync(table);
        }
        catch (SqlException ex)
        {
            // SqlBulkCopy will throw on duplicate PK/unique key depending on table; wrap as DbUpdateException style
            _logger.LogDebug(ex, "SqlBulkCopy error - likely uniqueness collision with concurrent inserts.");
            throw;
        }
    }
}
