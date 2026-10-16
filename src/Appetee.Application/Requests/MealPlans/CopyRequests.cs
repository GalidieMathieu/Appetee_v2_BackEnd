using System.Text.Json.Serialization;

namespace Appetee.Application.Requests.MealPlans;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CopyDayRequest
{
    public required string SourceDate {get;init;}
    public required string DestinationDate {get;init;}
    public required bool ReplaceExisting {get;init;}
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CopyWeekRequest
{
    public required string SourceMonday {get;init;}
    public required string DestinationMonday {get;init;}
    public required bool ReplaceExisting {get;init;}
}
