using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.utils;
using System.Text.Json;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B11ManualItemRulesTests
{
    [Theory]
    [InlineData("name")][InlineData("caloriesKcal")][InlineData("proteinG")][InlineData("carbsG")][InlineData("fatG")]
    public void MissingField_IsRejectedWithExactNestedPath(string field)
    {
        var json=new Dictionary<string,object>{{"name","Food"},{"caloriesKcal",1},{"proteinG",1},{"carbsG",1},{"fatG",1}};
        json.Remove(field);var request=JsonSerializer.Deserialize<ManualItemRequest>(JsonSerializer.Serialize(json),new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var error=Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizeManualItem(request,"manualItem."));
        Assert.Contains("manualItem."+field,error.Errors!.Keys);
    }
    [Fact]
    public void AllMissingFields_AreReportedTogether_WithoutManufacturedZeroes()
    {
        var error=Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizeManualItem(new()));
        Assert.Equal(5,error.Errors!.Count);
    }
    [Fact]
    public void NormalizesNfcAndUnicodeScalarLength_WithoutCalorieMacroEquality()
    {
        var input=new ManualItemRequest{Name="  Cafe\u0301 ",CaloriesKcal=.01m,ProteinG=5000m,CarbsG=5000m,FatG=5000m};
        Assert.Equal("Café",MealPlanRules.NormalizeManualItem(input).Name);
        Assert.Equal(120,MealPlanRules.NormalizeManualItem(input with {Name=string.Concat(Enumerable.Repeat("😀",120))}).Name!.EnumerateRunes().Count());
        Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizeManualItem(input with {Name="\ud800"}));
        Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizeManualItem(input with {CaloriesKcal=1.001m}));
    }
}
