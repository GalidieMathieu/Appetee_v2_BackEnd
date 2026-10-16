using Appetee.Application.Dtos.MealPlans;
using System.Net.Http.Json;
using System.Text.Json;

namespace Appetee.Api.Tests.Infrastructure;

public abstract class MealItemTestBase(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    protected const string Date="2026-10-13";
    protected const string Range="/api/meal-plan/days?from="+Date+"&through="+Date;
    protected const string RecipeRoute="/api/meal-plan/days/"+Date+"/recipe-items";
    protected const string ManualRoute="/api/meal-plan/days/"+Date+"/manual-items";
    protected static object Manual(string name="Synthetic food",decimal calories=100.01m,decimal protein=1.02m,decimal carbs=2.03m,decimal fat=3.04m) =>
        new {name,caloriesKcal=calories,proteinG=protein,carbsG=carbs,fatG=fat};
    protected static async Task<HttpResponseMessage> Send(HttpClient client,HttpMethod method,string route,object? body,string? revision)
    {
        using var request=new HttpRequestMessage(method,route);
        if(revision is not null)request.Headers.Add("X-Meal-Plan-Revision",revision);
        if(body is not null)request.Content=JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    protected static async Task<PlannerMutationDto> Receipt(HttpResponseMessage response)
    {response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<PlannerMutationDto>())!;}
    protected static async Task Current(HttpClient client,int count=4)
    {
        using var response=await Send(client,HttpMethod.Post,"/api/nutrition-plans",new {name="Synthetic current",mode="easy",defaultMealsPerDay=count},"0");
        response.EnsureSuccessStatusCode();
    }
    protected static async Task<int> Meal(HttpClient client)
    {
        using var response=await Send(client,HttpMethod.Post,"/api/meal-plan/days/"+Date+"/meals",new {name="Saved meal"},"0");
        return (await Receipt(response)).CreatedMealId!.Value;
    }
    protected static async Task Code(HttpResponseMessage response,string code)
    {
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code,json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId",out _));
    }
    protected async Task<string> Snapshot(int userId)
    {
        var rows=await Factory.Database.QueryAsync<string>("""
            SELECT row_value FROM (
              SELECT 0 kind,s.user_id id,CAST(JSON_ARRAY(s.user_id,s.current_nutrition_plan_id,s.revision,s.created_at,s.updated_at) AS CHAR) row_value FROM meal_plan_user_settings s WHERE s.user_id=@Id
              UNION ALL SELECT 1,d.id,CAST(JSON_ARRAY(d.id,d.plan_date,d.created_at,d.updated_at) AS CHAR) FROM meal_plan_days d WHERE d.user_id=@Id
              UNION ALL SELECT 2,m.id,CAST(JSON_ARRAY(m.id,m.meal_plan_day_id,m.name,m.sort_order,m.created_at,m.updated_at) AS CHAR) FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id
              UNION ALL SELECT 3,i.id,CAST(JSON_ARRAY(i.id,i.meal_plan_day_meal_id,i.item_type,i.recipe_id,i.quantity,i.sort_order,i.created_at,i.updated_at) AS CHAR) FROM meal_plan_meal_items i JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id
              UNION ALL SELECT 4,x.meal_plan_meal_item_id,CAST(JSON_ARRAY(x.meal_plan_meal_item_id,x.name,x.calories_kcal,x.protein_g,x.carbs_g,x.fat_g) AS CHAR) FROM meal_plan_manual_items x JOIN meal_plan_meal_items i ON i.id=x.meal_plan_meal_item_id JOIN meal_plan_day_meals m ON m.id=i.meal_plan_day_meal_id JOIN meal_plan_days d ON d.id=m.meal_plan_day_id WHERE d.user_id=@Id
            ) saved ORDER BY kind,id;
            """,new {Id=userId});
        return string.Join('|',rows);
    }
}
