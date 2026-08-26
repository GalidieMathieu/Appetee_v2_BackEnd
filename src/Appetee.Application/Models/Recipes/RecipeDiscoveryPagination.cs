// Purpose: Carries normalized Recipe Discovery pagination state across application and persistence layers.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-25T23:50:21-06:00

using Appetee.Application.Dtos;

namespace Appetee.Application.Models.Recipes;

/// <summary>Provides validated seeded browse state to the discovery query.</summary>
public sealed record RecipeDiscoveryQuery(
    int CurrentUserId,
    int PageSize,
    int BrowseSeed,
    long? AfterRank,
    int? AfterRecipeId
);

/// <summary>Identifies the last returned candidate used to continue a stable page chain.</summary>
public sealed record RecipeDiscoveryContinuation(
    long Rank,
    int RecipeId
);

/// <summary>Returns one bounded candidate slice and its private continuation state.</summary>
public sealed record RecipeDiscoverySlice(
    IReadOnlyList<RecipeCardDto> Items,
    bool HasMore,
    RecipeDiscoveryContinuation? Continuation
);

/// <summary>Represents the normalized criteria bound into an opaque discovery cursor.</summary>
internal sealed record RecipeDiscoveryCriteria(
    int CurrentUserId,
    IReadOnlyList<string> EffectiveSearchTerms,
    IReadOnlyList<int> IngredientIds,
    bool RequireAllIngredients,
    IReadOnlyList<string> CanonicalBadges,
    int? MaxTotalMinutes,
    RecipeDifficulty? MaxDifficulty,
    bool SavedOnly,
    int PageSize
);

/// <summary>Defines version one of the private seeded browse cursor payload.</summary>
internal sealed record BrowseCursorV1(
    int V,
    string Mode,
    int Seed,
    long Rank,
    int Id,
    string Criteria
);

/// <summary>Defines version one of the private ranked search cursor payload reserved for later discovery phases.</summary>
internal sealed record SearchCursorV1(
    int V,
    string Mode,
    long Rank,
    int Id,
    string Criteria
);
