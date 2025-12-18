using System.Text.Json;
using System.Text.Json.Serialization;
using GameEngine;
using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;
using GameEngineHost.Services;
using Microsoft.AspNetCore.OpenApi;
using RNGClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.Converters.Add(new MoneyJsonConverter());
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS to allow RGS to call backend engine
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowRGS", policy =>
    {
        policy
            .WithOrigins("http://localhost:5101", "http://localhost:5000", "http://127.0.0.1:5101", "http://127.0.0.1:5000")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var configDirectory = ResolvePath(builder.Configuration["GameEngine:ConfigurationDirectory"] ?? "configs", builder.Environment);
var manifestPath = ResolvePath(builder.Configuration["GameEngine:ControlProgramManifest"] ?? "control-program-manifest.json", builder.Environment);
builder.Services.AddGameEngine(configDirectory, manifestPath);
builder.Services.AddSingleton<ISpinTelemetrySink, NullSpinTelemetrySink>();
builder.Services.AddSingleton<IEngineClient, LocalEngineClient>();
// Use integrated RNG service with Fortuna PRNG instead of external HTTP service
builder.Services.AddSingleton<IRngClient>(sp =>
{
    var fortunaPrng = sp.GetRequiredService<FortunaPrng>();
    return new IntegratedRngClient(fortunaPrng);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("AllowRGS");

// Health check endpoint for Kubernetes
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .WithTags("Health")
    .WithName("HealthCheck");

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();

static string ResolvePath(string path, IWebHostEnvironment environment)
{
    if (Path.IsPathRooted(path))
    {
        return path;
    }

    return Path.GetFullPath(Path.Combine(environment.ContentRootPath, path));
}
