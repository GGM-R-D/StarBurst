using GameEngine.Configuration;
using GameEngine.Play;
using GameEngine.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GameEngine;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameEngine(
        this IServiceCollection services,
        string configurationDirectory,
        string controlProgramManifestPath)
    {
        services.AddSingleton(new GameConfigurationLoader(configurationDirectory));
        services.AddSingleton(new Security.ControlProgramVerifier(controlProgramManifestPath));
        services.AddSingleton<ITimeService, TimeService>();
        services.AddSingleton<FortunaPrng>();
        services.AddSingleton<WinEvaluator>();
        services.AddSingleton<ISpinTelemetrySink, LoggingSpinTelemetrySink>();
        services.AddSingleton<SpinHandler>();
        services.AddSingleton<IGameEngineService, GameEngineService>();
        return services;
    }
}

