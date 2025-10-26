using EPS.Discount.Core.Models;

namespace EPS.Discount.Core.Interfaces;

public interface IDiscountService
{
    Task<GenerateCodesResponse> GenerateCodesAsync(GenerateCodesRequest request);
    Task<UseCodeResponse> UseCodeAsync(UseCodeRequest request);
}
