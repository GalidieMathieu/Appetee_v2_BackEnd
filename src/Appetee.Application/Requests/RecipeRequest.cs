using Appetee.Application.Models.Recipes;
using Microsoft.AspNetCore.Http;

namespace Appetee.Application.Requests
{
    public sealed record RecipeIngredientRequest
    {
        public int IngredientId { get; set; }
        public decimal? Quantity { get; set; }
        public string? Unit { get; set; }
        public int? FeaturedOrder { get; set; }
    }

    public sealed record RecipeInstructionStepRequest
    {
        public string? Title { get; set; }
        public string? Instruction { get; set; }
    }

    public sealed record RecipeDetailRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public IFormFile? Image { get; set; }
        public List<RecipeInstructionStepRequest> Instructions { get; set; } = [];
        public int PrepTimeMinutes { get; set; }
        public int CookTimeMinutes { get; set; }
        public int TotalTimeMinutes { get; set; }
        public int Servings { get; set; }
        public RecipeDifficulty? Difficulty { get; set; }
        public List<string> Badges { get; set; } = [];
        public List<int> DietIds { get; set; } = [];
        public List<RecipeIngredientRequest> Ingredients { get; set; } = [];
    }
}
