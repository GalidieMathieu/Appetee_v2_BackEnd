// Purpose: Verifies F-008 Phase 4 cursor encoding, criteria binding, and service coordination.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-25T23:50:21-06:00

using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Recipes;
using Appetee.Application.Requests;
using Appetee.Application.Services.Recipes;
using Appetee.Application.utils;
using System.Text;

namespace Appetee.Api.Tests.Unit;

/// <summary>Exercises private cursor invariants without exposing cursor payloads through the API.</summary>
public sealed class F008Phase4CursorTests
{
    private const string ValidCriteria = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    [Fact]
    public void CursorCodec_RoundTripsBrowseAndSearchPayloads()
    {
        var browse = new BrowseCursorV1(1, "browse", 8675309, uint.MaxValue, 42, ValidCriteria);
        var search = new SearchCursorV1(1, "search", 27, 84, ValidCriteria);

        var decodedBrowse = RecipeDiscoveryCursorCodec.DecodeBrowse(
            RecipeDiscoveryCursorCodec.Encode(browse));
        var decodedSearch = RecipeDiscoveryCursorCodec.DecodeSearch(
            RecipeDiscoveryCursorCodec.Encode(search));

        Assert.Equal(browse, decodedBrowse);
        Assert.Equal(search, decodedSearch);
    }

    [Theory]
    [InlineData("not*a*cursor")]
    [InlineData("e30")]
    [InlineData("")]
    public void CursorCodec_RejectsMalformedPayload(string cursor)
    {
        Assert.Throws<ValidationException>(
            () => RecipeDiscoveryCursorCodec.DecodeBrowse(cursor));
    }

    [Theory]
    [InlineData("{\"v\":2,\"mode\":\"browse\",\"seed\":1,\"rank\":0,\"id\":1,\"criteria\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"v\":1,\"mode\":\"other\",\"seed\":1,\"rank\":0,\"id\":1,\"criteria\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"v\":1,\"mode\":\"browse\",\"seed\":0,\"rank\":0,\"id\":1,\"criteria\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"v\":1,\"mode\":\"browse\",\"seed\":1,\"rank\":4294967296,\"id\":1,\"criteria\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    [InlineData("{\"v\":1,\"mode\":\"browse\",\"seed\":1,\"rank\":0,\"id\":0,\"criteria\":\"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA\"}")]
    public void CursorCodec_RejectsUnsupportedOrInvalidBrowseValues(string json)
    {
        Assert.Throws<ValidationException>(
            () => RecipeDiscoveryCursorCodec.DecodeBrowse(EncodeRaw(json)));
    }

    [Fact]
    public void CriteriaFingerprint_IsCanonicalAndChangesWithBoundCriteria()
    {
        var baseline = Criteria(
            userId: 12,
            searchTerms: ["Chicken Bowl", "rice"],
            ingredientIds: [9, 3, 9],
            badges: ["High Protein", "Budget Friendly"],
            pageSize: 20);
        var equivalent = Criteria(
            userId: 12,
            searchTerms: [" RICE ", " chicken   bowl "],
            ingredientIds: [3, 9],
            badges: ["budget friendly", "HIGH PROTEIN"],
            pageSize: 20);

        var fingerprint = RecipeDiscoveryCriteriaFingerprint.Create(baseline);

        Assert.Equal(
            fingerprint,
            RecipeDiscoveryCriteriaFingerprint.Create(equivalent));
        Assert.NotEqual(
            fingerprint,
            RecipeDiscoveryCriteriaFingerprint.Create(baseline with { CurrentUserId = 13 }));
        Assert.NotEqual(
            fingerprint,
            RecipeDiscoveryCriteriaFingerprint.Create(baseline with { SavedOnly = true }));
        Assert.NotEqual(
            fingerprint,
            RecipeDiscoveryCriteriaFingerprint.Create(baseline with { PageSize = 21 }));
    }

    [Fact]
    public async Task RecipeService_FirstPageGeneratesSeedAndNextPageReusesIt()
    {
        var queries = new CapturingRecipeQueries
        {
            Slice = new RecipeDiscoverySlice(
                [Card(7)],
                HasMore: true,
                new RecipeDiscoveryContinuation(1234, 7)),
        };
        var service = new RecipeService(queries);

        var firstPage = await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest(),
            CancellationToken.None);
        var firstQuery = Assert.Single(queries.CapturedQueries);
        var cursor = RecipeDiscoveryCursorCodec.DecodeBrowse(firstPage.NextCursor!);

        Assert.InRange(firstQuery.BrowseSeed, 1, int.MaxValue - 1);
        Assert.Equal(firstQuery.BrowseSeed, cursor.Seed);
        Assert.Equal(1234, cursor.Rank);
        Assert.Equal(7, cursor.Id);

        queries.Slice = new RecipeDiscoverySlice(
            [Card(8)],
            HasMore: false,
            Continuation: null);
        var secondPage = await service.DiscoverAsync(
            42,
            new RecipeDiscoveryRequest { Cursor = firstPage.NextCursor },
            CancellationToken.None);
        var secondQuery = queries.CapturedQueries[1];

        Assert.Equal(firstQuery.BrowseSeed, secondQuery.BrowseSeed);
        Assert.Equal(1234, secondQuery.AfterRank);
        Assert.Equal(7, secondQuery.AfterRecipeId);
        Assert.Null(secondPage.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task RecipeService_RejectsOutOfRangeLimit(int limit)
    {
        var queries = new CapturingRecipeQueries();
        var service = new RecipeService(queries);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.DiscoverAsync(
                42,
                new RecipeDiscoveryRequest { Limit = limit },
                CancellationToken.None));

        Assert.Empty(queries.CapturedQueries);
    }

    private static RecipeDiscoveryCriteria Criteria(
        int userId,
        IReadOnlyList<string> searchTerms,
        IReadOnlyList<int> ingredientIds,
        IReadOnlyList<string> badges,
        int pageSize) =>
        new(
            userId,
            searchTerms,
            ingredientIds,
            RequireAllIngredients: true,
            badges,
            MaxTotalMinutes: null,
            MaxDifficulty: RecipeDifficulty.Medium,
            SavedOnly: false,
            pageSize);

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

    private static string EncodeRaw(string json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    /// <summary>Captures normalized discovery queries while stubbing unrelated recipe persistence methods.</summary>
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
