using Appetee.Application.Dtos.MealPlans;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Appetee.Api.Tests.Infrastructure;

public abstract class PlannerCopyTestBase(AppeteeWebApplicationFactory factory):MealItemTestBase(factory)
{
    protected const string Source="2026-10-12",Destination="2026-10-19"; // Distinct complete Monday weeks.
    protected Task<long> SeedDay(int userId,string date,int mealCount=3,bool content=true)=>Seed(userId,date,mealCount,content);
    private async Task<long> Seed(int userId,string date,int mealCount,bool content)
    {
        var id=await Factory.Database.QuerySingleOrDefaultAsync<long>("""
            INSERT INTO meal_plan_days(user_id,plan_date,created_at,updated_at) VALUES(@Id,@Date,'2026-10-01 00:00:00','2026-10-01 00:00:00'); SELECT LAST_INSERT_ID();
            """,new {Id=userId,Date=date});
        if(mealCount==0)return id;
        var numbers=string.Join(" UNION ALL ",Enumerable.Range(1,mealCount).Select(n=>$"SELECT {n} n"));
        await Factory.Database.ExecuteAsync($"""
            INSERT INTO meal_plan_day_meals(meal_plan_day_id,name,sort_order,created_at,updated_at)
            SELECT @Id,CONCAT('Synthetic meal ',n),n,'2026-10-01 00:00:00','2026-10-01 00:00:00' FROM ({numbers}) seq;
            """,new {Id=id});
        if(content)
            await Factory.Database.ExecuteAsync("""
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,recipe_id,quantity,sort_order,created_at,updated_at)
                SELECT id,'Recipe',1,2,1,'2026-10-01 00:00:00','2026-10-01 00:00:00' FROM meal_plan_day_meals WHERE meal_plan_day_id=@Id AND sort_order=1;
                INSERT INTO meal_plan_meal_items(meal_plan_day_meal_id,item_type,sort_order,created_at,updated_at)
                SELECT id,'Manual',2,'2026-10-01 00:00:00','2026-10-01 00:00:00' FROM meal_plan_day_meals WHERE meal_plan_day_id=@Id AND sort_order=1;
                INSERT INTO meal_plan_manual_items(meal_plan_meal_item_id,name,calories_kcal,protein_g,carbs_g,fat_g)
                SELECT i.id,'Synthetic manual',123.45,10.01,20.02,3.03 FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id
                WHERE m.meal_plan_day_id=@Id AND i.item_type='Manual';
                """,new {Id=id});
        return id;
    }
    protected static Task<MealPlannerRangeDto?> Read(HttpClient client,string from,string? through=null)=>
        client.GetFromJsonAsync<MealPlannerRangeDto>($"/api/meal-plan/days?from={from}&through={through??from}");
    protected static string Shape(PlannerDayDto day)
    {
        var node=JsonNode.Parse(JsonSerializer.Serialize(day,new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        Clean(node);return node.ToJsonString();
        static void Clean(JsonNode? current)
        {
            if(current is JsonObject obj)
                foreach(var field in obj.ToArray())
                {if(field.Key is "id" or "key" or "date")obj.Remove(field.Key);else Clean(field.Value);}
            else if(current is JsonArray array)foreach(var child in array)Clean(child);
        }
    }
    protected static async Task<HttpResponseMessage> CopyDay(HttpClient client,string source=Source,string destination=Destination,bool replace=true,string revision="0")=>
        await Send(client,HttpMethod.Post,"/api/meal-plan/copy-day",new {sourceDate=source,destinationDate=destination,replaceExisting=replace},revision);
    protected static async Task<HttpResponseMessage> CopyWeek(HttpClient client,string source=Source,string destination=Destination,bool replace=true,string revision="0")=>
        await Send(client,HttpMethod.Post,"/api/meal-plan/copy-week",new {sourceMonday=source,destinationMonday=destination,replaceExisting=replace},revision);
}
