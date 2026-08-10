using System.Text.RegularExpressions;

namespace Appetee.Api.Tests.Unit;

public sealed class RecipeSqlTests
{
    [Fact]
    public void IngredientCalculationDataQuery_IsSingleSetBasedStatement()
    {
        var sql = RecipeSql.GetIngredientCalculationDataByIds;

        Assert.Single(Regex.Matches(sql, @"\bSELECT\b", RegexOptions.IgnoreCase).Cast<Match>());
        Assert.Contains("WHERE i.id IN @Ids", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("LEFT JOIN ingredient_nutrition", sql, StringComparison.OrdinalIgnoreCase);
    }
}
