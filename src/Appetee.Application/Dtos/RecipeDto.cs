namespace Appetee.Application.Dtos
{
    //################ Shared ###########
    public sealed record RecipeNutritionDto(
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal
    );

    //################ Details ###########
    public sealed record RecipeIngredientDetailDto(
        int IngredientId,
        decimal? Quantity,
        string? Unit,
        IngredientAdminDetailDto Ingredient
    );

    //################ Summary ###########
    public sealed record RecipeSummaryDto(
        int Id,
        string Name,
        string? ImageUrl,
        int PrepTimeMinutes,
        int Servings,
        string Difficulty,
        IReadOnlyList<string>? Badges,
        IReadOnlyList<DietDto>? Diets,
        decimal? EstimatedCostPerServing,
        IReadOnlyList<IngredientDto> Ingredients,
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal
    );

    //################ Recipe Details ###########
    public sealed record RecipeDetailDto(
        int Id,
        string Name,
        string? ImageUrl,
        int PrepTimeMinutes,
        int Servings,
        string Difficulty,
        IReadOnlyList<string>? Badges,
        IReadOnlyList<DietDto>? Diets,
        decimal? EstimatedCostPerServing,
        IReadOnlyList<string> Instructions,
        IReadOnlyList<RecipeIngredientDetailDto> Ingredients,
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal
    );
}
