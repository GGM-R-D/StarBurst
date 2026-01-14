using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GameEngine.Configuration
{
    /// <summary>
    /// Loads, validates, and caches cascading slot configurations as immutable DTOs.
    /// </summary>
    public sealed class GameConfigurationLoader
    {
        private readonly string _configurationDirectory;
        private readonly ConcurrentDictionary<string, Lazy<Task<GameConfiguration>>> _cache = new();
        private readonly JsonSerializerOptions _serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            Converters =
            {
                new MoneyJsonConverter(),
                new JsonStringEnumConverter()
            }
        };

        public GameConfigurationLoader(string configurationDirectory)
        {
            if (string.IsNullOrWhiteSpace(configurationDirectory))
            {
                throw new ArgumentException("Configuration directory must be provided.", nameof(configurationDirectory));
            }

            _configurationDirectory = Path.GetFullPath(configurationDirectory);
        }

        public Task<GameConfiguration> GetConfigurationAsync(string gameId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                throw new ArgumentException("gameId is required.", nameof(gameId));
            }

            var lazyLoader = _cache.GetOrAdd(gameId, id => new Lazy<Task<GameConfiguration>>(() =>
                LoadConfigurationAsync(id, cancellationToken), LazyThreadSafetyMode.ExecutionAndPublication));

            return lazyLoader.Value;
        }

        public void ClearCache(string? gameId = null)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                _cache.Clear();
                return;
            }

            _cache.TryRemove(gameId, out _);
        }

        private async Task<GameConfiguration> LoadConfigurationAsync(string gameId, CancellationToken cancellationToken)
        {
            var fileName = $"{gameId}.json";
            var filePath = Path.Combine(_configurationDirectory, fileName);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Game configuration `{fileName}` was not found.", filePath);
            }

            await using var stream = File.OpenRead(filePath);
            var configuration = await JsonSerializer.DeserializeAsync<GameConfiguration>(stream, _serializerOptions, cancellationToken)
                                ?? throw new InvalidOperationException($"Configuration `{fileName}` could not be parsed.");

            configuration.Validate();
            configuration.SymbolMap = configuration.SymbolCatalog.ToDictionary(symbol => symbol.Sym, StringComparer.OrdinalIgnoreCase);
            configuration.SymbolIdMapper = new SymbolIdMapper(configuration.SymbolCatalog);
            configuration.MultiplierProfiles = BuildMultiplierProfiles(configuration.Multiplier);
            await LoadReelDataAsync(configuration, cancellationToken);

            return configuration;
        }

        private async Task LoadReelDataAsync(GameConfiguration configuration, CancellationToken cancellationToken)
        {
            var reelFile = configuration.Reels.SourceFile;
            var normalized = reelFile.Replace('/', Path.DirectorySeparatorChar);
            var candidate = Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(_configurationDirectory, normalized);
            candidate = Path.GetFullPath(candidate);

            if (!File.Exists(candidate) && !Path.IsPathRooted(normalized))
            {
                var fallback = Path.Combine(_configurationDirectory, Path.GetFileName(normalized));
                fallback = Path.GetFullPath(fallback);
                if (File.Exists(fallback))
                {
                    candidate = fallback;
                }
            }

            if (!File.Exists(candidate))
            {
                throw new FileNotFoundException($"Reel strip file `{reelFile}` is missing.", candidate);
            }

            await using var reelStream = File.OpenRead(candidate);
            var reelSets = await JsonSerializer.DeserializeAsync<Dictionary<string, List<List<string>>>>(reelStream, _serializerOptions, cancellationToken)
                           ?? throw new InvalidOperationException($"Reel strip file `{reelFile}` could not be parsed.");

            IReadOnlyList<IReadOnlyList<string>> Resolve(string key)
            {
                if (!reelSets.TryGetValue(key, out var strips) || strips.Count == 0)
                {
                    throw new InvalidOperationException($"Reel strip `{key}` was not found in `{reelFile}`.");
                }

                return strips.Select(strip => (IReadOnlyList<string>)strip.ToArray()).ToArray();
            }

            configuration.ReelLibrary = new ReelLibrary(
                High: Resolve(configuration.Reels.Keys.High),
                Low: Resolve(configuration.Reels.Keys.Low),
                Buy: Resolve(configuration.Reels.Keys.Buy),
                // Note: FreeSpins optional - not used in Starburst
                FreeSpins: configuration.Reels.Keys.FreeSpins != null ? Resolve(configuration.Reels.Keys.FreeSpins) : Array.Empty<IReadOnlyList<string>>());
        }

        private static MultiplierProfiles BuildMultiplierProfiles(MultiplierConfiguration configuration)
        {
            IReadOnlyList<MultiplierWeight> Build(Dictionary<string, int> source)
            {
                return configuration.Values
                    .Select(value =>
                    {
                        var key = value.ToString(CultureInfo.InvariantCulture);
                        source.TryGetValue(key, out var weight);
                        return new MultiplierWeight(value, weight);
                    })
                    .ToArray();
            }

            return new MultiplierProfiles
            {
                Standard = Build(configuration.Weights.Standard),
                // Note: Ante and FreeSpins profiles not used in Starburst - use empty if not provided
                Ante = configuration.Weights.Ante != null ? Build(configuration.Weights.Ante) : Array.Empty<MultiplierWeight>(),
                FreeSpinsHigh = configuration.Weights.FreeSpinsHigh != null ? Build(configuration.Weights.FreeSpinsHigh) : Array.Empty<MultiplierWeight>(),
                FreeSpinsLow = configuration.Weights.FreeSpinsLow != null ? Build(configuration.Weights.FreeSpinsLow) : Array.Empty<MultiplierWeight>(),
                FreeSpinsSwitchThreshold = configuration.Weights.FreeSpinsSwitchThreshold ?? 250m
            };
        }
    }

    #region DTOs

    public sealed class GameConfiguration
    {
        public required string GameId { get; init; }
        public required string Version { get; init; }
        public required BoardDefinition Board { get; init; }
        public required IReadOnlyList<SymbolDefinition> SymbolCatalog { get; init; }
        public required BetLedger BetLedger { get; init; }
        public required IReadOnlyDictionary<string, BetModeDefinition> BetModes { get; init; }
        public required BuyFeatureConfiguration BuyFeature { get; init; }
        public required MultiplierConfiguration Multiplier { get; init; }
        public required IReadOnlyList<PaytableEntry> Paytable { get; init; }
        public required IReadOnlyList<Money> BetLevels { get; init; }
        public required int DefaultBetIndex { get; init; }
        // Note: Scatter and FreeSpins are optional - not used in Starburst
        public ScatterConfiguration? Scatter { get; init; }
        public FreeSpinConfiguration? FreeSpins { get; init; }
        public required ReelConfiguration Reels { get; init; }
        public required decimal MaxWinMultiplier { get; init; }

        public MultiplierProfiles MultiplierProfiles { get; internal set; } = null!;
        public IReadOnlyDictionary<string, SymbolDefinition> SymbolMap { get; internal set; } = null!;
        public SymbolIdMapper SymbolIdMapper { get; internal set; } = null!;
        public ReelLibrary ReelLibrary { get; internal set; } = null!;

        public void Validate()
        {
            if (Board.Columns <= 0 || Board.Rows <= 0)
            {
                throw new InvalidOperationException("Grid dimensions must be positive.");
            }

            if (SymbolCatalog.Count == 0)
            {
                throw new InvalidOperationException("At least one symbol definition is required.");
            }

            if (BetLevels.Count == 0)
            {
                throw new InvalidOperationException("Bet levels must be defined.");
            }

            foreach (var bet in BetLevels)
            {
                if (!bet.IsValid)
                {
                    throw new InvalidOperationException($"Bet level `{bet}` violates Money precision rules.");
                }
            }

            if (BetModes.Count == 0)
            {
                throw new InvalidOperationException("At least one bet mode must be configured.");
            }

            if (string.IsNullOrWhiteSpace(Reels.SourceFile))
            {
                throw new InvalidOperationException("Reel strip source file must be defined.");
            }
        }
    }

    public sealed record BoardDefinition(int Columns, int Rows);

    public sealed class SymbolDefinition
    {
        public required string Sym { get; init; }
        public required string Code { get; init; }
        public required string DisplayName { get; init; }
        public required SymbolType Type { get; init; }
    }

    public enum SymbolType
    {
        Low,
        High,
        Scatter,
        Multiplier
    }

    public sealed class BetLedger
    {
        public int BaseBetMultiplier { get; init; }
        // Note: AnteBetMultiplier optional - not used in Starburst
        public int AnteBetMultiplier { get; init; } = 1;
        public int BuyFreeSpinsCostMultiplier { get; init; }
    }

    public sealed class BetModeDefinition
    {
        public required ReelWeightDefinition ReelWeights { get; init; }
    }

    public sealed record ReelWeightDefinition(int Low, int High);

    public sealed class BuyFeatureConfiguration
    {
        public required string EnabledBetMode { get; init; }
        public required int CostMultiplier { get; init; }
        public required string EntryReelKey { get; init; }
    }

    public sealed class MultiplierConfiguration
    {
        public required IReadOnlyList<decimal> Values { get; init; }
        public required MultiplierWeightDefinitions Weights { get; init; }
    }

    public sealed class MultiplierWeightDefinitions
    {
        public required Dictionary<string, int> Standard { get; init; }
        // Note: Ante, FreeSpinsHigh, FreeSpinsLow optional - not used in Starburst
        public Dictionary<string, int>? Ante { get; init; }
        public Dictionary<string, int>? FreeSpinsHigh { get; init; }
        public Dictionary<string, int>? FreeSpinsLow { get; init; }
        public decimal? FreeSpinsSwitchThreshold { get; init; }
    }

    public sealed record MultiplierWeight(decimal Value, int Weight);

    public sealed class MultiplierProfiles
    {
        public required IReadOnlyList<MultiplierWeight> Standard { get; init; }
        // Note: Ante, FreeSpinsHigh, FreeSpinsLow optional - not used in Starburst
        // These are set to empty lists if not provided
        public IReadOnlyList<MultiplierWeight> Ante { get; init; } = Array.Empty<MultiplierWeight>();
        public IReadOnlyList<MultiplierWeight> FreeSpinsHigh { get; init; } = Array.Empty<MultiplierWeight>();
        public IReadOnlyList<MultiplierWeight> FreeSpinsLow { get; init; } = Array.Empty<MultiplierWeight>();
        public decimal FreeSpinsSwitchThreshold { get; init; } = 250m; // Default value, not used in Starburst
    }

    public sealed record PaytableEntry(string SymbolCode, IReadOnlyList<MultiplierEntry> Multipliers);

    public sealed record MultiplierEntry(int Count, decimal Multiplier);

    public sealed record ScatterConfiguration(IReadOnlyList<ScatterReward> Rewards);

    public sealed record ScatterReward(int Count, decimal PayoutMultiplier, int FreeSpinsAwarded);

    public sealed record FreeSpinConfiguration(int InitialSpins, int RetriggerSpins, int RetriggerScatterCount);

    public sealed class ReelConfiguration
    {
        public required string SourceFile { get; init; }
        public required ReelKeyMapping Keys { get; init; }
    }

    public sealed class ReelKeyMapping
    {
        public required string High { get; init; }
        public required string Low { get; init; }
        public required string Buy { get; init; }
        // Note: FreeSpins optional - not used in Starburst
        public string? FreeSpins { get; init; }
    }

    public sealed class ReelLibrary
    {
        public ReelLibrary(
            IReadOnlyList<IReadOnlyList<string>> High,
            IReadOnlyList<IReadOnlyList<string>> Low,
            IReadOnlyList<IReadOnlyList<string>> Buy,
            IReadOnlyList<IReadOnlyList<string>> FreeSpins)
        {
            this.High = High;
            this.Low = Low;
            this.Buy = Buy;
            this.FreeSpins = FreeSpins ?? Array.Empty<IReadOnlyList<string>>();
        }

        public IReadOnlyList<IReadOnlyList<string>> High { get; }
        public IReadOnlyList<IReadOnlyList<string>> Low { get; }
        public IReadOnlyList<IReadOnlyList<string>> Buy { get; }
        public IReadOnlyList<IReadOnlyList<string>> FreeSpins { get; } = Array.Empty<IReadOnlyList<string>>();
    }

    /// <summary>
    /// Money DTO abiding by decimal(20,2) precision with dot separator.
    /// </summary>
    public readonly struct Money : IEquatable<Money>, IComparable<Money>
    {
        private const int Scale = 2;
        private const int Precision = 20;

        public static Money Zero => new(0m);

        public decimal Amount { get; }

        public bool IsValid => HasValidPrecision(Amount);

        public Money(decimal amount)
        {
            if (!HasValidPrecision(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Money must adhere to decimal(20,2).");
            }

            Amount = decimal.Round(amount, Scale, MidpointRounding.ToEven);
        }

        public override string ToString() => Amount.ToString($"F{Scale}", CultureInfo.InvariantCulture);

        public static implicit operator decimal(Money money) => money.Amount;
        public static explicit operator Money(decimal amount) => new(amount);

        public static Money operator +(Money left, Money right) => new(left.Amount + right.Amount);
        public static Money operator *(Money left, decimal multiplier) => new(left.Amount * multiplier);
        public static Money FromBet(decimal bet, decimal multiplier) => new(decimal.Round(bet * multiplier, Scale, MidpointRounding.ToEven));

        public int CompareTo(Money other) => Amount.CompareTo(other.Amount);
        public bool Equals(Money other) => Amount == other.Amount;
        public override bool Equals(object? obj) => obj is Money other && Equals(other);
        public override int GetHashCode() => Amount.GetHashCode();

        private static bool HasValidPrecision(decimal value)
        {
            var bits = decimal.GetBits(value);
            var scale = (bits[3] >> 16) & 0x7F;
            var isPrecisionValid = decimal.Truncate(value).ToString(CultureInfo.InvariantCulture).Replace("-", "").Length <= Precision - Scale;
            return scale <= Scale && isPrecisionValid;
        }
    }

    #endregion
}

