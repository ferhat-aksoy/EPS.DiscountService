using EPS.Discount.Core;
using EPS.Discount.LoadTest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using EPS.Discount.Core.Interfaces;
using EPS.Discount.Data;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using EPS.Discount.Application;

var services = new ServiceCollection();

// Build configuration with connection string
var configuration = new ConfigurationBuilder()
    .AddInMemoryCollection(new Dictionary<string, string>
    {
        ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=discount_test;Username=postgres;Password=Ferhat@1234",
        ["ConnectionStrings:Redis"] = "localhost:6379,password=Ferhat@1234,ssl=False,abortConnect=False"
    })
    .Build();

// Register IConfiguration
services.AddSingleton<IConfiguration>(configuration);

services.AddDbContextFactory<DiscountDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(redisConnectionString);
    configuration.AbortOnConnectFail = false;
    configuration.ConnectTimeout = 5000;
    configuration.SyncTimeout = 5000;
    return ConnectionMultiplexer.Connect(configuration);
});

services.AddLogging(builder => builder.AddConsole());
services.AddScoped<IBulkInserter, PostgresBulkInserter>();
services.AddScoped<ICacheManager, RedisCacheManager>();

var serviceProvider = services.BuildServiceProvider();
var codeGenerator = new CodeGenerator();
var loadTest = new GenerateLoadTest(serviceProvider, codeGenerator);

await loadTest.Run(
    concurrentClients: 10,
    requestsPerClient: 100,
    codesPerRequest: 50,
    codeLength: 8
);