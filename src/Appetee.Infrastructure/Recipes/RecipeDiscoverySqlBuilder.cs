// Purpose: Binds Recipe Discovery parameters and selects SQL owned centrally by RecipeSql.
// Change reason: Bind F-008 Phase 10 ingredient IDs/count and select the ALL/ANY SQL shape.
// Created: 2026-08-26T09:45:15-06:00
// Last updated: 2026-08-28T08:53:55-06:00

using Appetee.Application.Models.Recipes;
using Dapper;

namespace Appetee.Infrastructure.Recipes;

/// <summary>Selects discovery mode and binds request values without owning SQL text.</summary>
internal static class RecipeDiscoverySqlBuilder
{
    /// <summary>Selects the discovery SQL shape and binds only parameters used by that shape.</summary>
    internal static (string Sql, DynamicParameters Parameters) Build(
        RecipeDiscoveryQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.EffectiveSearchTerms.Count > RecipeSearchLimits.MaxEffectiveTerms)
            throw new InvalidOperationException("Recipe discovery received too many effective search terms.");

        var parameters = new DynamicParameters();
        parameters.Add("CurrentUserId", query.CurrentUserId);
        parameters.Add("HasCursor", query.AfterRank.HasValue);
        parameters.Add("CursorRank", query.AfterRank);
        parameters.Add("CursorId", query.AfterRecipeId);
        parameters.Add("TakePlusOne", query.PageSize + 1);

        if (query.IngredientIds.Count > 0)
        {
            parameters.Add("IngredientIds", query.IngredientIds.ToArray());
            parameters.Add("IngredientCount", query.IngredientIds.Count);
        }

        if (query.CanonicalBadges.Count > 0)
        {
            parameters.Add("Badges", query.CanonicalBadges.ToArray());
            parameters.Add("BadgeCount", query.CanonicalBadges.Count);
        }

        if (query.MaxTotalMinutes.HasValue)
            parameters.Add("MaxTotalMinutes", query.MaxTotalMinutes.Value);

        if (query.AllowedDifficulties.Count > 0)
            parameters.Add("AllowedDifficulties", query.AllowedDifficulties.ToArray());

        if (query.IsSearch)
        {
            if (string.IsNullOrWhiteSpace(query.NormalizedSearch))
                throw new InvalidOperationException("Recipe discovery search text is unavailable.");

            var escapedSearch = EscapeLikePattern(query.NormalizedSearch);
            parameters.Add("SearchExact", query.NormalizedSearch);
            parameters.Add("SearchStarts", $"{escapedSearch}%");

            for (var index = 0; index < query.EffectiveSearchTerms.Count; index++)
            {
                var term = query.EffectiveSearchTerms[index];
                var escapedTerm = EscapeLikePattern(term);
                parameters.Add($"Term{index}Exact", term);
                parameters.Add($"Term{index}Starts", $"{escapedTerm}%");
                parameters.Add($"Term{index}Contains", $"%{escapedTerm}%");
            }
        }
        else
        {
            if (!query.BrowseSeed.HasValue)
                throw new InvalidOperationException("Recipe discovery browse seed is unavailable.");

            parameters.Add("BrowseSeed", query.BrowseSeed.Value);
        }

        var sql = RecipeSql.BuildDiscoveryCandidates(
            query.IsSearch,
            query.EffectiveSearchTerms.Count,
            query.SavedOnly,
            query.CanonicalBadges.Count > 0,
            query.MaxTotalMinutes.HasValue,
            query.AllowedDifficulties.Count > 0,
            query.IngredientIds.Count > 0,
            query.RequireAllIngredients);

        return (sql, parameters);
    }

    /// <summary>Escapes LIKE metacharacters so user search text remains literal parameter data.</summary>
    internal static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>Keeps persistence validation independent from application-internal helper visibility.</summary>
    private static class RecipeSearchLimits
    {
        internal const int MaxEffectiveTerms = 4;
    }
}
