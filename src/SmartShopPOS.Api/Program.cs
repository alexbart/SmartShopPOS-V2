using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SmartShopPOS.Api.Health;
using SmartShopPOS.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql");

builder.Services.AddDbContext<SmartShopPosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Name == "postgresql"
});
app.MapHealthChecks("/health");

app.Run();

public partial class Program
{
}
