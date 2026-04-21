namespace Appetee.Application.RowData;

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
    decimal CaloriesKcal,
    decimal Price,
    string? ImageBlobName,
    decimal? ProteinG,
    decimal? FatG,
    decimal? CarbsG,
    decimal? SugarG,
    decimal? FiberG,
    decimal? SodiumMg,
    decimal? VitaminCMg,
    decimal? IronMg
);
