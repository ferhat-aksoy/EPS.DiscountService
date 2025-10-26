// See https://aka.ms/new-console-template for more information
using Discount.Proto;
using Grpc.Net.Client;

using var channel = GrpcChannel.ForAddress("http://localhost:5000");
var client = new DiscountService.DiscountServiceClient(channel);

var genResp = await client.GenerateCodesAsync(new GenerateRequest { Count = 2000, Length = 8 });
Console.WriteLine($"Generate: success={genResp.Result} err={genResp.ErrorMessage}");


// Replace with actual code returned or known code
var useResp = await client.UseCodeAsync(new UseCodeRequest { Code = "AZ8VGAUC" });
Console.WriteLine($"UseCode result={useResp.ResultCode} msg={useResp.Message}");

Console.ReadLine();