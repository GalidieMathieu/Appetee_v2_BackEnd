using Appetee.Application.Models.Recipes;
using Microsoft.AspNetCore.Http;

namespace Appetee.Application.Requests
{
    public sealed record RecipeIngredientRequest
    {
        public int IngredientId { get; set; }
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
    }

    public sealed record RecipeDetailRequest
    {
        public string Name { get; set; } = string.Empty;
        public IFormFile? Image { get; set; }
        public decimal CaloriesTotal { get; set; }
        public decimal ProteinTotal { get; set; }
        public decimal CarbsTotal { get; set; }
        public string Instructions { get; set; } = string.Empty;
        public int PrepTimeMinutes { get; set; }
        public int Servings { get; set; }
        public RecipeDifficulty? Difficulty { get; set; }
        public List<string> Badges { get; set; } = [];
        public List<int> DietIds { get; set; } = [];
        public decimal? EstimatedCostPerServing { get; set; }
        public List<RecipeIngredientRequest> Ingredients { get; set; } = [];
    }
}
