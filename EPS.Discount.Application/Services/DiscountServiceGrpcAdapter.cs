using Discount.Proto;
using EPS.Discount.Core.Interfaces;
using EPS.Discount.Core.Models;
using Grpc.Core;
using static Discount.Proto.DiscountService;
using UseCodeRequest = Discount.Proto.UseCodeRequest;
using UseCodeResponse = Discount.Proto.UseCodeResponse;

namespace EPS.Discount.Application.Services;

public class DiscountServiceGrpcAdapter : DiscountServiceBase
{
    private readonly IDiscountService _discountService;

    public DiscountServiceGrpcAdapter(IDiscountService discountService)
    {
        _discountService = discountService ?? throw new ArgumentNullException(nameof(discountService));
    }

    public override async Task<GenerateResponse> GenerateCodes(GenerateRequest request, ServerCallContext context)
    {
        var coreRequest = new GenerateCodesRequest
        {
            Count = (int)request.Count,
            Length = (int)request.Length
        };

        var result = await _discountService.GenerateCodesAsync(coreRequest);

        return new GenerateResponse
        {
            Result = result.Result,
            ErrorMessage = result.ErrorMessage ?? string.Empty
        };
    }

    public override async Task<UseCodeResponse> UseCode(UseCodeRequest request, ServerCallContext context)
    {
        var coreRequest = new Core.Models.UseCodeRequest
        {
            Code = request.Code ?? string.Empty
        };

        var result = await _discountService.UseCodeAsync(coreRequest);

        return new UseCodeResponse
        {
            ResultCode = (uint)result.ResultCode,
            Message = result.Message
        };
    }
}