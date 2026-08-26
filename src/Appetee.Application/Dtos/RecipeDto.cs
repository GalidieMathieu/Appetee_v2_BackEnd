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
        int DisplayOrder,
        int? FeaturedOrder,
        IngredientAdminDetailDto Ingredient
    );

    public sealed record RecipeInstructionStepDto(
        string Title,
        string Instruction
    );

    //################ Summary ###########
    public sealed record RecipeSummaryDto(
        int Id,
        string Name,
        string Description,
        string? PreviewImageUrl,
        int PrepTimeMinutes,
        int CookTimeMinutes,
        int TotalTimeMinutes,
        int Servings,
        string Difficulty,
        IReadOnlyList<string>? Badges,
        IReadOnlyList<DietDto>? Diets,
        decimal? EstimatedCostPerServing,
        IReadOnlyList<IngredientDto> Ingredients,
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        decimal CaloriesPerServing,
        decimal ProteinPerServing
    );

    //################ Discovery ###########
    /// <summary>Identifies an ingredient selected for compact Recipe Card display.</summary>
    public sealed record FeaturedIngredientDto(
        int Id,
        string Name,
        int FeaturedOrder
    );

    /// <summary>Provides the lightweight, current-user-aware Recipe Card read contract.</summary>
    public sealed record RecipeCardDto(
        int Id,
        string Name,
        string? CardImageUrl,
        int TotalTimeMinutes,
        decimal CaloriesPerServing,
        decimal EstimatedCostPerServing,
        IReadOnlyList<string> Badges,
        IReadOnlyList<FeaturedIngredientDto> FeaturedIngredients,
        bool IsSaved
    );

    /// <summary>Provides one bounded page of Recipe Discovery cards.</summary>
    public sealed record RecipeDiscoveryPageDto(
        IReadOnlyList<RecipeCardDto> Items,
        string? NextCursor,
        bool HasMore
    );

    //################ Recipe Details ###########
    public sealed record RecipeDetailDto(
        int Id,
        string Name,
        string Description,
        string? PreviewImageUrl,
        int PrepTimeMinutes,
        int CookTimeMinutes,
        int TotalTimeMinutes,
        int Servings,
        string Difficulty,
        IReadOnlyList<string>? Badges,
        IReadOnlyList<DietDto>? Diets,
        decimal? EstimatedCostPerServing,
        IReadOnlyList<RecipeInstructionStepDto> Instructions,
        IReadOnlyList<RecipeIngredientDetailDto> Ingredients,
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        decimal CaloriesPerServing,
        decimal ProteinPerServing
    );
}
