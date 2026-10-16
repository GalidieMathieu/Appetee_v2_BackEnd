using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B01MealPlanProjectionTests
{
    [Fact]
    public void SqlBuilder_WidensOnlyTheMealPlanProjection()
    {
        var query = Query();

        var (ordinarySql, _) = RecipeDiscoverySqlBuilder.Build(query);
        var (mealPlanSql, _) = RecipeDiscoverySqlBuilder.Build(
            query,
            includeMealPlanProjection: true);

        Assert.DoesNotContain("AS Description", ordinarySql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AS CarbsPerServing", ordinarySql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AS FatPerServing", ordinarySql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.description", mealPlanSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.protein_per_serving", mealPlanSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROUND(r.carbs_total / r.servings, 2)", mealPlanSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ROUND(r.fat_total / r.servings, 2)", mealPlanSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preview", mealPlanSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Service_ReusesNormalizationRankingAndCursorForMealPlan()
    {
        var queries = new CapturingQueries
        {
            DiscoverySlice = new RecipeDiscoverySlice(
                [BaseCard(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(500, 7)),
            MealPlanSlice = new RecipeMealPlanSlice(
                [AdvanceCard(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(500, 7)),
        };
        var service = new RecipeService(queries);
        var request = new RecipeDiscoveryRequest
        {
            Search = " chicken   rice ",
            IngredientIds = [3, 1, 3],
            RequireAllIngredients = false,
            Badges = ["Meal Prep", "High Protein"],
            MaxTotalMinutes = 60,
            MaxDifficulty = RecipeDifficulty.Medium,
            SavedOnly = true,
            Limit = 1,
        };

        var ordinary = await service.DiscoverAsync(12, request, CancellationToken.None);
        var mealPlan = await service.DiscoverForMealPlanAsync(12, request, CancellationToken.None);

        Assert.NotNull(queries.DiscoveryQuery);
        Assert.NotNull(queries.MealPlanQuery);
        Assert.Equal(queries.DiscoveryQuery!.CurrentUserId, queries.MealPlanQuery!.CurrentUserId);
        Assert.Equal(queries.DiscoveryQuery.PageSize, queries.MealPlanQuery.PageSize);
        Assert.Equal(queries.DiscoveryQuery.NormalizedSearch, queries.MealPlanQuery.NormalizedSearch);
        Assert.Equal(queries.DiscoveryQuery.EffectiveSearchTerms, queries.MealPlanQuery.EffectiveSearchTerms);
        Assert.Equal(queries.DiscoveryQuery.IngredientIds, queries.MealPlanQuery.IngredientIds);
        Assert.Equal(queries.DiscoveryQuery.RequireAllIngredients, queries.MealPlanQuery.RequireAllIngredients);
        Assert.Equal(queries.DiscoveryQuery.CanonicalBadges, queries.MealPlanQuery.CanonicalBadges);
        Assert.Equal(queries.DiscoveryQuery.AllowedDifficulties, queries.MealPlanQuery.AllowedDifficulties);
        Assert.Equal(queries.DiscoveryQuery.SavedOnly, queries.MealPlanQuery.SavedOnly);
        Assert.Equal(ordinary.NextCursor, mealPlan.NextCursor);
        Assert.Equal(ordinary.HasMore, mealPlan.HasMore);
        Assert.Equal(ordinary.Items.Select(item => item.Id), mealPlan.Items.Select(item => item.Id));
    }

    private static RecipeDiscoveryQuery Query() =>
        new(
            CurrentUserId: 12,
            PageSize: 20,
            NormalizedSearch: "chicken",
            EffectiveSearchTerms: ["chicken"],
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: null,
            AfterRank: null,
            AfterRecipeId: null);

    private static RecipeCardDto BaseCard(int id) =>
        new(id, $"Recipe {id}", null, 30, 400m, 3m, [], [], false);

    private static RecipeCardAdvance AdvanceCard(int id) =>
        new(
            id,
            $"Recipe {id}",
            null,
            30,
            400m,
            3m,
            [],
            [],
            false,
            "Description",
            30m,
            50m,
            null);

    private sealed class CapturingQueries : IRecipeQueries
    {
        internal RecipeDiscoverySlice DiscoverySlice { get; init; } = new([], false, null);
        internal RecipeMealPlanSlice MealPlanSlice { get; init; } = new([], false, null);
        internal RecipeDiscoveryQuery? DiscoveryQuery { get; private set; }
        internal RecipeDiscoveryQuery? MealPlanQuery { get; private set; }

        public Task<RecipeDiscoverySlice> DiscoverAsync(RecipeDiscoveryQuery query, CancellationToken ct)
        {
            DiscoveryQuery = query;
            return Task.FromResult(DiscoverySlice);
        }

        public Task<RecipeMealPlanSlice> DiscoverForMealPlanAsync(RecipeDiscoveryQuery query, CancellationToken ct)
        {
            MealPlanQuery = query;
            return Task.FromResult(MealPlanSlice);
        }

        public Task<RecipePreviewDto?> GetPreviewAsync(int currentUserId, int recipeId, CancellationToken ct) =>
            throw new InvalidOperationException("Meal Plan discovery must not load Preview rows.");

        public Task<RecipeCookingViewDto?> GetCookingViewAsync(int currentUserId, int recipeId, CancellationToken ct) =>
            Task.FromResult<RecipeCookingViewDto?>(null);

        public Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(int currentUserId, int? limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecipeCardDto>>([]);

        public Task<bool> SaveFavoriteAsync(int currentUserId, int recipeId, CancellationToken ct) =>
            Task.FromResult(false);

        public Task RemoveFavoriteAsync(int currentUserId, int recipeId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(RecipeDetailRequest request, CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(int id, RecipeDetailRequest request, CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(int id, CancellationToken ct) =>
            Task.FromResult<RecipeDetailDto?>(null);
    }
}
