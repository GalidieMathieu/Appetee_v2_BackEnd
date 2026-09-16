// Purpose: Defines public recipe authoring, discovery, detail, Quick Preview, and Cooking View response contracts.
// Change reason: Add F-010 Cooking contracts and centralize fields shared by Preview and Cooking read models.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-31T18:28:42-06:00

namespace Appetee.Application.Dtos
{
    //################ Shared ###########
    public sealed record RecipeNutritionDto(
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal
    );

    /// <summary>Defines the stable recipe fields shared by user-facing Preview and Cooking read models.</summary>
    public abstract record RecipeReadDto(
        int Id,
        string Name,
        string Description,
        int TotalTimeMinutes
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
    ) : RecipeReadDto(Id, Name, Description, TotalTimeMinutes);

    //################ Cooking ###########
    /// <summary>Provides one scalable ingredient in authored Cooking Mode display order.</summary>
    public sealed record RecipeCookingIngredientDto(
        int Id,
        string Name,
        decimal Quantity,
        string Unit,
        int DisplayOrder
    );

    /// <summary>Provides one complete Cooking Mode instruction with explicit one-based order.</summary>
    public sealed record RecipeCookingStepDto(
        int Order,
        string Title,
        string Instruction
    );

    /// <summary>Provides the complete current-user-compatible base recipe required by Cooking Mode.</summary>
    public sealed record RecipeCookingViewDto(
        int Id,
        string Name,
        string? ImageUrl,
        string Description,
        int TotalTimeMinutes,
        int BaseServings,
        decimal CaloriesTotal,
        decimal ProteinTotal,
        decimal CarbsTotal,
        IReadOnlyList<string> Badges,
        IReadOnlyList<RecipeCookingIngredientDto> Ingredients,
        IReadOnlyList<RecipeCookingStepDto> Steps
    ) : RecipeReadDto(Id, Name, Description, TotalTimeMinutes);

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
