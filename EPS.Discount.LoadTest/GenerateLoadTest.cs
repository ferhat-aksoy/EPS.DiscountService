using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using EPS.Discount.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using EPS.Discount.Data;

namespace EPS.Discount.LoadTest;

public class GenerateLoadTest
{
    private readonly IServiceProvider _provider;
    private readonly ICodeGenerator _generator;

    public GenerateLoadTest(IServiceProvider provider, ICodeGenerator generator)
    {
        _provider = provider;
        _generator = generator;
    }

    public async Task Run(int concurrentClients, int requestsPerClient, int codesPerRequest, int codeLength)
    {
        var results = new ConcurrentBag<(bool success, string error)>();
        var sw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, concurrentClients).Select(clientId => Task.Run(async () =>
        {
            // Each client simulates sequential requests; can be parallel inside too.
            var scopeFactory = _provider.GetRequiredService<IServiceScopeFactory>();

            for (int r = 0; r < requestsPerClient; r++)
            {
                using var scope = scopeFactory.CreateScope();

                // Resolve service per call to simulate separate gRPC calls (per-request scope)
                var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiscountDbContext>>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Application.Services.DiscountService>>();
                var bulkInserter = scope.ServiceProvider.GetRequiredService<IBulkInserter>();
                var redis = scope.ServiceProvider.GetRequiredService<ICacheManager>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var service = new Application.Services.DiscountService(dbFactory, _generator, logger, bulkInserter, redis, configuration);

                try
                {
                    var request = new Core.Models.GenerateCodesRequest { Count = codesPerRequest, Length = codeLength };
                    var resp = await service.GenerateCodesAsync(request);
                    results.Add((resp.Result, resp.ErrorMessage));
                }
                catch (Exception ex)
                {
                    results.Add((false, ex.Message));
                }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();

        var totalRequests = concurrentClients * requestsPerClient;
        var successCount = results.Count(r => r.success);
        var failedCount = results.Count(r => !r.success);
        var uniqueCodes = await CountTotalCodesInDb();

        Console.WriteLine("Load test finished:");
        Console.WriteLine($"Clients: {concurrentClients}, Requests/client: {requestsPerClient}, Total requests: {totalRequests}");
        Console.WriteLine($"Success responses: {successCount}, Failed responses: {failedCount}");
        Console.WriteLine($"Elapsed: {sw.Elapsed}");
        Console.WriteLine($"Total codes in DB (approx): {uniqueCodes}");
    }

    private async Task<long> CountTotalCodesInDb()
    {
        using var scope = _provider.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DiscountDbContext>>();
        using var db = dbFactory.CreateDbContext();
        return await db.DiscountCodes.LongCountAsync();
    }
    }

