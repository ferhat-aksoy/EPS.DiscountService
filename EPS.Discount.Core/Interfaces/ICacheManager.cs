namespace EPS.Discount.Core.Interfaces;

public interface ICacheManager
{
    Task<bool> SetAsync(string key, string value, TimeSpan timeSpan);
    Task<string> GetAsync(string key);

}
