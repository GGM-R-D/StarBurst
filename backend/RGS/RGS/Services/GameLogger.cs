using System;
using System.IO;
using System.Text;
using System.Threading;

namespace RGS.Services;

public sealed class GameLogger
{
    private static readonly object _lockObject = new object();
    private static string? _logFilePath;
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "star-burst");

    static GameLogger()
    {
        // Ensure log directory exists
        if (!Directory.Exists(LogDirectory))
        {
            Directory.CreateDirectory(LogDirectory);
        }

        // Create log file with timestamp
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        _logFilePath = Path.Combine(LogDirectory, $"starburst-log-{timestamp}.txt");
        
        // Write initial header
        WriteLine("=".PadRight(100, '='));
        WriteLine($"STARBURST GAME LOG - Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        WriteLine("=".PadRight(100, '='));
        WriteLine();
    }

    public static void LogSessionStart(string sessionId, string gameId, decimal initialBalance, string playerId)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [SESSION_START]");
        WriteLine($"  SessionId: {sessionId}");
        WriteLine($"  GameId: {gameId}");
        WriteLine($"  PlayerId: {playerId}");
        WriteLine($"  InitialBalance: {initialBalance:F2}");
        WriteLine();
    }

    public static void LogPlayRequest(string sessionId, string roundId, decimal bet, string betMode, bool isRespin, int? respinsRemaining)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [PLAY_REQUEST]");
        WriteLine($"  SessionId: {sessionId}");
        WriteLine($"  RoundId: {roundId}");
        WriteLine($"  Bet: {bet:F2}");
        WriteLine($"  BetMode: {betMode}");
        WriteLine($"  IsRespin: {isRespin}");
        if (respinsRemaining.HasValue)
        {
            WriteLine($"  RespinsRemaining: {respinsRemaining.Value}");
        }
        WriteLine();
    }

    public static void LogEngineResponse(string roundId, decimal win, int? respinsRemaining, bool featureEnded, string gridLayout)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ENGINE_RESPONSE]");
        WriteLine($"  RoundId: {roundId}");
        WriteLine($"  Win: {win:F2}");
        if (respinsRemaining.HasValue)
        {
            WriteLine($"  RespinsRemaining: {respinsRemaining.Value}");
            WriteLine($"  FeatureEnded: {respinsRemaining.Value == 0}");
        }
        WriteLine($"  FeatureEnded: {featureEnded}");
        WriteLine($"  GridLayout:");
        WriteLine(gridLayout);
        WriteLine();
    }

    public static void LogBalanceUpdate(string sessionId, decimal prevBalance, decimal bet, decimal win, decimal newBalance)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [BALANCE_UPDATE]");
        WriteLine($"  SessionId: {sessionId}");
        WriteLine($"  PreviousBalance: {prevBalance:F2}");
        WriteLine($"  Bet: {bet:F2}");
        WriteLine($"  Win: {win:F2}");
        WriteLine($"  NewBalance: {newBalance:F2}");
        WriteLine($"  Calculation: {prevBalance:F2} - {bet:F2} + {win:F2} = {newBalance:F2}");
        WriteLine();
    }

    public static void LogRgsResponse(string roundId, decimal balance, string featureName, string featureType, int featureIsClosure, int gameMode)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [RGS_RESPONSE]");
        WriteLine($"  RoundId: {roundId}");
        WriteLine($"  Balance: {balance:F2}");
        WriteLine($"  FeatureName: {featureName}");
        WriteLine($"  FeatureType: {featureType}");
        WriteLine($"  FeatureIsClosure: {featureIsClosure}");
        WriteLine($"  GameMode: {gameMode}");
        WriteLine();
    }

    public static void LogWildFeature(string roundId, int wildReel, int respinsAwarded, int totalRespinsRemaining, string lockedReels)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WILD_FEATURE]");
        WriteLine($"  RoundId: {roundId}");
        WriteLine($"  WildReel: {wildReel + 1} (0-based: {wildReel})");
        WriteLine($"  RespinsAwarded: {respinsAwarded}");
        WriteLine($"  TotalRespinsRemaining: {totalRespinsRemaining}");
        WriteLine($"  LockedReels: {lockedReels}");
        WriteLine();
    }

    public static void LogError(string context, string error)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ERROR] [{context}]");
        WriteLine($"  {error}");
        WriteLine();
    }

    public static void LogInfo(string message)
    {
        WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] {message}");
    }

    private static void WriteLine(string? line = null)
    {
        lock (_lockObject)
        {
            try
            {
                if (_logFilePath != null)
                {
                    File.AppendAllText(_logFilePath, (line ?? string.Empty) + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                // Fallback to console if file write fails
                Console.WriteLine($"[GameLogger Error] {ex.Message}");
                Console.WriteLine(line ?? string.Empty);
            }
        }
    }
}
