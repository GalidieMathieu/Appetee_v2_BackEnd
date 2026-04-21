using Appetee.Application.Dtos;
using Appetee.Api.Tests.Infrastructure;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Integration;

public sealed class CatalogEndpointsTests : IntegrationTestBase
{
    public CatalogEndpointsTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    [Fact]
    public async Task GetDiets_ReturnsSeededDiets()
    {
        var diets = await Client.GetFromJsonAsync<IReadOnlyList<DietDto>>("/api/diets");

        Assert.NotNull(diets);
        Assert.Equal(3, diets!.Count);
        Assert.Contains(diets, diet => diet.id == 3 && diet.name == "High Protein");
    }

    [Fact]
    public async Task GetIngredients_ReturnsSeededIngredients()
    {
        var ingredients = await Client.GetFromJsonAsync<IReadOnlyList<IngredientDto>>("/api/ingredients");

        Assert.NotNull(ingredients);
        Assert.Equal(4, ingredients!.Count);
        Assert.Contains(ingredients, ingredient => ingredient.id == 1 && ingredient.name == "Chicken Breast");
    }
}
