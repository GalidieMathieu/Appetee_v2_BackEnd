// Purpose: Normalizes and bounds the public Recipe Discovery text-search input.
// Created: 2026-08-26T09:45:15-06:00
// Last updated: 2026-08-26T09:45:15-06:00

using Appetee.Application.utils;

namespace Appetee.Application.Services.Recipes;

/// <summary>Produces the effective phrase and first four non-empty terms used by discovery search.</summary>
internal static class RecipeSearchNormalizer
{
    internal const int MaxRawLength = 100;
    internal const int MaxEffectiveTerms = 4;

    internal static (string? NormalizedSearch, IReadOnlyList<string> EffectiveTerms) Normalize(
        string? search)
    {
        if (search is null)
            return (null, []);

        if (search.Length > MaxRawLength)
            throw new ValidationException("search cannot exceed 100 characters.");

        var terms = search
            .Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(MaxEffectiveTerms)
            .ToArray();

        if (terms.Length == 0)
            return (null, []);

        return (string.Join(' ', terms), terms);
    }
}
