namespace EPS.Discount.Core.Models;

public class CachedCodeStatus
{
    public bool IsUsed { get; set; }
    public bool Exists { get; set; }
    public DateTime CachedAt { get; set; }
}
