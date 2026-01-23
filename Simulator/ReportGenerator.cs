using System.Text;

namespace Simulator;

/// <summary>
/// Generates formatted simulation reports for console and file output.
/// </summary>
public sealed class ReportGenerator
{
    private const decimal TARGET_RTP = 96.09m; // Starburst theoretical RTP
    private const decimal MAX_WIN_MULTIPLIER = 500m;

    // Payline definitions for reference
    private static readonly string[] PaylineDescriptions = new[]
    {
        "Middle row (straight)",
        "Top row (straight)",
        "Bottom row (straight)",
        "V-shape up",
        "V-shape down (inverted V)",
        "Top-center",
        "Bottom-center",
        "Bottom-heavy",
        "Top-heavy",
        "Alternating"
    };

    // Symbol order for display (high-paying first, WILD included for completeness)
    private static readonly string[] SymbolOrder = new[]
    {
        "WILD", "BAR", "SEVEN", "ORANGE", "GREEN", "RED", "BLUE", "PURPLE"
    };

    public string GenerateReport(StatisticsSnapshot stats, TimeSpan elapsed)
    {
        var sb = new StringBuilder();
        
        AppendHeader(sb);
        AppendOverview(sb, stats, elapsed);
        AppendRTPAnalysis(sb, stats);
        AppendFeatureStats(sb, stats);
        AppendPaylineReachability(sb, stats);
        AppendMultiplierDistribution(sb, stats);
        AppendSymbolAnalysis(sb, stats);
        AppendMaxWinAnalysis(sb, stats);
        AppendFooter(sb);

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                     STARBURST SIMULATION REPORT                              ║");
        sb.AppendLine("║                   GLI-19 Compliance Verification                             ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
    }

    private static void AppendOverview(StringBuilder sb, StatisticsSnapshot stats, TimeSpan elapsed)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                              SIMULATION OVERVIEW                               ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine($"  Spins Completed:     {stats.TotalSpins:N0}");
        sb.AppendLine($"  Execution Time:      {elapsed.TotalSeconds:F2}s ({stats.TotalSpins / elapsed.TotalSeconds:N0} spins/sec)");
        sb.AppendLine($"  Total Bet:           R{stats.TotalBet:N2}");
        sb.AppendLine($"  Total Win:           R{stats.TotalWin:N2}");
        sb.AppendLine($"  Net Result:          R{stats.TotalWin - stats.TotalBet:N2}");
        sb.AppendLine();
    }

    private static void AppendRTPAnalysis(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                              RTP & HIT FREQUENCY                               ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        
        var rtpDeviation = stats.RTP - TARGET_RTP;
        var rtpStatus = Math.Abs(rtpDeviation) <= 0.5m ? "PASS" : (Math.Abs(rtpDeviation) <= 1.0m ? "WARN" : "FAIL");
        
        sb.AppendLine($"  Calculated RTP:      {stats.RTP:F4}%");
        sb.AppendLine($"  Target RTP:          {TARGET_RTP:F2}%");
        sb.AppendLine($"  Deviation:           {rtpDeviation:+0.00;-0.00}% [{rtpStatus}]");
        sb.AppendLine();
        sb.AppendLine($"  Hit Frequency:       {stats.HitFrequency:F2}% (1 in {(stats.HitFrequency > 0 ? 100m / stats.HitFrequency : 0):F1} spins)");
        sb.AppendLine($"  Winning Spins:       {stats.WinningSpins:N0} / {stats.TotalSpins:N0}");
        sb.AppendLine();

        // Standard deviation note
        if (stats.TotalSpins >= 1_000_000)
        {
            // Approximate 95% confidence interval for RTP
            // Standard error ≈ sqrt(variance / n) * 100 for percentage
            // For slot games, typical variance is around 10-50 for high volatility
            var approximateSE = 15m / (decimal)Math.Sqrt((double)stats.TotalSpins) * 100m;
            sb.AppendLine($"  95% Confidence:      {stats.RTP - 1.96m * approximateSE:F2}% - {stats.RTP + 1.96m * approximateSE:F2}%");
            sb.AppendLine();
        }
    }

    private static void AppendFeatureStats(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                           EXPANDING WILDS FEATURE                              ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        
        var featureRate = stats.TotalSpins > 0 ? (decimal)stats.WildRespinTriggers / stats.TotalSpins : 0;
        var featureFreqOneIn = featureRate > 0 ? 1m / featureRate : 0;
        
        sb.AppendLine($"  Feature Triggers:    {stats.WildRespinTriggers:N0} ({stats.FeatureFrequency:F2}%)");
        sb.AppendLine($"  Feature Frequency:   1 in {featureFreqOneIn:F1} spins");
        sb.AppendLine();
        sb.AppendLine("  Wild Reel Distribution:");
        sb.AppendLine($"    Single Wild (1 reel):  {stats.SingleWildTriggers:N0} ({(stats.TotalSpins > 0 ? (decimal)stats.SingleWildTriggers / stats.TotalSpins * 100m : 0):F3}%)");
        sb.AppendLine($"    Double Wild (2 reels): {stats.DoubleWildTriggers:N0} ({(stats.TotalSpins > 0 ? (decimal)stats.DoubleWildTriggers / stats.TotalSpins * 100m : 0):F4}%)");
        sb.AppendLine($"    Triple Wild (3 reels): {stats.TripleWildTriggers:N0} ({(stats.TotalSpins > 0 ? (decimal)stats.TripleWildTriggers / stats.TotalSpins * 100m : 0):F5}%)");
        sb.AppendLine();
        sb.AppendLine($"  Total Respins:       {stats.TotalRespins:N0}");
        sb.AppendLine();
    }

    private void AppendPaylineReachability(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                            PAYLINE REACHABILITY                                ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine("  This table shows min/max wins achieved per payline per symbol.");
        sb.AppendLine("  All paylines should show wins for common symbols (reachability test).");
        sb.AppendLine();

        // Check if all paylines have been hit
        var allPaylinesHit = true;
        for (int i = 1; i <= 10; i++)
        {
            if (!stats.PaylineHitCounts.ContainsKey(i) || stats.PaylineHitCounts[i] == 0)
            {
                allPaylinesHit = false;
                break;
            }
        }
        
        sb.AppendLine($"  Reachability Status: {(allPaylinesHit ? "PASS - All 10 paylines hit" : "INCOMPLETE - Need more spins")}");
        sb.AppendLine();

        // Payline hit summary
        sb.AppendLine("  Payline Hit Summary:");
        for (int i = 1; i <= 10; i++)
        {
            var hitCount = stats.PaylineHitCounts.TryGetValue(i, out var count) ? count : 0;
            var description = i <= PaylineDescriptions.Length ? PaylineDescriptions[i - 1] : "Unknown";
            var status = hitCount > 0 ? "OK" : "NOT HIT";
            sb.AppendLine($"    Payline {i,2}: {hitCount,12:N0} hits - {description} [{status}]");
        }
        sb.AppendLine();

        // Detailed payline/symbol matrix
        sb.AppendLine("  Detailed Min/Max Wins by Payline and Symbol:");
        sb.AppendLine();
        
        // Header
        sb.Append("           ");
        foreach (var symbol in SymbolOrder)
        {
            sb.Append($" {symbol,10}");
        }
        sb.AppendLine();
        sb.AppendLine($"           {new string('-', SymbolOrder.Length * 11)}");

        for (int paylineId = 1; paylineId <= 10; paylineId++)
        {
            sb.Append($"  PL {paylineId,2}:  ");
            
            if (stats.PaylineStats.TryGetValue(paylineId, out var symbolStats))
            {
                foreach (var symbol in SymbolOrder)
                {
                    if (symbolStats.TryGetValue(symbol, out var symStats) && symStats.HitCount > 0)
                    {
                        sb.Append($" {symStats.MinWin:F2}-{symStats.MaxWin:F2}");
                    }
                    else
                    {
                        sb.Append($" {"---",10}");
                    }
                }
            }
            else
            {
                foreach (var _ in SymbolOrder)
                {
                    sb.Append($" {"---",10}");
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine();
    }

    private void AppendMultiplierDistribution(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                          MULTIPLIER DISTRIBUTION                               ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        sb.AppendLine("  Count of wins by symbol and match count (3x, 4x, 5x):");
        sb.AppendLine();
        
        // Header
        sb.AppendLine($"  {"Symbol",-10} {"3-Match",15} {"4-Match",15} {"5-Match",15} {"Total",15}");
        sb.AppendLine($"  {new string('-', 70)}");

        // Check if all multipliers have been achieved
        var all3Match = true;
        var all4Match = true;
        var all5Match = true;

        foreach (var symbol in SymbolOrder)
        {
            if (stats.MultiplierDistribution.TryGetValue(symbol, out var matchCounts))
            {
                var count3 = matchCounts.TryGetValue(3, out var c3) ? c3 : 0;
                var count4 = matchCounts.TryGetValue(4, out var c4) ? c4 : 0;
                var count5 = matchCounts.TryGetValue(5, out var c5) ? c5 : 0;
                var total = count3 + count4 + count5;

                if (count3 == 0) all3Match = false;
                if (count4 == 0) all4Match = false;
                if (count5 == 0) all5Match = false;

                sb.AppendLine($"  {symbol,-10} {count3,15:N0} {count4,15:N0} {count5,15:N0} {total,15:N0}");
            }
            else
            {
                all3Match = false;
                all4Match = false;
                all5Match = false;
                sb.AppendLine($"  {symbol,-10} {"---",15} {"---",15} {"---",15} {"---",15}");
            }
        }
        sb.AppendLine();
        
        // Multiplier verification status
        sb.AppendLine("  Multiplier Verification:");
        sb.AppendLine($"    3-Match (all symbols): {(all3Match ? "PASS" : "INCOMPLETE")}");
        sb.AppendLine($"    4-Match (all symbols): {(all4Match ? "PASS" : "INCOMPLETE")}");
        sb.AppendLine($"    5-Match (all symbols): {(all5Match ? "PASS" : "INCOMPLETE")}");
        sb.AppendLine();
    }

    private void AppendSymbolAnalysis(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                              SYMBOL ANALYSIS                                   ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        
        sb.AppendLine($"  {"Symbol",-10} {"Win Count",15} {"Total Win (R)",18} {"Avg Win (R)",15} {"% of Wins",12}");
        sb.AppendLine($"  {new string('-', 70)}");

        var totalWinCount = stats.SymbolWinCounts.Values.Sum();

        foreach (var symbol in SymbolOrder)
        {
            var winCount = stats.SymbolWinCounts.TryGetValue(symbol, out var wc) ? wc : 0;
            var winAmount = stats.SymbolWinAmounts.TryGetValue(symbol, out var wa) ? wa : 0m;
            var avgWin = winCount > 0 ? winAmount / winCount : 0m;
            var pctOfWins = totalWinCount > 0 ? (decimal)winCount / totalWinCount * 100m : 0m;

            sb.AppendLine($"  {symbol,-10} {winCount,15:N0} {winAmount,18:N2} {avgWin,15:F4} {pctOfWins,12:F2}%");
        }
        sb.AppendLine();
    }

    private static void AppendMaxWinAnalysis(StringBuilder sb, StatisticsSnapshot stats)
    {
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine("                              MAX WIN ANALYSIS                                  ");
        sb.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        sb.AppendLine();
        
        var cappedStatus = stats.MaxWinMultiplier >= MAX_WIN_MULTIPLIER ? "YES (cap hit)" : "NO";
        var maxWinStatus = stats.WinCapHits > 0 ? "VERIFIED" : "NOT YET VERIFIED";
        
        sb.AppendLine($"  Highest Win Amount:  R{stats.MaxWin:N2}");
        sb.AppendLine($"  Highest Multiplier:  {stats.MaxWinMultiplier:F1}x");
        sb.AppendLine($"  Win Cap ({MAX_WIN_MULTIPLIER}x):       {cappedStatus}");
        sb.AppendLine($"  Cap Hits:            {stats.WinCapHits:N0}");
        sb.AppendLine($"  Max Win Cap Status:  {maxWinStatus}");
        sb.AppendLine();
        
        if (stats.WinCapHits == 0 && stats.TotalSpins < 100_000_000)
        {
            sb.AppendLine("  Note: 500x cap verification typically requires 100M+ spins.");
            sb.AppendLine("        Run a larger simulation to verify cap enforcement.");
            sb.AppendLine();
        }
    }

    private static void AppendFooter(StringBuilder sb)
    {
        sb.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║                              END OF REPORT                                   ║");
        sb.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");
        sb.AppendLine();
    }

    /// <summary>
    /// Writes the report to a file.
    /// </summary>
    public void WriteToFile(string filePath, StatisticsSnapshot stats, TimeSpan elapsed)
    {
        var report = GenerateReport(stats, elapsed);
        File.WriteAllText(filePath, report);
    }
}
