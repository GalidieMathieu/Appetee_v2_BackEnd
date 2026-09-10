// Purpose: Verifies the centralized recipe SQL contracts and bounded discovery query shape.
// Change reason: Supply neutral Phase 8 filter state to the expanded discovery query contract.
// Created: Existing file; original timestamp was not recorded.
// Last updated: 2026-08-27T10:48:05-06:00

using System.Text.RegularExpressions;
using Appetee.Application.Models.Recipes;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

public sealed class RecipeSqlTests
{
    [Fact]
    public void DiscoveryCandidates_UseCardImageWithMainImageFallback()
    {
        var sql = BuildBrowseSql();

        Assert.Contains(
            "COALESCE(r.card_image_blob_name, r.image_blob_name) AS CardImageBlobName",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoveryCandidates_AreBoundedAndAlwaysApplyCurrentUserCompatibility()
    {
        var sql = BuildBrowseSql();

        Assert.Contains("LIMIT @TakePlusOne", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("fr.user_id = @CurrentUserId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM user_diets", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FROM diet_recipes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JOIN user_ingredient_restrictions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("protein_per_serving", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoveryCandidates_UseStableSeededKeysetOrdering()
    {
        var sql = BuildBrowseSql();

        Assert.Contains("CRC32(CONCAT(@BrowseSeed, ':', r.id))", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranked.SortRank > @CursorRank", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranked.SortRank = @CursorRank", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranked.Id > @CursorId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ranked.SortRank ASC, ranked.Id ASC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ORDER BY RAND", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OFFSET", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("COUNT(*)", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiscoveryCardHydration_IsOneBoundedTwoResultCommand()
    {
        var sql = RecipeSql.HydrateDiscoveryCards;

        Assert.Equal(2, Regex.Matches(sql, @"\bSELECT\b", RegexOptions.IgnoreCase).Count);
        Assert.Equal(2, Regex.Matches(sql, @"IN\s+@RecipeIds", RegexOptions.IgnoreCase).Count);
        Assert.Contains("featured_order IS NOT NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("diet_recipes", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("instructions", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TemporaryWholeCatalogSql_IsAbsent()
    {
        var getAllField = typeof(RecipeSql).GetField(
            "GetAll",
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);

        Assert.Null(getAllField);
    }

    [Fact]
    public void RecipeDetail_UsesMainImage()
    {
        Assert.Contains(
            "r.image_blob_name            AS PreviewImageBlobName",
            RecipeSql.GetWithDetailsById,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("card_image_blob_name", RecipeSql.GetWithDetailsById, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeUpdate_ClearsCardImageOnlyWhenMainImageIsReplaced()
    {
        Assert.Contains(
            "card_image_blob_name = CASE WHEN @ClearCardImage THEN NULL ELSE card_image_blob_name END",
            RecipeSql.UpdateRecipe,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeWrites_PersistF008ReadinessFields()
    {
        foreach (var field in new[]
        {
            "description",
            "cook_time_minutes",
            "total_time_minutes",
            "calories_per_serving",
            "protein_per_serving",
        })
        {
            Assert.Contains(field, RecipeSql.CreateRecipe, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(field, RecipeSql.UpdateRecipe, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RecipeDetail_LoadsIngredientsInPersistedDisplayOrder()
    {
        Assert.Contains("ri.display_order", RecipeSql.GetWithDetailsById, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ri.featured_order", RecipeSql.GetWithDetailsById, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ri.display_order", RecipeSql.GetWithDetailsById, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IngredientCalculationDataQuery_IsSingleSetBasedStatement()
    {
        var sql = RecipeSql.GetIngredientCalculationDataByIds;

        Assert.Single(Regex.Matches(sql, @"\bSELECT\b", RegexOptions.IgnoreCase).Cast<Match>());
        Assert.Contains("WHERE i.id IN @Ids", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LEFT JOIN ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Builds the neutral browse query shape used by centralized SQL regression assertions.</summary>
    private static string BuildBrowseSql() =>
        RecipeDiscoverySqlBuilder.Build(
            new RecipeDiscoveryQuery(
                CurrentUserId: 42,
                PageSize: 20,
                NormalizedSearch: null,
                EffectiveSearchTerms: [],
                CanonicalBadges: [],
                MaxTotalMinutes: null,
                AllowedDifficulties: [],
                SavedOnly: false,
                BrowseSeed: 1234,
                AfterRank: null,
                AfterRecipeId: null))
        .Sql;
}
