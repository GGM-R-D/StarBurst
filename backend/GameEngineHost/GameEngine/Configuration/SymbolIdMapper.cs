using System;
using System.Collections.Generic;
using System.Linq;

namespace GameEngine.Configuration;

/// <summary>
/// Maps symbol codes to numeric IDs based on symbol catalog order.
/// Symbol IDs are 0-based indices in the symbol catalog.
/// This enables GLI-19 compliant symbol matrix representation.
/// </summary>
public sealed class SymbolIdMapper
{
    private readonly IReadOnlyDictionary<string, int> _codeToId;
    private readonly IReadOnlyList<string> _idToCode;

    public SymbolIdMapper(IReadOnlyList<SymbolDefinition> symbolCatalog)
    {
        if (symbolCatalog == null || symbolCatalog.Count == 0)
        {
            throw new ArgumentException("Symbol catalog cannot be null or empty.", nameof(symbolCatalog));
        }

        // Build mapping: symbol code -> ID (0-based index in catalog)
        _codeToId = symbolCatalog
            .Select((symbol, index) => new { symbol.Code, Index = index })
            .ToDictionary(x => x.Code, x => x.Index, StringComparer.OrdinalIgnoreCase);

        // Build reverse mapping: ID -> symbol code
        _idToCode = symbolCatalog.Select(s => s.Code).ToArray();
    }

    /// <summary>
    /// Converts a symbol code to its numeric ID.
    /// </summary>
    public int CodeToId(string symbolCode)
    {
        if (string.IsNullOrWhiteSpace(symbolCode))
        {
            throw new ArgumentException("Symbol code cannot be null or empty.", nameof(symbolCode));
        }

        if (!_codeToId.TryGetValue(symbolCode, out var id))
        {
            throw new ArgumentException($"Unknown symbol code: {symbolCode}", nameof(symbolCode));
        }

        return id;
    }

    /// <summary>
    /// Converts a numeric ID to its symbol code.
    /// </summary>
    public string IdToCode(int symbolId)
    {
        if (symbolId < 0 || symbolId >= _idToCode.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(symbolId), $"Symbol ID {symbolId} is out of range [0, {_idToCode.Count - 1}]");
        }

        return _idToCode[symbolId];
    }

    /// <summary>
    /// Converts a list of symbol codes to symbol IDs.
    /// </summary>
    public IReadOnlyList<int> CodesToIds(IReadOnlyList<string> symbolCodes)
    {
        if (symbolCodes == null)
        {
            return Array.Empty<int>();
        }

        return symbolCodes.Select(CodeToId).ToArray();
    }

    /// <summary>
    /// Converts a list of symbol IDs to symbol codes.
    /// </summary>
    public IReadOnlyList<string> IdsToCodes(IReadOnlyList<int> symbolIds)
    {
        if (symbolIds == null)
        {
            return Array.Empty<string>();
        }

        return symbolIds.Select(IdToCode).ToArray();
    }

    /// <summary>
    /// Gets the total number of symbols in the catalog.
    /// </summary>
    public int SymbolCount => _idToCode.Count;
}

