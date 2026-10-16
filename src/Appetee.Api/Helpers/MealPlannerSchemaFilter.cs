using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Appetee.Api.Helpers;

public sealed class MealPlannerSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema,SchemaFilterContext context)
    {
        if(context.Type==typeof(ReorderRequest))
        {
            schema.AdditionalPropertiesAllowed=false;schema.Required.Add("orderedIds");
            var ids=schema.Properties["orderedIds"];ids.Nullable=false;ids.UniqueItems=true;ids.MaxItems=20;
            ids.Items.Minimum=1;ids.Items.Maximum=int.MaxValue;
            ids.Description="Complete permutation of saved parent membership; max 12 meals or 20 items. Empty only for saved empty parent; no reparenting.";
        }
        if(context.Type==typeof(CopyDayRequest) || context.Type==typeof(CopyWeekRequest))
        {
            schema.AdditionalPropertiesAllowed=false;schema.Required.UnionWith(schema.Properties.Keys);
            foreach(var (name,value) in schema.Properties)
            {
                value.Nullable=false;
                if(name!="replaceExisting"){value.Type="string";value.Format="date";value.Description="ISO calendar date, year 1000-9999; week copy requires Monday and a complete 7-date range. Source/destination must differ.";}
            }
            schema.Properties["replaceExisting"].Description="Required explicit confirmation for any persisted destination, including a saved empty day. The revision must jointly cover reviewed source/destination.";
        }
        if(context.Type==typeof(MealTargetRequest))
        {
            schema.AdditionalPropertiesAllowed=false;schema.Properties.Clear();schema.Required.Clear();
            schema.OneOf=[Target("mealId",1,int.MaxValue),Target("defaultSlot",1,8)];
            schema.Description="Exactly one non-null mealId belonging to route date, or current defaultSlot on an untouched date.";
        }
        if(context.Type==typeof(AddRecipeItemRequest) || context.Type==typeof(AddManualItemRequest)
            || context.Type==typeof(SetRecipeQuantityRequest) || context.Type==typeof(ManualItemRequest))
        {
            schema.AdditionalPropertiesAllowed=false;schema.Required.UnionWith(schema.Properties.Keys);
            foreach(var property in schema.Properties.Values)property.Nullable=false;
            if(schema.Properties.TryGetValue("recipeId",out var recipeId)){recipeId.Minimum=1;recipeId.Maximum=int.MaxValue;}
            if(schema.Properties.TryGetValue("quantity",out var quantity)){quantity.Minimum=1;quantity.Maximum=20;}
            if(context.Type==typeof(ManualItemRequest))
            {
                schema.Properties["name"].MinLength=1;schema.Properties["name"].MaxLength=120;
                schema.Properties["name"].Description="Trimmed NFC plain text, 1-120 Unicode scalar values; no line/control characters.";
                foreach(var field in new[]{"caloriesKcal","proteinG","carbsG","fatG"})
                {
                    var value=schema.Properties[field];value.Minimum=.01m;value.Maximum=field=="caloriesKcal" ? 20000m : 5000m;
                    value.MultipleOf=.01m;value.Description="Required strictly positive total-entry value, at most two decimals; no serving multiplier or calorie/macro equality constraint.";
                }
            }
        }
        if(context.Type==typeof(CreateMealRequest) || context.Type==typeof(RenameMealRequest))
        {
            schema.AdditionalPropertiesAllowed=false;schema.Required.Add("name");
            schema.Properties["name"].MinLength=1;schema.Properties["name"].MaxLength=80;
            schema.Properties["name"].Description="Trimmed NFC plain text, 1-80 Unicode scalar values; no line/control characters.";
            if(context.Type==typeof(CreateMealRequest))
            {
                var slot=schema.Properties["defaultSlot"];slot.Minimum=1;slot.Maximum=8;slot.Nullable=false;
                slot.Description="Omit for explicit creation. A supplied integer slot requires an untouched date and a Current plan containing that slot.";
            }
        }
        if(context.Type==typeof(PlannerItemDto))
        {
            var recipe=context.SchemaGenerator.GenerateSchema(typeof(PlannerRecipeItemDto),context.SchemaRepository);
            var manual=context.SchemaGenerator.GenerateSchema(typeof(PlannerManualItemDto),context.SchemaRepository);
            schema.Properties.Clear();schema.Required.Clear();schema.OneOf=[recipe,manual];
            schema.Discriminator=new OpenApiDiscriminator {PropertyName="type",Mapping=new Dictionary<string,string> {
                ["recipe"]="#/components/schemas/"+recipe.Reference.Id,["manual"]="#/components/schemas/"+manual.Reference.Id }};
        }
        if(context.Type==typeof(PlannerRecipeItemDto) || context.Type==typeof(PlannerManualItemDto))
        {
            var type=context.Type==typeof(PlannerRecipeItemDto) ? "recipe" : "manual";
            schema.Properties["type"]=new(){Type="string",Enum=[new OpenApiString(type)]};
            schema.Required.UnionWith(schema.Properties.Keys);schema.AdditionalPropertiesAllowed=false;
            if(type=="recipe")
            {
                schema.Properties["name"].Nullable=true;schema.Properties["perServing"].Nullable=true;
                schema.Properties["availability"].Enum=[new OpenApiString("available"),new OpenApiString("unavailable")];
            }
        }
        if(context.Type==typeof(MealPlannerRangeDto) || context.Type==typeof(PlannerMutationDto))
        {
            schema.Required.UnionWith(schema.Properties.Keys);
            schema.Properties["revision"].Type="string";schema.Properties["revision"].Pattern="^[0-9]+$";
        }
    }
    private static OpenApiSchema Target(string field,int minimum,int maximum) => new()
    {
        Type="object",AdditionalPropertiesAllowed=false,Required=new HashSet<string>{field},
        Properties=new Dictionary<string,OpenApiSchema>{{field,new(){Type="integer",Format="int32",Minimum=minimum,Maximum=maximum,Nullable=false}}}
    };
}
