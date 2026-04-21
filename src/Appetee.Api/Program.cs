using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Abstractions.Recipes;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.Diets;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.Services.Recipes;
using Appetee.Application.Services.Users;
using Appetee.Infrastructure.Auth;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Diets;
using Appetee.Infrastructure.Ingredients;
using Appetee.Infrastructure.Recipes;
using Appetee.Infrastructure.Users;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using System.Reflection;

try
{
    var builder = WebApplication.CreateBuilder(args);

// --------------------------------------------------
// Configuration values (from appsettings.json / environment)
// --------------------------------------------------

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

var cookieName = builder.Configuration["Authentication:CookieName"] ?? "__Host-appetee";
var cookieExpireDays = builder.Configuration.GetValue<int>("Authentication:ExpireDays", 14);
var slidingExpiration = builder.Configuration.GetValue<bool>("Authentication:SlidingExpiration", true);

var storageAccountUri = GetRequiredAbsoluteUri(builder.Configuration, "AzureStorage:AccountUrl");
GetRequiredConfigurationValue(builder.Configuration, "AzureStorage:ContainerName");

// --------------------------------------------------
// Core services
// --------------------------------------------------

builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Appetee API", Version = "v1" });

    // Include XML comments if project emits them
    var xmlFile = $"{Assembly.GetEntryAssembly()?.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Read config-driven CORS origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularFront", p =>
    {
        if (allowedOrigins.Length == 0)
        {
            // If no origins configured, allow any origin (no credentials).
            p.AllowAnyOrigin()
             .AllowAnyHeader()
             .AllowAnyMethod();
        }
        else
        {
            // If origins are configured, use them and allow credentials for cookies.
            p.WithOrigins(allowedOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod()
             .AllowCredentials();
        }
    });
});

// Cookie authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = cookieName;
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None; // required for cross-site cookie usage with credentials
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(cookieExpireDays);
        options.SlidingExpiration = slidingExpiration;
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

// BlobServiceClient from configured account URL
builder.Services.AddSingleton(_ =>
{
    return new BlobServiceClient(storageAccountUri, new DefaultAzureCredential());
});

builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
builder.Services.AddAuthorization();

// Connection string: prefer "AppeteeDb" (development), fall back to "Default"
var connectionString = GetRequiredConnectionString(builder.Configuration);

builder.Services.AddScoped<IDbConnectionFactory>(_ => new DbConnectionFactory(connectionString));

//##################### Infra implementation #####################
//Users
builder.Services.AddScoped<IUserQueries, UserQueries>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

//Auth
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthQueries, AuthQueries>();
builder.Services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();
builder.Services.AddSingleton<IAuthCookieService, AuthCookieService>();

//Diets
builder.Services.AddScoped<IDietQueries, DietQueries>();

//Ingredients
builder.Services.AddScoped<IIngredientQueries, IngredientQueries>();
//Recipes
builder.Services.AddScoped<IRecipeQueries, RecipeQueries>();

//##################### Application implementation #####################
// Users
builder.Services.AddScoped<IUserService, UserService>();
//Auth
builder.Services.AddScoped<IAuthService, AuthService>();
//Diets
builder.Services.AddScoped<IDietService, DietService>();
//Ingredients
builder.Services.AddScoped<IIngredientService, IngredientService>();
//Recipes
builder.Services.AddScoped<IRecipeService, RecipeService>();

var app = builder.Build();
app.UseExceptionHandler("/error");

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseHsts();

// in the request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Appetee API v1");
        // c.RoutePrefix = string.Empty; // uncomment to serve UI at app root
    });
}

// Security headers
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    ctx.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    await next();
});
app.UseRouting();

app.UseCors("AngularFront");

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
}
catch (Exception ex)
{
    var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
    Console.Error.WriteLine(
        $"Application startup failed in environment '{environmentName}'.");
    Console.Error.WriteLine(ex);
    throw;
}

static string GetRequiredConfigurationValue(IConfiguration configuration, string key)
{
    var value = configuration[key];
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Missing configuration: {key}. Set it in appsettings or as environment variable '{ToEnvironmentVariableName(key)}'.");
    }

    return value;
}

static Uri GetRequiredAbsoluteUri(IConfiguration configuration, string key)
{
    var value = GetRequiredConfigurationValue(configuration, key);

    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
    {
        throw new InvalidOperationException(
            $"Invalid configuration: {key} must be a valid absolute URI.");
    }

    if (!string.IsNullOrEmpty(uri.AbsolutePath.Trim('/')))
    {
        uri = new UriBuilder(uri) { Path = string.Empty }.Uri;
    }

    return uri;
}

static string GetRequiredConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("AppeteeDb");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    connectionString = configuration.GetConnectionString("Default");
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        return connectionString;
    }

    throw new InvalidOperationException(
        "Missing connection string: AppeteeDb or Default. Set 'ConnectionStrings__AppeteeDb' in App Service configuration.");
}

static string ToEnvironmentVariableName(string key) => key.Replace(":", "__");

// Expose Program for WebApplicationFactory integration tests.
public partial class Program { }
