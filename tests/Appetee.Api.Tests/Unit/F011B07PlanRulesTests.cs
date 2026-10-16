using Appetee.Application.Models.MealPlans;
using Appetee.Application.utils;
using System.Text;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B07PlanRulesTests
{
    [Theory]
    [InlineData(1," - Copy")]
    [InlineData(2," - Copy 2")]
    [InlineData(50," - Copy 50")]
    [InlineData(51," - Copy 51")]
    public void DuplicateName_TruncatesUnicodeScalarsWithoutSplittingSurrogates(int number,string suffix)
    {
        var source=string.Concat(Enumerable.Repeat("😀",80));
        var copy=MealPlanRules.DuplicatePlanName(source,number);
        Assert.Equal(80,copy.EnumerateRunes().Count());
        Assert.Equal(string.Concat(Enumerable.Repeat("😀",80-suffix.Length))+suffix,copy);
        Assert.Equal(copy,MealPlanRules.NormalizePlanName(copy));
    }
    [Fact]
    public void DuplicateName_TrimsTruncatedBase_AndDoesNotStripAnExistingCopySuffix()
    {
        Assert.Equal("Plan - Copy - Copy",MealPlanRules.DuplicatePlanName("Plan - Copy",1));
        Assert.Equal("Plan - Copy",MealPlanRules.DuplicatePlanName("Plan   ",1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>MealPlanRules.DuplicatePlanName("Plan",0));
    }
    [Theory]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("\u0000")]
    [InlineData("\t")]
    public void PlanName_RejectsLineControlAndMalformedUtf16(string suffix)
    {
        var error=Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizePlanName("Plan"+suffix));
        Assert.Equal("validation_failed",error.Code); Assert.Contains("name",error.Errors!.Keys);
    }
    [Fact]
    public void PlanName_RejectsUnpairedSurrogateConstructedAtRuntime()
    {
        // Attribute metadata encodes strings as UTF-8 and replaces lone surrogates.
        // Construct the actual malformed UTF-16 input instead of using InlineData.
        Assert.Throws<ValidationException>(()=>MealPlanRules.NormalizePlanName("Plan"+new string((char)0xd800,1)));
    }
}
