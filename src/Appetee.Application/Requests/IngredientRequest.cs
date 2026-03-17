using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Requests
{
    public sealed record IngredientAdminDetailRequest(
     string Name,
     decimal Basis,
     decimal CaloriesKcal,
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
