using EPS.Discount.Application;
using EPS.Discount.Application.Services;
using EPS.Discount.Core;
using EPS.Discount.Core.Interfaces;
using EPS.Discount.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackExchange.Redis;

namespace EPS.Discount.Server.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            MaxPoolSize = 100,
            MinPoolSize = 10,
            ConnectionIdleLifetime = 300,
            ConnectionPruningInterval = 10,
            MaxAutoPrepare = 20,
            AutoPrepareMinUsages = 2
        };

        services.AddDbContextFactory<DiscountDbContext>(opts =>
            opts.UseNpgsql(
                connectionStringBuilder.ToString(),
                b => b.MigrationsAssembly("EPS.Discount.Data")
                    .EnableRetryOnFailure(maxRetryCount: 3)
                    .CommandTimeout(30)
            ));

        var provider = configuration["Database:Provider"]!;
        if (provider == "sqlserver")
        {
            services.AddSingleton<IBulkInserter, SqlServerBulkInserter>();
        }
        else
        {
            services.AddSingleton<IBulkInserter, PostgresBulkInserter>();
        }

        return services;
    }

    public static IServiceCollection AddCachingServices(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var config = ConfigurationOptions.Parse(redisConnectionString);
            config.AbortOnConnectFail = false;
            config.ConnectTimeout = 5000;
            config.SyncTimeout = 5000;
            return ConnectionMultiplexer.Connect(config);
        });

        services.AddScoped<ICacheManager, RedisCacheManager>();
        return services;
    }

    public static IServiceCollection AddDiscountServices(this IServiceCollection services)
    {
        services.AddSingleton<ICodeGenerator, CodeGenerator>();
        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<DiscountServiceGrpcAdapter>();
        return services;
    }
}