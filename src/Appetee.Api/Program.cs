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
using System.Diagnostics;
using System.Reflection;

try
{
    var builder = WebApplication.CreateBuilder(args);

    /*
     * WebApplication.CreateBuilder already registers the default logging
     * providers, including Console.
     *
     * AddSimpleConsole configures the existing console logging provider so:
     * - Every log appears on one line in Azure Log Stream.
     * - Logging scopes such as TraceId and RequestPath are displayed.
     */
    builder.Logging.AddSimpleConsole(options =>
    {
        options.IncludeScopes = true;
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
    });

    // --------------------------------------------------
    // Configuration values
    // --------------------------------------------------

    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?? Array.Empty<string>();

    var cookieName =
        builder.Configuration["Authentication:CookieName"]
        ?? "__Host-appetee";

    var cookieExpireDays =
        builder.Configuration.GetValue(
            "Authentication:ExpireDays",
            14);

    var slidingExpiration =
        builder.Configuration.GetValue(
            "Authentication:SlidingExpiration",
            true);

    var storageAccountUri =
        GetRequiredAzureStorageAccountUri(
            builder.Configuration,
            "AzureStorage:AccountUrl");

    GetRequiredConfigurationValue(
        builder.Configuration,
        "AzureStorage:ContainerName");

    /*
     * A production application using credentialed CORS requests should not
     * silently fall back to allowing every origin.
     */
    if (!builder.Environment.IsDevelopment()
        && allowedOrigins.Length == 0)
    {
        throw new InvalidOperationException(
            "Missing production CORS configuration. " +
            "Set Cors:AllowedOrigins or environment variables such as " +
            "'Cors__AllowedOrigins__0'.");
    }

    // --------------------------------------------------
    // Core services
    // --------------------------------------------------

    builder.Services.AddControllers();

    // --------------------------------------------------
    // Swagger / OpenAPI
    // --------------------------------------------------

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "Appetee API",
                Version = "v1"
            });

        var xmlFile =
            $"{Assembly.GetEntryAssembly()?.GetName().Name}.xml";

        var xmlPath =
            Path.Combine(AppContext.BaseDirectory, xmlFile);

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    });

    // --------------------------------------------------
    // CORS
    // --------------------------------------------------

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AngularFront", policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                /*
                 * Development-only fallback.
                 *
                 * This does not allow credentialed cross-origin cookies.
                 * Configure an exact origin when testing cookie authentication
                 * from a separate Angular development server.
                 */
                policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                return;
            }

            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });

    // --------------------------------------------------
    // Authentication
    // --------------------------------------------------

    builder.Services
        .AddAuthentication(
            CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.Name = cookieName;
            options.Cookie.Path = "/";
            options.Cookie.HttpOnly = true;

            // Required when the Angular frontend and API are cross-site.
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy =
                CookieSecurePolicy.Always;

            options.ExpireTimeSpan =
                TimeSpan.FromDays(cookieExpireDays);

            options.SlidingExpiration =
                slidingExpiration;

            // APIs should return status codes rather than HTML redirects.
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;

                return Task.CompletedTask;
            };

            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;

                return Task.CompletedTask;
            };
        });

    builder.Services.AddAuthorization();

    // --------------------------------------------------
    // Azure Blob Storage
    // --------------------------------------------------

    var isDevelopment =
        builder.Environment.IsDevelopment();

    builder.Services.AddSingleton(_ =>
    {
        /*
         * Local development:
         * DefaultAzureCredential can use Visual Studio, Azure CLI,
         * environment credentials, or interactive authentication.
         *
         * Azure App Service:
         * DefaultAzureCredential uses the App Service managed identity.
         *
         * The App Service identity must have:
         * Storage Blob Data Contributor
         *
         * Assign the role on the storage account to cover all containers,
         * or on an individual container for narrower permissions.
         */
        var credential = new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                /*
                 * A production web application must never attempt an
                 * interactive browser login.
                 */
                ExcludeInteractiveBrowserCredential =
                    !isDevelopment
            });

        return new BlobServiceClient(
            storageAccountUri,
            credential);
    });

    builder.Services.AddScoped<
        IBlobStorageService,
        BlobStorageService>();

    // --------------------------------------------------
    // Database
    // --------------------------------------------------

    var connectionString =
        GetRequiredConnectionString(
            builder.Configuration);

    builder.Services.AddScoped<IDbConnectionFactory>(
        _ => new DbConnectionFactory(connectionString));

    // --------------------------------------------------
    // Infrastructure implementations
    // --------------------------------------------------

    // Users
    builder.Services.AddScoped<
        IUserQueries,
        UserQueries>();

    builder.Services.AddScoped<
        IUserRepository,
        UserRepository>();

    // Authentication
    builder.Services.AddScoped<
        IAuthRepository,
        AuthRepository>();

    builder.Services.AddScoped<
        IAuthQueries,
        AuthQueries>();

    builder.Services.AddSingleton<
        IPasswordHasher,
        AspNetIdentityPasswordHasher>();

    builder.Services.AddSingleton<
        IAuthCookieService,
        AuthCookieService>();

    // Diets
    builder.Services.AddScoped<
        IDietQueries,
        DietQueries>();

    // Ingredients
    builder.Services.AddScoped<
        IIngredientQueries,
        IngredientQueries>();

    // Recipes
    builder.Services.AddScoped<
        IRecipeQueries,
        RecipeQueries>();

    // --------------------------------------------------
    // Application implementations
    // --------------------------------------------------

    // Users
    builder.Services.AddScoped<
        IUserService,
        UserService>();

    // Authentication
    builder.Services.AddScoped<
        IAuthService,
        AuthService>();

    // Diets
    builder.Services.AddScoped<
        IDietService,
        DietService>();

    // Ingredients
    builder.Services.AddScoped<
        IIngredientService,
        IngredientService>();

    // Recipes
    builder.Services.AddScoped<
        IRecipeService,
        RecipeService>();

    // --------------------------------------------------
    // Build application
    // --------------------------------------------------

    var app = builder.Build();

    // --------------------------------------------------
    // Request logging context
    // --------------------------------------------------

    /*
     * This scope is created before the exception handler so all logs for the
     * request, including ErrorsController logs, receive the same context.
     */
    app.Use(async (context, next) =>
    {
        var traceId =
            Activity.Current?.TraceId.ToString();

        if (string.IsNullOrWhiteSpace(traceId))
        {
            traceId = context.TraceIdentifier;
        }

        using var scope = app.Logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["TraceId"] = traceId,
                ["RequestPath"] =
                    context.Request.Path.Value
                    ?? string.Empty,
                ["HttpMethod"] =
                    context.Request.Method
            });

        await next();
    });

    // --------------------------------------------------
    // Global error handling
    // --------------------------------------------------

    if (app.Environment.IsDevelopment())
    {
        /*
         * Provides detailed local debugging information.
         * Never enable the developer exception page in production.
         */
        app.UseDeveloperExceptionPage();
    }
    else
    {
        /*
         * Re-executes failed requests through /error.
         * ErrorsController returns a safe ProblemDetails response.
         */
        app.UseExceptionHandler("/error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();

    // --------------------------------------------------
    // Security headers
    // --------------------------------------------------

    app.Use(async (context, next) =>
    {
        context.Response.Headers.TryAdd(
            "X-Content-Type-Options",
            "nosniff");

        context.Response.Headers.TryAdd(
            "X-Frame-Options",
            "DENY");

        context.Response.Headers.TryAdd(
            "Referrer-Policy",
            "no-referrer");

        await next();
    });

    // --------------------------------------------------
    // Development Swagger
    // --------------------------------------------------

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint(
                "/swagger/v1/swagger.json",
                "Appetee API v1");
        });
    }

    // --------------------------------------------------
    // Request pipeline
    // --------------------------------------------------

    app.UseRouting();

    app.UseCors("AngularFront");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    /*
     * Exceptions occurring before app.Run cannot be processed by
     * ErrorsController because the HTTP pipeline has not started.
     *
     * stderr is captured by Azure App Service logging.
     */
    var environmentName =
        Environment.GetEnvironmentVariable(
            "ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    Console.Error.WriteLine(
        $"Application startup failed in environment " +
        $"'{environmentName}'.");

    Console.Error.WriteLine(ex);

    throw;
}

static string GetRequiredConfigurationValue(
    IConfiguration configuration,
    string key)
{
    var value = configuration[key];

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException(
            $"Missing configuration: {key}. " +
            $"Set it in appsettings or as environment variable " +
            $"'{ToEnvironmentVariableName(key)}'.");
    }

    return value;
}

static Uri GetRequiredAzureStorageAccountUri(
    IConfiguration configuration,
    string key)
{
    var value =
        GetRequiredConfigurationValue(
            configuration,
            key);

    if (!Uri.TryCreate(
            value,
            UriKind.Absolute,
            out var uri))
    {
        throw new InvalidOperationException(
            "Invalid Azure Storage account URL.");
    }

    var isHttps =
        string.Equals(
            uri.Scheme,
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);

    var isAzureBlobHost =
        !string.IsNullOrWhiteSpace(uri.Host)
        && uri.Host.EndsWith(
            ".blob.core.windows.net",
            StringComparison.OrdinalIgnoreCase);

    var containsQuery =
        !string.IsNullOrWhiteSpace(uri.Query);

    var containsUserInformation =
        !string.IsNullOrWhiteSpace(uri.UserInfo);

    if (!isHttps
        || !isAzureBlobHost
        || containsQuery
        || containsUserInformation)
    {
        throw new InvalidOperationException(
            "Invalid Azure Storage account URL. " +
            "Expected an HTTPS Blob service URL such as " +
            "'https://account.blob.core.windows.net'.");
    }

    /*
     * The configured value must represent the storage account Blob endpoint,
     * not a specific container or Blob.
     */
    if (!string.IsNullOrEmpty(
            uri.AbsolutePath.Trim('/')))
    {
        uri = new UriBuilder(uri)
        {
            Path = string.Empty
        }.Uri;
    }

    return uri;
}

static string GetRequiredConnectionString(
    IConfiguration configuration)
{
    var connectionString =
        configuration.GetConnectionString(
            "AppeteeDb");

    if (!string.IsNullOrWhiteSpace(
            connectionString))
    {
        return connectionString;
    }

    connectionString =
        configuration.GetConnectionString(
            "Default");

    if (!string.IsNullOrWhiteSpace(
            connectionString))
    {
        return connectionString;
    }

    throw new InvalidOperationException(
        "Missing connection string: AppeteeDb or Default. " +
        "Set 'ConnectionStrings__AppeteeDb' in " +
        "App Service configuration.");
}

static string ToEnvironmentVariableName(
    string key)
{
    return key.Replace(":", "__");
}

// Expose Program for WebApplicationFactory integration tests.
public partial class Program
{
}
