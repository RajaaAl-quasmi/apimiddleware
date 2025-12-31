using ApiMiddleware.Data;
using ApiMiddleware.Middleware;
using ApiMiddleware.Models;
using ApiMiddleware.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Bind ML server URL
builder.Services.Configure<IsolationForestOptions>(
    builder.Configuration.GetSection("MLServer"));

builder.Services.AddHttpClient<IsolationForestClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<IsolationForestOptions>>().Value;
    client.BaseAddress = new Uri(opt.Url.TrimEnd('/') + "/");
});

builder.Services.AddHttpClient("ProxyClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip
                               | DecompressionMethods.Deflate
                               | DecompressionMethods.Brotli,
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false, // Disable proxy for better performance
        MaxConnectionsPerServer = 100 // Allow more concurrent connections
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<RequestCacheService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<HealthCheckService>();

// Real MySQL with Pomelo
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseMiddleware<GatewayMiddleware>();
app.UseAuthorization();
app.MapControllers();

app.Run();