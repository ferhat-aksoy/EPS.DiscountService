using EPS.Discount.Application.Services;
using EPS.Discount.Data;
using EPS.Discount.Server.Extensions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddGrpc();
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddCachingServices(builder.Configuration);
builder.Services.AddDiscountServices();

var app = builder.Build();

// Apply database migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DiscountDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
app.MapGrpcService<DiscountServiceGrpcAdapter>();

app.Run();
