// Purpose: Verifies F-008 Phase 6 search normalization, safe SQL composition, and cursor coordination.
// Change reason: Supply a neutral F-009 Favorites stub for the expanded recipe query contract.
// Created: 2026-08-26T09:45:15-06:00
// Last updated: 2026-08-29T14:05:58-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using Appetee.Infrastructure.Recipes;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises the bounded search contract without exposing persistence ranks in API DTOs.</summary>
public sealed class F008Phase6SearchTests
{
    [Fact]
    public void SearchNormalizer_CollapsesWhitespaceAndUsesOnlyFirstFourTerms()
    {
        var normalized = RecipeSearchNormalizer.Normalize(
            "  Chicken\t rice  broccoli\r\nbowl ignored  ");

        Assert.Equal("Chicken rice broccoli bowl", normalized.NormalizedSearch);
        Assert.Equal(
            new[] { "Chicken", "rice", "broccoli", "bowl" },
            normalized.EffectiveTerms);
    }

    [Fact]
    public void SearchNormalizer_BlankSearchReturnsBrowseAndOversizedSearchIsRejected()
    {
        var blank = RecipeSearchNormalizer.Normalize(" \t\r\n ");

        Assert.Null(blank.NormalizedSearch);
        Assert.Empty(blank.EffectiveTerms);
        Assert.Throws<ValidationException>(
            () => RecipeSearchNormalizer.Normalize(
                new string('a', RecipeSearchNormalizer.MaxRawLength + 1)));
    }

    [Fact]
    public void SearchSql_RequiresEveryTermAndUsesApprovedDeterministicRelevance()
    {
        var (sql, parameters) = RecipeDiscoverySqlBuilder.Build(
            SearchQuery("chicken rice", ["chicken", "rice"]));

        Assert.Contains("r.name LIKE @Term0Contains", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("i_search.name LIKE @Term0Contains", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.name LIKE @Term1Contains", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("i_search.name LIKE @Term1Contains", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.name = @SearchExact THEN 1000", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("r.name LIKE @SearchStarts", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 500", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 120", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 90", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 60", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 30", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 20", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("THEN 10", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranked.SortRank < @CursorRank", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ranked.Id < @CursorId", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY ranked.SortRank DESC, ranked.Id DESC", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CRC32", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FULLTEXT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOWER(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("chicken", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BrowseSeed", parameters.ParameterNames, StringComparer.Ordinal);
    }

    [Fact]
    public void SearchSql_EscapesLikeMetacharactersAsLiteralValues()
    {
        var (_, parameters) = RecipeDiscoverySqlBuilder.Build(
            SearchQuery(@"50% _ \", ["50%", "_", @"\"]));

        Assert.Equal(@"%50\%%", parameters.Get<string>("Term0Contains"));
        Assert.Equal(@"%\_%", parameters.Get<string>("Term1Contains"));
        Assert.Equal(@"%\\%", parameters.Get<string>("Term2Contains"));
        Assert.Equal(@"50\% \_ \\%", parameters.Get<string>("SearchStarts"));
    }

    [Fact]
    public async Task RecipeService_SearchUsesSeedlessSearchCursorAndPreservesRank()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(590, 7)),
        };
        var service = new RecipeService(queries);

        var firstPage = await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { Search = " chicken   rice ", Limit = 1 },
            CancellationToken.None);
        var firstQuery = Assert.Single(queries.CapturedQueries);
        var cursor = RecipeDiscoveryCursorCodec.DecodeSearch(firstPage.NextCursor!);

        Assert.True(firstQuery.IsSearch);
        Assert.Equal("chicken rice", firstQuery.NormalizedSearch);
        Assert.Equal(new[] { "chicken", "rice" }, firstQuery.EffectiveSearchTerms);
        Assert.Null(firstQuery.BrowseSeed);
        Assert.Equal(590, cursor.Rank);
        Assert.Equal(7, cursor.Id);

        queries.Slice = new RecipeDiscoverySlice([], HasMore: false, Continuation: null);
        await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest
            {
                Search = "chicken rice",
                Cursor = firstPage.NextCursor,
                Limit = 1,
            },
            CancellationToken.None);
        var secondQuery = queries.CapturedQueries[1];

        Assert.Equal(590, secondQuery.AfterRank);
        Assert.Equal(7, secondQuery.AfterRecipeId);
    }

    [Fact]
    public async Task RecipeService_BlankSearchUsesBrowseModeAndRejectsCrossModeCursor()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(1234, 7)),
        };
        var service = new RecipeService(queries);

        var browsePage = await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { Search = "   ", Limit = 1 },
            CancellationToken.None);
        var browseQuery = Assert.Single(queries.CapturedQueries);

        Assert.False(browseQuery.IsSearch);
        Assert.NotNull(browseQuery.BrowseSeed);
        Assert.Throws<ValidationException>(
            () => RecipeDiscoveryCursorCodec.DecodeSearch(browsePage.NextCursor!));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.DiscoverAsync(
                42,
                new RecipeDiscoveryRequest
                {
                    Search = "chicken",
                    Cursor = browsePage.NextCursor,
                    Limit = 1,
                },
                CancellationToken.None));
        Assert.Single(queries.CapturedQueries);
    }

    /// <summary>Builds ranked-search persistence state with neutral later-phase filter values.</summary>
    private static RecipeDiscoveryQuery SearchQuery(
        string normalizedSearch,
        IReadOnlyList<string> terms) =>
        new(
            CurrentUserId: 42,
            PageSize: 20,
            NormalizedSearch: normalizedSearch,
            EffectiveSearchTerms: terms,
            CanonicalBadges: [],
            MaxTotalMinutes: null,
            AllowedDifficulties: [],
            SavedOnly: false,
            BrowseSeed: null,
            AfterRank: null,
            AfterRecipeId: null);

    private static RecipeCardDto Card(int id) =>
        new(
            id,
            $"Recipe {id}",
            CardImageUrl: null,
            TotalTimeMinutes: 30,
            CaloriesPerServing: 400,
            EstimatedCostPerServing: 4,
            Badges: [],
            FeaturedIngredients: [],
            IsSaved: false);

    /// <summary>Captures search query state while stubbing unrelated recipe persistence operations.</summary>
    private sealed class CapturingRecipeQueries : IRecipeQueries
    {
        internal List<RecipeDiscoveryQuery> CapturedQueries { get; } = [];

        internal RecipeDiscoverySlice Slice { get; set; } =
            new([], HasMore: false, Continuation: null);

        public Task<RecipeDiscoverySlice> DiscoverAsync(
            RecipeDiscoveryQuery query,
            CancellationToken ct)
        {
            CapturedQueries.Add(query);
            return Task.FromResult(Slice);
        }

        public Task<RecipePreviewDto?> GetPreviewAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.FromResult<RecipePreviewDto?>(null);

        public Task<IReadOnlyList<RecipeCardDto>> GetFavoritesAsync(
            int currentUserId,
            int? limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecipeCardDto>>([]);

        public Task<bool> SaveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.FromResult(false);

        public Task RemoveFavoriteAsync(
            int currentUserId,
            int recipeId,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<RecipeSummaryDto?> CreateRecipeWithDetailsAsync(
            RecipeDetailRequest request,
            CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeSummaryDto?> UpdateRecipeWithDetailsAsync(
            int id,
            RecipeDetailRequest request,
            CancellationToken ct) =>
            Task.FromResult<RecipeSummaryDto?>(null);

        public Task<RecipeDetailDto?> GetRecipeWithDetailsByIdAsync(
            int id,
            CancellationToken ct) =>
            Task.FromResult<RecipeDetailDto?>(null);
    }
}
