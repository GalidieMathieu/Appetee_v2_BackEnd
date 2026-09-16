// Purpose: Defines persistence projections used by recipe discovery, Quick Preview, Cooking View, details, and authoring.
// Change reason: Add bounded F-010 Phase 1 Cooking View recipe and ingredient row projections.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-31T18:01:27-06:00

namespace Appetee.Application.RowData;

/// <summary>Materializes one compatibility-filtered Recipe Discovery candidate row.</summary>
public sealed record RecipeDiscoveryRowData(
    int Id,
    string Name,
    string? CardImageBlobName,
    int TotalTimeMinutes,
    decimal CaloriesPerServing,
    decimal EstimatedCostPerServing,
    long IsSaved,
    long SortRank
);

public sealed record RecipeBadgeRowData(
    int RecipeId,
    string Badge
);

/// <summary>Materializes one featured ingredient for bounded card hydration.</summary>
public sealed record RecipeFeaturedIngredientRowData(
    int RecipeId,
    int Id,
    string Name,
    byte FeaturedOrder
);

/// <summary>Materializes the compatibility-filtered recipe portion of one Quick Preview.</summary>
public sealed record RecipePreviewRowData(
    int Id,
    string Name,
    string Description,
    string? PreviewImageBlobName,
    int TotalTimeMinutes,
    decimal CaloriesPerServing,
    decimal ProteinPerServing,
    decimal EstimatedCostPerServing,
    long IsSaved
);

/// <summary>Materializes one lightweight Quick Preview ingredient in authored display order.</summary>
public sealed record RecipePreviewIngredientRowData(
    int Id,
    string Name
);

/// <summary>Materializes the compatibility-filtered base recipe portion of one Cooking View.</summary>
public sealed record RecipeCookingRowData(
    int Id,
    string Name,
    string? ImageBlobName,
    string Description,
    int TotalTimeMinutes,
    int BaseServings,
    decimal CaloriesTotal,
    decimal ProteinTotal,
    decimal CarbsTotal,
    string Instructions
);

/// <summary>Materializes one Cooking View ingredient in authored display order.</summary>
public sealed record RecipeCookingIngredientRowData(
    int Id,
    string Name,
    decimal Quantity,
    string Unit,
    ushort DisplayOrder
);

public sealed record RecipeDetailRowData(
    int Id,
    string Name,
    string Description,
    string? PreviewImageBlobName,
    string Instructions,
    int PrepTimeMinutes,
    int CookTimeMinutes,
    int TotalTimeMinutes,
    int Servings,
    string Difficulty,
    decimal? EstimatedCostPerServing,
    decimal CaloriesTotal,
    decimal ProteinTotal,
    decimal CarbsTotal,
    decimal CaloriesPerServing,
    decimal ProteinPerServing
);

public sealed record RecipeImageBlobRowData(
    int Id,
    string? PreviewImageBlobName
);

public sealed record RecipeIngredientDetailRowData(
    int IngredientId,
    decimal? Quantity,
    string? Unit,
    ushort DisplayOrder,
    byte? FeaturedOrder,
    int Id,
    string Name,
    decimal Basis,
    string BasisUnit,
    decimal CaloriesKcal,
    decimal Price,
    string? ImageBlobName,
    decimal ProteinG,
    decimal? FatG,
    decimal CarbsG,
    decimal? SugarG,
    decimal? FiberG,
    decimal? SodiumMg,
    decimal? VitaminCMg,
    decimal? IronMg
);
