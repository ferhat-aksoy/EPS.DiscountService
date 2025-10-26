using EPS.Discount.Core.Models;

namespace EPS.Discount.Core.Interfaces;

public interface IBulkInserter
{
    Task InsertAsync(IEnumerable<BulkInsertCode> entities);
}
