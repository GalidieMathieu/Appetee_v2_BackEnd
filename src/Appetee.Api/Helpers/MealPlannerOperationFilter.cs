using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

public sealed class MealPlannerOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation,OperationFilterContext context)
    {
        if(!context.ApiDescription.ActionDescriptor.RouteValues.TryGetValue("controller",out var controller) || controller!="MealPlanner")return;
        operation.Responses.TryAdd("400",new(){Description="validation_failed/revision_required/revision_invalid; no state changes."});
        operation.Responses.TryAdd("401",new(){Description="Authentication required."});
        operation.Responses.TryAdd("404",new(){Description="resource_not_found: owned object unavailable or wrong subtype; recipe_unavailable: canonical reference unavailable."});
        operation.Responses.TryAdd("500",new(){Description="Generic internal error; reconcile an ambiguous mutation outcome through GET."});
        foreach(var parameter in operation.Parameters)
        {
            if(parameter.In==ParameterLocation.Query && parameter.Name is "from" or "through")
            {parameter.Required=true;parameter.Schema.Type="string";parameter.Schema.Format="date";parameter.Description="Inclusive ISO calendar date, year 1000-9999. The interval must contain 1-7 dates.";}
            if(parameter.Name==MealPlanRevisionHeader.HeaderName)
            {parameter.Required=true;parameter.Schema.Type="string";parameter.Schema.Pattern="^[0-9]+$";parameter.Description="Current nonnegative signed BIGINT revision from the reviewed planner context; never substitute a newer unrelated version.";}
        }
        if(context.ApiDescription.HttpMethod!="GET")
        {
            operation.Responses.TryAdd("409",new(){Description="meal_plan_revision_conflict, meal_limit_reached, item_limit_reached, recipe_already_in_meal, destination_not_empty or source_recipe_unavailable; no changes saved."});
            operation.Responses.TryAdd("415",new(){Description="Unsupported media type."});
        }
        if(context.ApiDescription.RelativePath=="api/meal-plan/days/{date}/meal-order" && operation.RequestBody is not null)
            foreach(var content in operation.RequestBody.Content.Values)
                content.Schema=new OpenApiSchema {AllOf=[content.Schema],Properties=new Dictionary<string,OpenApiSchema>{
                    ["orderedIds"]=new(){Type="array",MaxItems=12,UniqueItems=true,Items=new(){Type="integer",Format="int32",Minimum=1,Maximum=int.MaxValue}}}};
    }
}
