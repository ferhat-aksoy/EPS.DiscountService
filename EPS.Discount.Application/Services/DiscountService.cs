using EPS.Discount.Core.Interfaces;
using EPS.Discount.Core.Models;
using EPS.Discount.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EPS.Discount.Application.Services;

public class DiscountService : IDiscountService
{
    private const int MaxGenerate = 2000;
    private const int DefaultBatchSize = 500;
    private const int DefaultCacheExpirationMinutes = 1440; // 24 hours

    private readonly IDbContextFactory<DiscountDbContext> _dbFactory;
    private readonly ICodeGenerator _generator;
    private readonly ILogger<DiscountService> _logger;
    private readonly IBulkInserter _bulkInserter;
    private readonly ICacheManager _cacheManager;
    private readonly TimeSpan _cacheExpiration;

    private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;

    public DiscountService(
        IDbContextFactory<DiscountDbContext> dbFactory,
        ICodeGenerator generator,
        ILogger<DiscountService> logger,
        IBulkInserter bulkInserter,
        ICacheManager cacheManager,
        IConfiguration configuration)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _bulkInserter = bulkInserter ?? throw new ArgumentNullException(nameof(bulkInserter));
        _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));

        var cacheExpirationMinutes = configuration.GetValue<int>(
            "Redis:CacheExpirationMinutes",
            DefaultCacheExpirationMinutes);
        _cacheExpiration = TimeSpan.FromMinutes(cacheExpirationMinutes);
    }

    public async Task<GenerateCodesResponse> GenerateCodesAsync(GenerateCodesRequest request)
    {
        if (request.Count < 1 || request.Count > MaxGenerate)
            return new GenerateCodesResponse { Result = false, ErrorMessage = $"Count must be between 1 and {MaxGenerate}" };

        if (request.Length != 7 && request.Length != 8)
            return new GenerateCodesResponse { Result = false, ErrorMessage = "Length must be 7 or 8" };

        int remaining = request.Count;
        int length = request.Length;
        int consecutiveFailures = 0;
        const int maxConsecutiveFailures = 3;
        var allGenerated = new HashSet<string>(CodeComparer);

        while (remaining > 0)
        {
            if (consecutiveFailures >= maxConsecutiveFailures)
            {
                _logger.LogError("Max consecutive failures reached");
                break;
            }

            int batchSize = Math.Min(DefaultBatchSize, remaining);

            var candidates = _generator.GenerateBatch(batchSize, length)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Where(c => !allGenerated.Contains(c))
                .Distinct(CodeComparer)
                .ToList();

            if (!candidates.Any())
            {
                _logger.LogWarning("Generator returned no unique candidates; breaking.");
                break;
            }

            foreach (var c in candidates) allGenerated.Add(c);

            // Query DB for existing codes 
            List<string> existing = [];
            using (var db = _dbFactory.CreateDbContext())
            {
                // chunk to avoid large IN (...) queries
                const int checkChunk = 200;
                for (int i = 0; i < candidates.Count; i += checkChunk)
                {
                    var chunk = candidates.Skip(i).Take(checkChunk).ToList();
                    var existingCodes = await db.DiscountCodes
                        .Where(x => chunk.Contains(x.Code))
                        .Select(x => x.Code)
                        .ToListAsync();
                    existing.AddRange(existingCodes);
                }
            }

            var existingSet = new HashSet<string>(existing, CodeComparer);

            var toInsert = candidates
                .Where(c => !existingSet.Contains(c))
                .Select(c => new BulkInsertCode
                {
                    Code = c,
                    Length = length,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            // Use the IBulkInserter to perform efficient insert
            try
            {
                await _bulkInserter.InsertAsync(toInsert);
                remaining -= toInsert.Count;
                _logger.LogInformation("Inserted {Count} codes; remaining {Remaining}", toInsert.Count, remaining);
            }
            catch (Exception ex) when (ex is DbUpdateException || ex is InvalidOperationException)
            {
                _logger.LogDebug(ex, "Insert collided with concurrent writes; will retry to fill remaining.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during bulk insert.");
                return new GenerateCodesResponse { Result = false, ErrorMessage = "Internal error" };
            }

            if (toInsert.Count > 0)
            {
                consecutiveFailures = 0;
                // Optional: Remove inserted codes from tracking
                foreach (var code in toInsert)
                {
                    allGenerated.Remove(code.Code);
                }
            }
            else
            {
                consecutiveFailures++;
            }
        }

        return remaining == 0
            ? new GenerateCodesResponse { Result = true }
            : new GenerateCodesResponse { Result = false, ErrorMessage = $"Could not generate requested number. Remaining: {remaining}" };
    }

    public async Task<UseCodeResponse> UseCodeAsync(UseCodeRequest request)
    {
        var code = (request.Code ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(code) || (code.Length != 7 && code.Length != 8))
            return new UseCodeResponse { ResultCode = UseCodeResult.InvalidFormat, Message = "Invalid code format" };

        var cacheKey = $"discount:code:{code}";

        CachedCodeStatus? cachedStatus = null;
        try
        {
            var cachedValue = await _cacheManager.GetAsync(cacheKey);
            if (cachedValue != null)
            {
                cachedStatus = JsonSerializer.Deserialize<CachedCodeStatus>(cachedValue);
                if (cachedStatus?.IsUsed == true)
                {
                    _logger.LogDebug("Code {Code} found in cache as used", code);
                    return new UseCodeResponse { ResultCode = UseCodeResult.AlreadyUsed, Message = "Already used" };
                }
                if (cachedStatus?.Exists == false)
                {
                    _logger.LogDebug("Code {Code} found in cache as non-existent", code);
                    return new UseCodeResponse { ResultCode = UseCodeResult.NotFound, Message = "Not found" };
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or TimeoutException)
        {
            _logger.LogWarning(ex, "Cache read failed for {Code}, continuing without cache", code);
        }

        return await UseCodeInDatabase(code, cacheKey);
    }

    private async Task<UseCodeResponse> UseCodeInDatabase(string code, string cacheKey)
    {
        using var db = _dbFactory.CreateDbContext();
        var now = DateTime.UtcNow;

        var affected = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"DiscountCodes\" SET \"UsedAt\" = {now} WHERE \"Code\" = {code} AND \"UsedAt\" IS NULL");

        if (affected == 1)
        {
            await CacheCodeStatus(cacheKey, isUsed: true, exists: true);
            _logger.LogInformation("Code {Code} successfully used", code);
            return new UseCodeResponse { ResultCode = UseCodeResult.Success, Message = "OK" };
        }

        var existing = await db.DiscountCodes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == code);

        if (existing == null)
        {
            await CacheCodeStatus(cacheKey, isUsed: false, exists: false);
            _logger.LogDebug("Code {Code} not found in database", code);
            return new UseCodeResponse
            {
                ResultCode = UseCodeResult.NotFound,
                Message = "Not found"
            };
        }

        if (existing.UsedAt != null)
        {
            await CacheCodeStatus(cacheKey, isUsed: true, exists: true);
            _logger.LogDebug("Code {Code} already used at {UsedAt}", code, existing.UsedAt);
            return new UseCodeResponse
            {
                ResultCode = UseCodeResult.AlreadyUsed,
                Message = "Already used"
            };
        }

        _logger.LogWarning("Code {Code} exists but update failed unexpectedly", code);
        return new UseCodeResponse { ResultCode = UseCodeResult.UnknownError, Message = "Unknown error" };
    }

    private async Task CacheCodeStatus(string cacheKey, bool isUsed, bool exists)
    {
        try
        {
            var status = new CachedCodeStatus
            {
                IsUsed = isUsed,
                Exists = exists,
                CachedAt = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(status);
            await _cacheManager.SetAsync(cacheKey, json, _cacheExpiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cache code status for {CacheKey}", cacheKey);
        }
    }
}