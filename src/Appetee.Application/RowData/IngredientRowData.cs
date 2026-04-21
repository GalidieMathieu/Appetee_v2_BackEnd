namespace Appetee.Application.RowData;

public sealed record IngredientRowData(
    int Id,
    string Name,
    string? ImageBlobName
);


public sealed record IngredientRowdataAdmin(
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