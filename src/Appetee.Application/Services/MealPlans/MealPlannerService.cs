using System.Globalization;
using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public sealed class MealPlannerService(IMealPlannerQueries queries) : IMealPlannerService
{
    public async Task<MealPlannerRangeDto> GetRangeAsync(int currentUserId,string? from,string? through,CancellationToken ct)
    {
        var first=ParseDate(from,"from"); var last=ParseDate(through,"through");
        if(last.DayNumber-first.DayNumber is <0 or >6)throw MealPlanRules.InvalidPlanField("through","Request an inclusive interval of 1-7 dates.");
        return MealPlanReadModelBuilder.Build(await queries.GetRangeAsync(currentUserId,first,last,ct),first,last);
    }
    public static DateOnly ParseDate(string? value,string field)
    {
        if(value is null || value.Length!=10 || !DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) || date.Year<1000)
            throw MealPlanRules.InvalidPlanField(field,"Use a valid ISO calendar date in years 1000-9999.");
        return date;
    }
}
