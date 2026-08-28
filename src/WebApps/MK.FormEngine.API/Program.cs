using KH.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using MK.FormEngine.API;
using Shared.Swagger;
using Shared.Logs;

var builder = WebApplication.CreateBuilder(args);

// Add Serilog Logging
builder.AddSerilogLogging();

// Add Request-Response Audit Logging (Hangfire, DbContext, Services)
builder.AddAuditLoggingServices();

// Add services to the container.
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

// OpenAPI 
builder.Services.AddSwaggerDocs();

// Enable CORS reading allowed origins from appsettings.json
var allowedOrigins = builder.Configuration.GetSection("CorsOrigins").Get<string[]>() 
    ?? new[] { "http://localhost:4200", "https://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDev", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Enable CORS middleware (must be placed before UseAuthentication & UseAuthorization)
app.UseCors("AllowAngularDev");

// Enable Tenant Logging enrichment
app.UseTenantLogging();

// Enable request-response audit logging middleware and dashboard
app.UseAuditLogging();

// Configure the HTTP request pipeline.
app.UseSwaggerDocsMiddleware();

await app.InitialiseDatabaseAsync();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
