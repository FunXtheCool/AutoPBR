using System.Collections.Concurrent;
using System.Text.Json;
using AutoPBR.Contracts.Ml;

namespace AutoPBR.Core.Embeddings;

/// <summary>
/// Dictionary provider backed by FreeDictionaryAPI (https://freedictionaryapi.com).
/// </summary>
public sealed class FreeDictionaryDefinitionProvider : IDictionaryDefinitionProvider
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("https://freedictionaryapi.com/")
    };

    private readonly ConcurrentDictionary<string, IReadOnlyList<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetDefinitions(
        string languageCode,
        string lookupTerm,
        TimeSpan timeout,
        out string? diagnostic)
    {
        diagnostic = null;
        var lang = string.IsNullOrWhiteSpace(languageCode) ? "en" : languageCode.Trim().ToLowerInvariant();
        var term = lookupTerm.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            return [];
        }

        var cacheKey = $"{lang}:{term}".ToLowerInvariant();
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var escapedTerm = Uri.EscapeDataString(term);
            var requestUri = $"api/v1/entries/{lang}/{escapedTerm}";
            using var response = Client.GetAsync(requestUri, cts.Token).GetAwaiter().GetResult();

            if ((int)response.StatusCode == 429)
            {
                diagnostic = "dictionary-rate-limited-429";
                return [];
            }

            if (!response.IsSuccessStatusCode)
            {
                diagnostic = $"dictionary-http-{(int)response.StatusCode}";
                return [];
            }

            var json = response.Content.ReadAsStringAsync(cts.Token).GetAwaiter().GetResult();
            var defs = ParseDefinitions(json);
            _cache[cacheKey] = defs;
            return defs;
        }
        catch (OperationCanceledException)
        {
            diagnostic = "dictionary-timeout";
            return [];
        }
        catch (Exception ex)
        {
            diagnostic = $"dictionary-error-{ex.GetType().Name}";
            return [];
        }
    }

    internal static IReadOnlyList<string> ParseDefinitions(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var list = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("senses", out var senses) || senses.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var sense in senses.EnumerateArray())
                {
                    if (sense.TryGetProperty("definition", out var def) && def.ValueKind == JsonValueKind.String)
                    {
                        var text = def.GetString()?.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            list.Add(text);
                        }
                    }
                }
            }
        }
        catch
        {
            return [];
        }

        return list
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
    }
}
