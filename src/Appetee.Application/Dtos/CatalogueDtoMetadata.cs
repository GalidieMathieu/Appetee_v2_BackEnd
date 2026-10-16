using System.Text.Json.Serialization;
using Appetee.Application.Models.Catalogue;

namespace Appetee.Application.Dtos;

// Only the separate admin envelope serializes metadata; ordinary responses stay unchanged.
public sealed partial record RecipeSummaryDto
{
    [JsonIgnore] public CatalogueMetadata? CatalogueMetadata { get; init; }
}
public sealed partial record RecipeDetailDto
{
    [JsonIgnore] public CatalogueMetadata? CatalogueMetadata { get; init; }
}
public sealed partial record IngredientAdminDetailDto
{
    [JsonIgnore] public CatalogueMetadata? CatalogueMetadata { get; init; }
}
