using Appetee.Application.Abstractions.MealPlans;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.utils;

namespace Appetee.Application.Services.MealPlans;

public sealed class PlannerCopyService(IPlannerCopyRepository repository) : IPlannerCopyService
{
    public async Task<PlannerMutationDto> CopyDayAsync(int currentUserId,CopyDayRequest request,long expectedRevision,CancellationToken ct)
    {
        var source=MealPlannerService.ParseDate(request.SourceDate,"sourceDate");
        var destination=MealPlannerService.ParseDate(request.DestinationDate,"destinationDate");
        Distinct(source,destination,"destinationDate");
        return Map(await repository.CopyDayAsync(currentUserId,source,destination,request.ReplaceExisting,expectedRevision,ct));
    }
    public async Task<PlannerMutationDto> CopyWeekAsync(int currentUserId,CopyWeekRequest request,long expectedRevision,CancellationToken ct)
    {
        var source=Week(request.SourceMonday,"sourceMonday");var destination=Week(request.DestinationMonday,"destinationMonday");
        Distinct(source,destination,"destinationMonday");
        return Map(await repository.CopyWeekAsync(currentUserId,source,destination,request.ReplaceExisting,expectedRevision,ct));
    }
    private static DateOnly Week(string date,string field)
    {
        var parsed=MealPlannerService.ParseDate(date,field);
        if(parsed.DayOfWeek!=DayOfWeek.Monday || parsed.DayNumber>DateOnly.MaxValue.DayNumber-6)
            throw MealPlanRules.InvalidPlanField(field,"Use a Monday with a complete seven-date interval in years 1000–9999.");
        return parsed;
    }
    private static void Distinct(DateOnly source,DateOnly destination,string field)
    {if(source==destination)throw MealPlanRules.InvalidPlanField(field,"Source and destination must differ.");}
    private static PlannerMutationDto Map(MealPlanWriteResult<PlannerMutationRowData> result)
    {
        var captured=result.Payload;
        if(captured.Readback.Context.Revision!=result.Revision)throw new InternalServerException("The captured planner revision is invalid.");
        var range=MealPlanReadModelBuilder.Build(captured.Readback,captured.From,captured.Through);
        return new(range.Revision,range.Settings,range.Days,null,null);
    }
}
