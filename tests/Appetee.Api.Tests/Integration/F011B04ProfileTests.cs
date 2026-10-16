using Appetee.Api.Tests.Infrastructure;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Infrastructure.Users;
using Appetee.Infrastructure.MealPlans;
using Appetee.Application.Models.MealPlans;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Appetee.Api.Tests.Integration;

public sealed class F011B04ProfileTests : IntegrationTestBase
{
    private const string Path = "/api/users/me/nutrition-profile";
    private const string RevisionHeader = "X-Meal-Plan-Revision";
    private static NutritionProfileRequest Valid => new("female", DateTime.UtcNow.Year - 30, 170m, 65m);

    public F011B04ProfileTests(AppeteeWebApplicationFactory factory) : base(factory) { }

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, object body, string? revision = "0")
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(body) };
        if (revision is not null) request.Headers.TryAddWithoutValidation(RevisionHeader, revision);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task GetAbsent_ReturnsExactNullableContractWithoutWriting()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync(auth.userId);
            using var response = await client.GetAsync(Path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.EnumerateObject().Count());
            Assert.Equal("0", json.RootElement.GetProperty("revision").GetString());
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("profile").ValueKind);
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Theory]
    [InlineData("male", 120, 35)]
    [InlineData("female", 230, 250)]
    public async Task PutBoundaryValues_ReturnsCapturedProfileAndIncrementsOnce(string sex, decimal height, decimal weight)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var body = Valid with { CalculationSex = sex, HeightCm = height, WeightKg = weight };
            using var response = await PutAsync(client, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var profile = await response.Content.ReadFromJsonAsync<NutritionProfileDto>();
            Assert.Equal(1, profile!.Revision);
            Assert.Equal(new NutritionProfileValue(sex, body.BirthYear, height, weight), profile.Profile);
            Assert.Equal(profile, await client.GetFromJsonAsync<NutritionProfileDto>(Path));
            Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<long>(
                "SELECT revision FROM meal_plan_user_settings WHERE user_id = @Id;", new { Id = auth.userId }));
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2, json.RootElement.EnumerateObject().Count());
            Assert.Equal("1", json.RootElement.GetProperty("revision").GetString());
            Assert.Equal(4, json.RootElement.GetProperty("profile").EnumerateObject().Count());
            Assert.False(json.RootElement.GetProperty("profile").TryGetProperty("adultConfirmed", out _));
        }
    }

    [Theory]
    [InlineData("calculationSex", "0")]
    [InlineData("calculationSex", "\"Male\"")]
    [InlineData("calculationSex", "null")]
    [InlineData("calculationSex", "<missing>")]
    [InlineData("birthYear", "<missing>")]
    [InlineData("birthYear", "null")]
    [InlineData("birthYear", "\"2000\"")]
    [InlineData("birthYear", "2000.5")]
    [InlineData("birthYear", "2147483647")]
    [InlineData("birthYear", "1900")]
    [InlineData("heightCm", "119.99")]
    [InlineData("heightCm", "230.01")]
    [InlineData("heightCm", "170.001")]
    [InlineData("heightCm", "170.000")]
    [InlineData("heightCm", "\"170\"")]
    [InlineData("heightCm", "<missing>")]
    [InlineData("heightCm", "null")]
    [InlineData("weightKg", "34.99")]
    [InlineData("weightKg", "250.01")]
    [InlineData("weightKg", "65.001")]
    [InlineData("weightKg", "\"NaN\"")]
    [InlineData("weightKg", "<missing>")]
    [InlineData("adultConfirmed", "null")]
    [InlineData("userId", "2")]
    [InlineData("ownerId", "2")]
    [InlineData("ageYears", "30")]
    [InlineData("revision", "0")]
    [InlineData("goal", "\"maintain\"")]
    public async Task InvalidBody_IsStructured400AndLeavesAllStateUnchanged(string field, string value)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync(auth.userId);
            var body = JsonSerializer.SerializeToNode(Valid, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
            if (value == "<missing>") body.Remove(field);
            else body[field] = JsonNode.Parse(value);
            using var request = new HttpRequestMessage(HttpMethod.Put, Path)
            {
                Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
            };
            request.Headers.Add(RevisionHeader, "0");
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.True(problem!.Extensions.ContainsKey("errors"));
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Theory]
    [InlineData(null, "revision_required")]
    // HttpClient omits an empty header on the wire; malformed present values are covered below.
    [InlineData("", "revision_required")]
    [InlineData("-1", "revision_invalid")]
    [InlineData("1.0", "revision_invalid")]
    [InlineData("9223372036854775808", "revision_invalid")]
    public async Task InvalidRevision_LeavesProfileAndSettingsAbsent(string? revision, string code)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync(auth.userId);
            using var response = await PutAsync(client, Valid, revision);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, code);
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Fact]
    public async Task SameValues_AreNoOpButStillRequireCurrentRevision()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            using var first = await PutAsync(client, Valid);
            first.EnsureSuccessStatusCode();
            await Factory.Database.ExecuteAsync("UPDATE users SET updated_at = '2020-01-01' WHERE id = @Id;", new { Id = auth.userId });
            var before = await SnapshotAsync(auth.userId);
            using var noOp = await PutAsync(client, Valid with { AdultConfirmed = true }, "1");
            Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);
            Assert.Equal(1, (await noOp.Content.ReadFromJsonAsync<NutritionProfileDto>())!.Revision);
            Assert.Equal(before, await SnapshotAsync(auth.userId));
            using var stale = await PutAsync(client, Valid, "0");
            await AssertErrorAsync(stale, HttpStatusCode.Conflict, "meal_plan_revision_conflict");
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Fact]
    public async Task NoOpWithExistingProfileButNoSettings_DoesNotCreateSettings()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            await Factory.Database.ExecuteAsync(UserSql.UpdateNutritionProfile, new
            {
                CurrentUserId = auth.userId, Valid.CalculationSex, Valid.BirthYear, Valid.HeightCm, Valid.WeightKg,
            });
            var before = await SnapshotAsync(auth.userId);
            using var response = await PutAsync(client, Valid);
            response.EnsureSuccessStatusCode();
            Assert.Equal(0, (await response.Content.ReadFromJsonAsync<NutritionProfileDto>())!.Revision);
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Fact]
    public async Task SeparateProfile_PreservesLegacyContractAndOtherUsers()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        var (other, otherAuth) = await CreateAuthenticatedClientAsync();
        using (client)
        using (other)
        {
            using var legacyUpdate = await client.PutAsJsonAsync("/api/users/me", new { imageUrl = "https://cdn.test/profile.png" });
            legacyUpdate.EnsureSuccessStatusCode();
            var ordinaryBefore = await client.GetStringAsync("/api/users/me");
            var otherBefore = await SnapshotAsync(otherAuth.userId);
            using var save = await PutAsync(client, Valid);
            save.EnsureSuccessStatusCode();
            Assert.Equal(ordinaryBefore, await client.GetStringAsync("/api/users/me"));
            using var json = JsonDocument.Parse(ordinaryBefore);
            Assert.Equal(2, json.RootElement.EnumerateObject().Count());
            Assert.Equal(otherBefore, await SnapshotAsync(otherAuth.userId));
            Assert.Null((await other.GetFromJsonAsync<NutritionProfileDto>(Path))!.Profile);
            foreach (var id in new[] { auth.userId, otherAuth.userId, 999999 })
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{id}/nutrition-profile")).StatusCode);
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task QueryParameters_AreRejectedRatherThanAcceptingOwnerId(string method)
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync(auth.userId);
            using var request = new HttpRequestMessage(new HttpMethod(method), Path + "?userId=2&ownerId=2");
            if (method == "PUT") { request.Headers.Add(RevisionHeader, "0"); request.Content = JsonContent.Create(Valid); }
            using var response = await client.SendAsync(request);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    public async Task AnonymousRequests_Retain401SessionSemantics(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), Path);
        if (method == "PUT") { request.Headers.Add(RevisionHeader, "0"); request.Content = JsonContent.Create(Valid); }
        using var response = await Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task ProfileWrite_UsesUtcServerYearAndAdultDeclaration()
    {
        var time = new AdjustableTimeProvider(new DateTimeOffset(2029, 12, 31, 17, 0, 0, TimeSpan.FromHours(-7)));
        using var factory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>(); services.AddSingleton<TimeProvider>(time);
        }));
        using var client = AppeteeWebApplicationFactory.CreateCsrfClient(factory);
        using var signup = await client.PostAsJsonAsync("/api/auth/sign-up",
            new Appetee.Application.Requests.Auth.SignUpRequest("Year test", "year@appetee.test", "Password123!"));
        signup.EnsureSuccessStatusCode();
        var body = Valid with { BirthYear = 2012 };
        using var unconfirmed = await PutAsync(client, body);
        await AssertErrorAsync(unconfirmed, HttpStatusCode.BadRequest, "validation_failed");
        using var confirmed = await PutAsync(client, body with { AdultConfirmed = true });
        confirmed.EnsureSuccessStatusCode();
        using var tooYoung = await PutAsync(client, body with { BirthYear = 2013, AdultConfirmed = true }, "1");
        await AssertErrorAsync(tooYoung, HttpStatusCode.BadRequest, "validation_failed");
        using var maxAge = await PutAsync(client, body with { BirthYear = 1965 }, "1");
        maxAge.EnsureSuccessStatusCode();
        using var tooOld = await PutAsync(client, body with { BirthYear = 1964 }, "2");
        await AssertErrorAsync(tooOld, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(1965, (await client.GetFromJsonAsync<NutritionProfileDto>(Path))!.Profile!.BirthYear);
    }

    private Task<string?> SnapshotAsync(int id) => Factory.Database.QuerySingleOrDefaultAsync<string>("""
        SELECT CAST(JSON_OBJECT('sex', u.calculation_sex, 'year', u.birth_year,
            'height', u.height_cm, 'weight', u.weight_kg, 'updated', u.updated_at,
            'settingsId', s.user_id, 'revision', s.revision, 'settingsUpdated', s.updated_at) AS CHAR)
        FROM users u LEFT JOIN meal_plan_user_settings s ON s.user_id = u.id WHERE u.id = @Id;
        """, new { Id = id });

    [Fact]
    public async Task ProfileChange_DoesNotRewriteSavedPlanInputsTargetsOrTimestamps()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            await Factory.Database.ExecuteAsync("""
                INSERT INTO nutrition_plans (
                    user_id, name, mode, default_meals_per_day, goal, activity_level,
                    calculation_sex, birth_year, age_years, height_cm, weight_kg,
                    calculation_policy_version, rmr_kcal, maintenance_kcal,
                    target_calories_kcal, target_protein_g, target_carbs_g, target_fat_g,
                    calculated_at, created_at, updated_at
                ) VALUES (
                    @Id, 'Unchanged Plan', 'Intermediate', 4, 'maintain', 'active',
                    'female', 1995, 31, 168.25, 64.50, 'mp-v1.0.0-2026-10', 1500.25, 2625.44,
                    2500, 104, 352, 75, '2026-10-05', '2026-10-05', '2026-10-05'
                );
                """, new { Id = auth.userId });
            const string readPlan = """
                SELECT CAST(JSON_OBJECT('targets', JSON_ARRAY(target_calories_kcal, target_protein_g,
                    target_carbs_g, target_fat_g), 'calculated', calculated_at, 'updated', updated_at,
                    'inputs', JSON_ARRAY(calculation_sex, birth_year, age_years, height_cm, weight_kg,
                        calculation_policy_version, rmr_kcal, maintenance_kcal)) AS CHAR)
                FROM nutrition_plans WHERE user_id = @Id;
                """;
            var before = await Factory.Database.QuerySingleOrDefaultAsync<string>(readPlan, new { Id = auth.userId });
            using var response = await PutAsync(client, Valid with { WeightKg = 80m });
            response.EnsureSuccessStatusCode();
            Assert.Equal(before, await Factory.Database.QuerySingleOrDefaultAsync<string>(readPlan, new { Id = auth.userId }));
        }
    }

    [Fact]
    public async Task ConcurrentMealWriteAndProfileWrite_ShareOwnerLockAndRevision()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        using (var scope = Factory.Services.CreateScope())
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var transaction = scope.ServiceProvider.GetRequiredService<MealPlanTransaction>();
            var meal = transaction.ExecuteAsync("test.meal-create", auth.userId, 0,
                async context =>
                {
                    entered.SetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10));
                    await context.ExecuteAsync("""
                        INSERT INTO meal_plan_days (user_id, plan_date, created_at, updated_at)
                        VALUES (@Id, '2026-10-06', @Utc, @Utc);
                        """, new { Id = auth.userId, Utc = context.TimestampUtc });
                    var day = await context.QuerySingleAsync<int>("SELECT LAST_INSERT_ID();");
                    await context.ExecuteAsync("""
                        INSERT INTO meal_plan_day_meals (meal_plan_day_id, name, sort_order, created_at, updated_at)
                        VALUES (@Day, 'Test Lunch', 1, @Utc, @Utc);
                        """, new { Day = day, Utc = context.TimestampUtc });
                    return MealPlanMutation<int>.ChangedResult(day);
                }, CancellationToken.None);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Task<HttpResponseMessage> profile;
            try
            {
                profile = PutAsync(client, Valid);
                await Task.Delay(100);
                Assert.False(profile.IsCompleted);
            }
            finally { release.TrySetResult(); }
            Assert.Equal(1, (await meal).Revision);
            using var response = await profile;
            await AssertErrorAsync(response, HttpStatusCode.Conflict, "meal_plan_revision_conflict");
            var current = await client.GetFromJsonAsync<NutritionProfileDto>(Path);
            Assert.Equal(1, current!.Revision);
            Assert.Null(current.Profile);
            Assert.Equal(1, await Factory.Database.QuerySingleOrDefaultAsync<int>("""
                SELECT COUNT(*) FROM meal_plan_day_meals m JOIN meal_plan_days d ON d.id = m.meal_plan_day_id
                WHERE d.user_id = @Id;
                """, new { Id = auth.userId }));
        }
    }

    [Fact]
    public async Task GetLookup_UsesExistingPrimaryKeys()
    {
        await Factory.Database.ExecuteAsync("""
            INSERT INTO meal_plan_user_settings (user_id, revision, created_at, updated_at)
            VALUES (1, 0, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6));
            """);
        var rows = await Factory.Database.QueryAsync<LookupPlan>("EXPLAIN " + UserSql.GetNutritionProfile,
            new { CurrentUserId = 1 });
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("PRIMARY", row.Key));
    }

    [Fact]
    public async Task CsrfFailure_PreventsNutritionWrite()
    {
        // Raw client deliberately does not use the ordinary test client's token helper.
        using var client = Factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var signup = new HttpRequestMessage(HttpMethod.Post, "/api/auth/sign-up")
        {
            Content = JsonContent.Create(new Appetee.Application.Requests.Auth.SignUpRequest(
                "CSRF Profile", "csrf-profile@appetee.test", "Password123!")),
        };
        signup.Headers.Add("Origin", "https://localhost:4200");
        signup.Headers.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        using var signedIn = await client.SendAsync(signup);
        signedIn.EnsureSuccessStatusCode();
        using var request = new HttpRequestMessage(HttpMethod.Put, Path) { Content = JsonContent.Create(Valid) };
        request.Headers.Add("Origin", "https://localhost:4200"); request.Headers.Add(RevisionHeader, "0");
        using var response = await client.SendAsync(request);
        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "csrf_invalid");
        var current = await client.GetFromJsonAsync<NutritionProfileDto>(Path);
        Assert.Equal(0, current!.Revision); Assert.Null(current.Profile);
    }

    [Fact]
    public async Task UnsupportedMediaTypeAndGetBody_AreRejectedWithoutWriting()
    {
        var (client, auth) = await CreateAuthenticatedClientAsync();
        using (client)
        {
            var before = await SnapshotAsync(auth.userId);
            using var form = new HttpRequestMessage(HttpMethod.Put, Path)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["heightCm"] = "170" }),
            };
            form.Headers.Add(RevisionHeader, "0");
            using var unsupported = await client.SendAsync(form);
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupported.StatusCode);
            using var get = new HttpRequestMessage(HttpMethod.Get, Path) { Content = JsonContent.Create(new { userId = 2 }) };
            using var response = await client.SendAsync(get);
            await AssertErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(before, await SnapshotAsync(auth.userId));
        }
    }

    [Fact]
    public async Task OpenApi_MatchesRevisionStrictRequestAndSeparateResponse()
    {
        using var json = JsonDocument.Parse(await Client.GetStringAsync("/swagger/v1/swagger.json"));
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var dto = schemas.GetProperty("NutritionProfileDto");
        Assert.Equal("string", dto.GetProperty("properties").GetProperty("revision").GetProperty("type").GetString());
        var request = schemas.GetProperty("NutritionProfileRequest");
        Assert.False(request.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(4, request.GetProperty("required").GetArrayLength());
        Assert.Equal(2, request.GetProperty("properties").GetProperty("calculationSex").GetProperty("enum").GetArrayLength());
        var operation = json.RootElement.GetProperty("paths").GetProperty(Path).GetProperty("put");
        Assert.Contains(operation.GetProperty("parameters").EnumerateArray(), p =>
            p.GetProperty("name").GetString() == RevisionHeader && p.GetProperty("required").GetBoolean());
        Assert.Equal(2, schemas.GetProperty("CurrentUserProfileDto").GetProperty("properties").EnumerateObject().Count());
    }

    private sealed class LookupPlan { public string? Key { get; set; } }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(status == response.StatusCode, $"Expected {status}, got {response.StatusCode}: {text}");
        var problem = JsonSerializer.Deserialize<ProblemDetails>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(problem);
        Assert.Equal(code, ((JsonElement)problem.Extensions["code"]!).GetString());
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
