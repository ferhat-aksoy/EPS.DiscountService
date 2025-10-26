namespace EPS.Discount.Data;

public class DiscountCode
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public int Length { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public string? UsedBy { get; set; }
}
