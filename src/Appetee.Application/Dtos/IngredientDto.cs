using Microsoft.AspNetCore.Http;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Appetee.Application.Dtos
{
    public sealed record IngredientDto(
        int id,
        string name
    );

    public sealed record IngredientAdminDetailDto(
     int Id,
     string Name,
     decimal Basis,
     decimal CaloriesKcal,
     decimal Price,
     string? ImageUrl,
     decimal? ProteinG,
     decimal? FatG,
     decimal? CarbsG,
     decimal? SugarG,
     decimal? FiberG,
     decimal? SodiumMg,
     decimal? VitaminCMg,
     decimal? IronMg
    );
}


