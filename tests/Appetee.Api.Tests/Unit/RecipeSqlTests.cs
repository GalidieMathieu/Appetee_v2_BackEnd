using System.Text.RegularExpressions;

namespace Appetee.Api.Tests.Unit;

public sealed class RecipeSqlTests
{
    [Fact]
    public void RecipeList_UsesCardImageWithMainImageFallback()
    {
        Assert.Contains(
            "COALESCE(r.card_image_blob_name, r.image_blob_name) AS ImageBlobName",
            RecipeSql.GetAll,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecipeDetail_UsesMainImage()
    {
        Assert.Contains(
            "r.image_blob_name            AS ImageBlobName",
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
    public void IngredientCalculationDataQuery_IsSingleSetBasedStatement()
    {
        var sql = RecipeSql.GetIngredientCalculationDataByIds;

        Assert.Single(Regex.Matches(sql, @"\bSELECT\b", RegexOptions.IgnoreCase).Cast<Match>());
        Assert.Contains("WHERE i.id IN @Ids", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LEFT JOIN ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
    }
}
