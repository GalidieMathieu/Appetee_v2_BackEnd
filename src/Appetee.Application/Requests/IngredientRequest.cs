using System;
using System.Collections.Generic;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Appetee.Application.Requests
{
    public sealed record IngredientAdminDetailRequest(
     string Name,
     decimal Basis,
     string BasisUnit,
     [Required] decimal? CaloriesKcal,
     IFormFile Image,
     [Required] decimal? Price,
     [Required] decimal? ProteinG,
     decimal? FatG,
     [Required] decimal? CarbsG,
     decimal? SugarG,
     decimal? FiberG,
     decimal? SodiumMg,
     decimal? VitaminCMg,
     decimal? IronMg
    );
}
