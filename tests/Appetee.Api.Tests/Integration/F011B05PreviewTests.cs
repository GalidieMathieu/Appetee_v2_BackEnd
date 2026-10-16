using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.Auth;
using Appetee.Application.Requests.MealPlans;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B05PreviewTests(AppeteeWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Path = "/api/nutrition-plans/calculation-preview";
    private static NutritionCalculationRequest Valid => new(
        new("male", DateTime.UtcNow.Year - 28, 180m, 80m), "maintain", "active");

    [Theory]
    [InlineData("maintain", 3500, 128, 506, 107)]
    [InlineData("fatLoss", 3000, 160, 401, 84)]
    public async Task Preview_ReturnsExactGoldenContractWithoutRevisionOrPersistence(
        string goal, int energy, int protein, int carbs, int fat)
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            using var response = await client.PostAsJsonAsync(Path, Valid with { Goal = goal });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            var text = await response.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<NutritionCalculationDto>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal("mp-v1.0.0-2026-10", dto!.PolicyVersion);
            Assert.Equal(DateTime.UtcNow.Year, dto.CalculationYear);
            Assert.Equal(28, dto.Inputs.AgeYears);
            Assert.Equal(Valid.Profile.BirthYear, dto.Inputs.BirthYear);
            Assert.Equal(goal, dto.Inputs.Goal); Assert.Equal("active", dto.Inputs.ActivityLevel);
            Assert.Equal(2005.48m, dto.RmrKcal); Assert.Equal(3509.60m, dto.MaintenanceKcal);
            Assert.Equal(new NutrientsDto(energy, protein, carbs, fat), dto.Targets);
            Assert.Empty(dto.Warnings);
            using var json = JsonDocument.Parse(text);
            Assert.Equal(7, json.RootElement.EnumerateObject().Count());
            Assert.Equal(7, json.RootElement.GetProperty("inputs").EnumerateObject().Count());
            Assert.Equal(4, json.RootElement.GetProperty("targets").EnumerateObject().Count());
            Assert.False(json.RootElement.TryGetProperty("id", out _));
            Assert.False(json.RootElement.TryGetProperty("revision", out _));
            Assert.False(json.RootElement.GetProperty("inputs").TryGetProperty("adultConfirmed", out _));
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Fact]
    public async Task Preview_UsesSubmittedProfileAndNeverReplacesEitherUsersSavedState()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        var (otherClient, other) = await CreateAuthenticatedClientAsync();
        using (client)
        using (otherClient)
        {
            foreach (var id in new[] { auth.userId, other.userId })
                await Factory.Database.ExecuteAsync("""
                    UPDATE users SET calculation_sex='female', birth_year=@Year, height_cm=160, weight_kg=60 WHERE id=@Id;
                    INSERT INTO nutrition_plans (user_id,name,mode,default_meals_per_day,created_at,updated_at)
                        VALUES (@Id,'Saved Easy','Easy',3,'2026-10-06','2026-10-06');
                    INSERT INTO meal_plan_user_settings(user_id,current_nutrition_plan_id,revision,created_at,updated_at)
                        VALUES (@Id,LAST_INSERT_ID(),7,'2026-10-06','2026-10-06');
                    """, new { Id = id, Year = DateTime.UtcNow.Year - 30 });
            var before = await SnapshotAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(Valid) };
            request.Headers.Add("X-Meal-Plan-Revision", "not-a-revision"); // preview never binds a revision
            using var response = await client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<NutritionCalculationDto>();
            Assert.Equal("male", dto!.Inputs.CalculationSex); Assert.Equal(80m, dto.Inputs.WeightKg);
            Assert.Equal(3500m, dto.Targets.CaloriesKcal);
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("goal", "0")]
    [InlineData("goal", "\"Maintain\"")]
    [InlineData("goal", "\"unknown\"")]
    [InlineData("goal", "null")]
    [InlineData("goal", "REMOVE")]
    [InlineData("activityLevel", "1")]
    [InlineData("activityLevel", "\"Active\"")]
    [InlineData("activityLevel", "\"unknown\"")]
    [InlineData("activityLevel", "null")]
    [InlineData("activityLevel", "REMOVE")]
    [InlineData("profile", "null")]
    [InlineData("profile", "REMOVE")]
    [InlineData("profile.calculationSex", "0")]
    [InlineData("profile.calculationSex", "\"Male\"")]
    [InlineData("profile.calculationSex", "REMOVE")]
    [InlineData("profile.birthYear", "\"1998\"")]
    [InlineData("profile.birthYear", "1998.5")]
    [InlineData("profile.birthYear", "2147483647")]
    [InlineData("profile.birthYear", "REMOVE")]
    [InlineData("profile.heightCm", "119.99")]
    [InlineData("profile.heightCm", "230.01")]
    [InlineData("profile.heightCm", "180.001")]
    [InlineData("profile.heightCm", "\"180\"")]
    [InlineData("profile.heightCm", "null")]
    [InlineData("profile.weightKg", "34.99")]
    [InlineData("profile.weightKg", "250.01")]
    [InlineData("profile.weightKg", "80.001")]
    [InlineData("profile.weightKg", "\"NaN\"")]
    [InlineData("profile.weightKg", "null")]
    [InlineData("profile.adultConfirmed", "null")]
    [InlineData("profile.ageYears", "28")]
    [InlineData("profile.userId", "1")]
    [InlineData("profile.ownerId", "1")]
    [InlineData("userId", "1")]
    [InlineData("ownerId", "1")]
    [InlineData("targets", "{}")]
    [InlineData("rmrKcal", "2005")]
    [InlineData("revision", "\"0\"")]
    [InlineData("policyVersion", "\"alternate\"")]
    [InlineData("calculationYear", "2026")]
    [InlineData("timestamp", "\"2026-10-06\"")]
    public async Task InvalidOrOverpostedInputs_ReturnFieldErrorsAndLeaveAllStateUnchanged(string field, string value)
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var body = JsonSerializer.SerializeToNode(Valid, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var parts = field.Split('.');
            var parent = parts.Length == 1 ? body.AsObject() : body[parts[0]]!.AsObject();
            if (value == "REMOVE") parent.Remove(parts[^1]);
            else parent[parts[^1]] = JsonNode.Parse(value);
            var before = await SnapshotAsync();
            using var response = await client.PostAsync(Path, new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
            var problem = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.True(problem.GetProperty("errors").EnumerateObject().Any());
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"profile\":{\"weightKg\":NaN}}")]
    [InlineData("{\"profile\":{\"weightKg\":Infinity}}")]
    public async Task InvalidJsonOrAbsentCompleteRequest_ReturnsStableErrorWithoutWrites(string body)
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            using var response = await client.PostAsync(Path, new StringContent(body, Encoding.UTF8, "application/json"));
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("userId=1")]
    [InlineData("ownerId=1")]
    [InlineData("limit=20")]
    public async Task QueryParameters_AreNotAccepted(string query)
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            using var response = await client.PostAsJsonAsync(Path + "?" + query, Valid);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Theory]
    [InlineData("female", 65, 120, 35, "inactive")]
    [InlineData("male", 18, 230, 250, "veryActive")]
    public async Task UnsupportedEnergy_ReturnsRangeErrorWithoutClampingOrWriting(
        string sex, int age, decimal height, decimal weight, string activity)
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            var body = new NutritionCalculationRequest(new(sex, DateTime.UtcNow.Year - age, height, weight, true), "maintain", activity);
            using var response = await client.PostAsJsonAsync(Path, body);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "calculation_outside_supported_range");
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Fact]
    public async Task WarningCodes_AreNonblockingAndDoNotChangePersistence()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            var body = Valid with { Profile = Valid.Profile with { CalculationSex = "female", BirthYear = DateTime.UtcNow.Year - 36 }, Goal = "fatLoss" };
            using var response = await client.PostAsJsonAsync(Path, body);
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<NutritionCalculationDto>();
            Assert.Equal(new[] { "equation_population_extrapolation", "carbohydrate_below_preferred" }, dto!.Warnings);
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Fact]
    public async Task ServerUtcYearAndAdultDeclaration_AreAuthoritativeInHttpPreview()
    {
        using var customized = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2029, 12, 31, 17, 0, 0, TimeSpan.FromHours(-7))));
        }));
        using var client = AppeteeWebApplicationFactory.CreateCsrfClient(customized);
        using var signup = await client.PostAsJsonAsync("/api/auth/sign-up", new SignUpRequest("Preview Year", "preview-year@appetee.test", "Password123!"));
        signup.EnsureSuccessStatusCode();
        var before = await SnapshotAsync();
        var request = new NutritionCalculationRequest(new("male", 2012, 180m, 80m), "maintain", "active");
        using var unconfirmed = await client.PostAsJsonAsync(Path, request);
        var problem = await AssertErrorAsync(unconfirmed, HttpStatusCode.BadRequest, "validation_failed");
        Assert.True(problem.GetProperty("errors").TryGetProperty("profile.adultConfirmed", out _));
        using var confirmed = await client.PostAsJsonAsync(Path, request with { Profile = request.Profile with { AdultConfirmed = true } });
        confirmed.EnsureSuccessStatusCode();
        var dto = await confirmed.Content.ReadFromJsonAsync<NutritionCalculationDto>();
        Assert.Equal(2030, dto!.CalculationYear); Assert.Equal(18, dto.Inputs.AgeYears);
        using var underage = await client.PostAsJsonAsync(Path, request with { Profile = request.Profile with { BirthYear = 2013, AdultConfirmed = true } });
        await AssertErrorAsync(underage, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task AnonymousPreview_IsUnauthorizedAndWritesNothing()
    {
        var before = await SnapshotAsync();
        using var response = await Client.PostAsJsonAsync(Path, Valid);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(before, await SnapshotAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid-token")]
    public async Task MissingOrInvalidCsrf_IsRejectedBeforeCalculationAndWritesNothing(string? csrf)
    {
        using var client = Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var signup = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-up")
        {
            Content = JsonContent.Create(new SignUpRequest("CSRF Preview", "csrf-preview@appetee.test", "Password123!")),
        };
        signup.Headers.Add("Origin", "https://localhost:4200");
        signup.Headers.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        using var signedIn = await client.SendAsync(signup); signedIn.EnsureSuccessStatusCode();
        var before = await SnapshotAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, Path) { Content = JsonContent.Create(Valid) };
        request.Headers.Add("Origin", "https://localhost:4200");
        if (csrf is not null) request.Headers.Add("X-CSRF-TOKEN", csrf);
        using var response = await client.SendAsync(request);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        Assert.Equal(before, await SnapshotAsync());
    }

    [Fact]
    public async Task UnsupportedMediaType_Returns415WithoutWriting()
    {
        var (client, _) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync();
            using var response = await client.PostAsync(Path, new FormUrlEncodedContent(new Dictionary<string, string> { ["goal"] = "maintain" }));
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
            Assert.Equal(before, await SnapshotAsync());
        }
    }

    [Fact]
    public async Task OpenApi_DeclaresStrictPreviewWithCsrfButNoRevisionOrOwnerParameters()
    {
        using var json = JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var request = schemas.GetProperty("NutritionCalculationRequest");
        Assert.False(request.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(3, request.GetProperty("required").GetArrayLength());
        Assert.Equal(6, request.GetProperty("properties").GetProperty("goal").GetProperty("enum").GetArrayLength());
        Assert.Equal(4, request.GetProperty("properties").GetProperty("activityLevel").GetProperty("enum").GetArrayLength());
        Assert.Equal(7, schemas.GetProperty("NutritionCalculationDto").GetProperty("properties").EnumerateObject().Count());
        var operation = json.RootElement.GetProperty("paths").GetProperty(Path).GetProperty("post");
        var parameters = operation.GetProperty("parameters").EnumerateArray().ToArray();
        Assert.Contains(parameters, p => p.GetProperty("name").GetString() == "X-CSRF-TOKEN" && p.GetProperty("required").GetBoolean());
        Assert.DoesNotContain(parameters, p => p.GetProperty("name").GetString() is "X-Meal-Plan-Revision" or "userId" or "ownerId");
    }

    private Task<string?> SnapshotAsync() => Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_OBJECT(
            'users', (SELECT JSON_ARRAYAGG(JSON_OBJECT('id',id,'sex',calculation_sex,'year',birth_year,
                'height',height_cm,'weight',weight_kg,'updated',updated_at)) FROM users),
            'settings', (SELECT JSON_ARRAYAGG(JSON_OBJECT('id',user_id,'current',current_nutrition_plan_id,
                'revision',revision,'created',created_at,'updated',updated_at)) FROM meal_plan_user_settings),
            'plans', (SELECT JSON_ARRAYAGG(JSON_OBJECT('id',id,'owner',user_id,'name',name,'mode',mode,
                'created',created_at,'updated',updated_at,'calculated',calculated_at,
                'targets',JSON_ARRAY(target_calories_kcal,target_protein_g,target_carbs_g,target_fat_g))) FROM nutrition_plans),
            'days', (SELECT COUNT(*) FROM meal_plan_days),
            'meals', (SELECT COUNT(*) FROM meal_plan_day_meals),
            'items', (SELECT COUNT(*) FROM meal_plan_meal_items),
            'manual', (SELECT COUNT(*) FROM meal_plan_manual_items)
        ) AS CHAR);
        """);

    private static async Task<JsonElement> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {status}, got {response.StatusCode}: {text}");
        using var json = JsonDocument.Parse(text);
        Assert.Equal(code, json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        return json.RootElement.Clone();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
