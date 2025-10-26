using System.Text.Json;
using EPS.Discount.Application.Services;
using EPS.Discount.Core.Interfaces;
using EPS.Discount.Core.Models;
using EPS.Discount.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace EPS.Discount.Tests;

public class DiscountServiceTests
{
    private static IConfiguration CreateConfig(int cacheMinutes = 10) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:CacheExpirationMinutes"] = cacheMinutes.ToString()
            })
            .Build();

    private static DiscountService CreateService(
        IDbContextFactory<DiscountDbContext> dbFactory,
        ICodeGenerator? generator = null,
        IBulkInserter? bulkInserter = null,
        ICacheManager? cache = null,
        IConfiguration? config = null,
        ILogger<DiscountService>? logger = null)
    {
        generator ??= Mock.Of<ICodeGenerator>();
        bulkInserter ??= Mock.Of<IBulkInserter>();
        cache ??= Mock.Of<ICacheManager>();
        config ??= CreateConfig();
        logger ??= NullLogger<DiscountService>.Instance;

        return new DiscountService(dbFactory, generator, logger, bulkInserter, cache, config);
    }

    [Fact]
    public async Task GenerateCodes_InvalidCount_Zero_ReturnsError()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var svc = CreateService(dbFactory);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 0, Length = 7 });

        Assert.False(response.Result);
        Assert.Equal("Count must be between 1 and 2000", response.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCodes_InvalidCount_TooHigh_ReturnsError()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var svc = CreateService(dbFactory);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 5000, Length = 8 });

        Assert.False(response.Result);
        Assert.Equal("Count must be between 1 and 2000", response.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCodes_InvalidLength_ReturnsError()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var svc = CreateService(dbFactory);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 10, Length = 6 });

        Assert.False(response.Result);
        Assert.Equal("Length must be 7 or 8", response.ErrorMessage);
    }

    [Fact]
    public async Task GenerateCodes_Succeeds_InsertsAll_InBatches()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var gen = new Mock<ICodeGenerator>();

        var call = 0;
        gen.Setup(g => g.GenerateBatch(It.IsAny<int>(), It.IsAny<int>()))
           .Returns((int count, int _) =>
           {
               call++;
               var prefix = call == 1 ? "A" : "B";
               return Enumerable.Range(1, count).Select(i => $"{prefix}{i:D5}").ToList();
           });

        var insertedBatches = new List<List<BulkInsertCode>>();
        var bulk = new Mock<IBulkInserter>();
        bulk.Setup(b => b.InsertAsync(It.IsAny<IEnumerable<BulkInsertCode>>()))
            .Callback<IEnumerable<BulkInsertCode>>(e => insertedBatches.Add(e.ToList()))
            .Returns(Task.CompletedTask);

        var svc = CreateService(dbFactory, gen.Object, bulk.Object);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 750, Length = 8 });

        Assert.True(response.Result);
        Assert.Null(response.ErrorMessage);
        Assert.Equal(2, insertedBatches.Count);
        Assert.Equal(500, insertedBatches[0].Count);
        Assert.Equal(250, insertedBatches[1].Count);
    }

    [Fact]
    public async Task GenerateCodes_RetryOnDbUpdateException_ThenSuccess()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var gen = new Mock<ICodeGenerator>();
        var seq = 0;
        gen.Setup(g => g.GenerateBatch(It.IsAny<int>(), It.IsAny<int>()))
           .Returns((int count, int _) =>
           {
               seq++;
               var prefix = seq == 1 ? "X" : "Y";
               return Enumerable.Range(1, count).Select(i => $"{prefix}{i:D5}").ToList();
           });

        var bulk = new Mock<IBulkInserter>();
        bulk.SetupSequence(b => b.InsertAsync(It.IsAny<IEnumerable<BulkInsertCode>>()))
            .ThrowsAsync(new DbUpdateException("collision", (Exception?)null))
            .Returns(Task.CompletedTask);

        var svc = CreateService(dbFactory, gen.Object, bulk.Object);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 10, Length = 7 });

        Assert.True(response.Result);
        Assert.Null(response.ErrorMessage);
        bulk.Verify(b => b.InsertAsync(It.IsAny<IEnumerable<BulkInsertCode>>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task GenerateCodes_GeneratorReturnsNoCandidates_Fails()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var gen = new Mock<ICodeGenerator>();
        gen.Setup(g => g.GenerateBatch(It.IsAny<int>(), It.IsAny<int>()))
           .Returns((int count, int _) => Enumerable.Repeat("   ", count).ToList());

        var bulk = new Mock<IBulkInserter>(MockBehavior.Strict);
        var svc = CreateService(dbFactory, gen.Object, bulk.Object);

        var response = await svc.GenerateCodesAsync(new GenerateCodesRequest { Count = 2, Length = 7 });

        Assert.False(response.Result);
        Assert.StartsWith("Could not generate requested number.", response.ErrorMessage);
        bulk.Verify(b => b.InsertAsync(It.IsAny<IEnumerable<BulkInsertCode>>()), Times.Never);
    }

    [Fact]
    public async Task UseCode_InvalidFormat_ReturnsInvalidFormat()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var svc = CreateService(dbFactory);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "BAD" });

        Assert.Equal(UseCodeResult.InvalidFormat, resp.ResultCode);
        Assert.Equal("Invalid code format", resp.Message);
    }

    [Fact]
    public async Task UseCode_CacheHit_Used_ReturnsAlreadyUsed_WithoutDb()
    {
        var dbFactory = new Mock<IDbContextFactory<DiscountDbContext>>(MockBehavior.Strict);
        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync(JsonSerializer.Serialize(new { IsUsed = true, Exists = true, CachedAt = DateTime.UtcNow }));

        var svc = CreateService(dbFactory.Object, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.AlreadyUsed, resp.ResultCode);
        Assert.Equal("Already used", resp.Message);
        dbFactory.Verify(f => f.CreateDbContext(), Times.Never);
    }

    [Fact]
    public async Task UseCode_CacheHit_NotExists_ReturnsNotFound_WithoutDb()
    {
        var dbFactory = new Mock<IDbContextFactory<DiscountDbContext>>(MockBehavior.Strict);
        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync(JsonSerializer.Serialize(new { IsUsed = false, Exists = false, CachedAt = DateTime.UtcNow }));

        var svc = CreateService(dbFactory.Object, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.NotFound, resp.ResultCode);
        Assert.Equal("Not found", resp.Message);
        dbFactory.Verify(f => f.CreateDbContext(), Times.Never);
    }

    [Fact]
    public async Task UseCode_CacheReadTimeout_ContinuesToDb_NotFound()
    {
        using var dbFactory = new SqliteTestDbFactory();
        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ThrowsAsync(new TimeoutException("cache timeout"));

        var svc = CreateService(dbFactory, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.NotFound, resp.ResultCode);
        Assert.Equal("Not found", resp.Message);
    }

    [Fact]
    public async Task UseCode_SuccessfulUse_UpdatesAndCaches()
    {
        using var dbFactory = new SqliteTestDbFactory();
        // Seed a not-yet-used code
        dbFactory.Seed(new DiscountCode
        {
            Code = "ABCDEFG",
            Length = 7,
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        });

        var cache = new Mock<ICacheManager>();
        string? cachedJson = null;
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync((string?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
             .Callback<string, string, TimeSpan>((_, v, __) => cachedJson = v)
             .ReturnsAsync(true);

        var svc = CreateService(dbFactory, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.Success, resp.ResultCode);
        Assert.Equal("OK", resp.Message);

        Assert.NotNull(cachedJson);
        using var doc = JsonDocument.Parse(cachedJson!);
        Assert.True(doc.RootElement.GetProperty("IsUsed").GetBoolean());
        Assert.True(doc.RootElement.GetProperty("Exists").GetBoolean());
    }

    [Fact]
    public async Task UseCode_AlreadyUsedInDb_CachesAndReturnsAlreadyUsed()
    {
        using var dbFactory = new SqliteTestDbFactory();
        dbFactory.Seed(new DiscountCode
        {
            Code = "ABCDEFG",
            Length = 7,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            UsedAt = DateTime.UtcNow.AddHours(-1)
        });

        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync((string?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
             .ReturnsAsync(true);

        var svc = CreateService(dbFactory, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.AlreadyUsed, resp.ResultCode);
        Assert.Equal("Already used", resp.Message);

        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("\"IsUsed\":true") && s.Contains("\"Exists\":true")),
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task UseCode_NotFoundInDb_CachesExistsFalse()
    {
        using var dbFactory = new SqliteTestDbFactory();

        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync((string?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
             .ReturnsAsync(true);

        var svc = CreateService(dbFactory, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.NotFound, resp.ResultCode);
        Assert.Equal("Not found", resp.Message);

        cache.Verify(c => c.SetAsync(
            It.IsAny<string>(),
            It.Is<string>(s => s.Contains("\"IsUsed\":false") && s.Contains("\"Exists\":false")),
            It.IsAny<TimeSpan>()), Times.Once);
    }

    [Fact]
    public async Task UseCode_CacheSetThrows_IgnoredAndReturnsSuccess()
    {
        using var dbFactory = new SqliteTestDbFactory();
        dbFactory.Seed(new DiscountCode
        {
            Code = "ABCDEFG",
            Length = 7,
            CreatedAt = DateTime.UtcNow,
            UsedAt = null
        });

        var cache = new Mock<ICacheManager>();
        cache.Setup(c => c.GetAsync(It.IsAny<string>()))
             .ReturnsAsync((string?)null);
        cache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
             .ThrowsAsync(new Exception("cache write failed"));

        var svc = CreateService(dbFactory, cache: cache.Object);

        var resp = await svc.UseCodeAsync(new UseCodeRequest { Code = "ABCDEFG" });

        Assert.Equal(UseCodeResult.Success, resp.ResultCode);
        Assert.Equal("OK", resp.Message);
    }

    private sealed class SqliteTestDbFactory : IDbContextFactory<DiscountDbContext>, IDisposable
    {
        private readonly SqliteConnection _conn;
        private readonly DbContextOptions<DiscountDbContext> _options;

        public SqliteTestDbFactory()
        {
            _conn = new SqliteConnection("DataSource=:memory:");
            _conn.Open();

            _options = new DbContextOptionsBuilder<DiscountDbContext>()
                .UseSqlite(_conn)
                .EnableSensitiveDataLogging()
                .Options;

            using var ctx = new DiscountDbContext(_options);
            ctx.Database.EnsureCreated();
        }

        public DiscountDbContext CreateDbContext() => new DiscountDbContext(_options);

        public void Seed(params DiscountCode[] codes)
        {
            using var ctx = CreateDbContext();
            ctx.DiscountCodes.AddRange(codes);
            ctx.SaveChanges();
        }

        public void Dispose() => _conn.Dispose();
    }
}