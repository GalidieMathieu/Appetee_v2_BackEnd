namespace Appetee.Application.RowData;

public sealed record RecipeSummaryRowData(
    int Id,
    string Name,
    string? ImageBlobName,
    int PrepTimeMinutes,
    int Servings,
    string Difficulty,
    decimal? EstimatedCostPerServing,
    decimal CaloriesTotal,
    decimal ProteinTotal,
    decimal CarbsTotal
);

public sealed record RecipeDietRowData(
    int RecipeId,
    int Id,
    string Name
);

public sealed record RecipeBadgeRowData(
    int RecipeId,
    string Badge
);

public sealed record RecipeIngredientRowData(
    int RecipeId,
    int Id,
    string Name
);

public sealed record RecipeDetailRowData(
    int Id,
    string Name,
    string? ImageBlobName,
    string Instructions,
    int PrepTimeMinutes,
    int Servings,
    string Difficulty,
    decimal? EstimatedCostPerServing,
    decimal CaloriesTotal,
    decimal ProteinTotal,
    decimal CarbsTotal
);

public sealed record RecipeImageBlobRowData(
    int Id,
    string? ImageBlobName
);

public sealed record RecipeIngredientDetailRowData(
    int IngredientId,
    decimal? Quantity,
    string? Unit,
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
