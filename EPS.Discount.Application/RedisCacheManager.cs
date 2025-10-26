using EPS.Discount.Core.Interfaces;
using StackExchange.Redis;

namespace EPS.Discount.Application;

public class RedisCacheManager(IConnectionMultiplexer redis) : ICacheManager
{
    private readonly IDatabase cache = redis.GetDatabase();
    public async Task<string> GetAsync(string key)
    {
        var result = await cache.StringGetAsync(key);

        return result.HasValue ? result.ToString() : null;
    }

    public async Task<bool> SetAsync(string key, string value,TimeSpan timeSpan)
    {
        return await cache.StringSetAsync(key, value,timeSpan);
    }


}
