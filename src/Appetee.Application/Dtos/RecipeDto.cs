// Purpose: Defines public recipe authoring, discovery, detail, and Quick Preview response contracts.
// Change reason: Add the dedicated lightweight F-008 Phase 12 Recipe Preview contract.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-28T11:50:10-06:00

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

    /// <summary>Identifies one ingredient in authored display order without exposing quantities or nutrition.</summary>
    public sealed record RecipePreviewIngredientDto(
        int Id,
        string Name
    );

    /// <summary>Provides the lightweight, current-user-aware Recipe Quick Preview read contract.</summary>
    public sealed record RecipePreviewDto(
        int Id,
        string Name,
        string Description,
        string? PreviewImageUrl,
        int TotalTimeMinutes,
        decimal CaloriesPerServing,
        decimal ProteinPerServing,
        decimal EstimatedCostPerServing,
        IReadOnlyList<string> Badges,
        IReadOnlyList<RecipePreviewIngredientDto> Ingredients,
        bool IsSaved
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
