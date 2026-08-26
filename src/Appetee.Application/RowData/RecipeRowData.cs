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
