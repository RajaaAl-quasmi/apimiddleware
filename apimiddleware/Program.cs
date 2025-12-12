using ApiMiddleware.Data;
using ApiMiddleware.Middleware;
using ApiMiddleware.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ApiMiddleware.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<IsolationForestOptions>(
   builder.Configuration.GetSection("MLServer")
);
builder.Services.AddHttpClient<IsolationForestClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<IsolationForestOptions>>().Value;
    client.BaseAddress = new Uri(opt.Url.TrimEnd('/') + "/");
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// HttpClient + Services

builder.Services.AddScoped<RequestCacheService>();
builder.Services.AddScoped<RoutingService>();
builder.Services.AddScoped<HealthCheckService>();

// Database connection - InMemory 
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("ApiMiddlewareDb"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

// Gateway middleware
app.UseMiddleware<GatewayMiddleware>();

app.MapControllers();

app.Run();