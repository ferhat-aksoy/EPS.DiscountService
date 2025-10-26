namespace EPS.Discount.Core.Models;

public class BulkInsertCode
{
    public required string Code { get; init; }
    public int Length { get; init; }
    public DateTime CreatedAt { get; init; }
}