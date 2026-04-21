using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace Appetee.Application.Requests
{
    public sealed record IngredientAdminDetailRequest(
     string Name,
     decimal Basis,
     decimal CaloriesKcal,
     IFormFile Image,
     decimal Price,
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
