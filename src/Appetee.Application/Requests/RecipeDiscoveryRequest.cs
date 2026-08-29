// Purpose: Binds the public Recipe Discovery filters, search, saved-membership, and pagination inputs.
// Change reason: Add F-008 Phase 10 repeated ingredient IDs and ALL/ANY selection mode.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-28T08:53:55-06:00

using Appetee.Application.Models.Recipes;

namespace Appetee.Application.Requests;

/// <summary>Provides normalized public discovery criteria with opaque bounded pagination.</summary>
public sealed record RecipeDiscoveryRequest
{
    public string? Search { get; init; }

    public IReadOnlyList<int> IngredientIds { get; init; } = [];

    public bool RequireAllIngredients { get; init; } = true;

    public IReadOnlyList<string> Badges { get; init; } = [];

    public int? MaxTotalMinutes { get; init; }

    public RecipeDifficulty? MaxDifficulty { get; init; }

    public bool SavedOnly { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
