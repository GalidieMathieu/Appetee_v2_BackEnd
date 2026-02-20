using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Options;
using Appetee.Application.Services.Auth;
using Appetee.Application.Services.Diets;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.Services.Users;
using Appetee.Infrastructure.Auth;
using Appetee.Infrastructure.Data;
using Appetee.Infrastructure.Diets;
using Appetee.Infrastructure.Ingredients;
using Appetee.Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

//cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-appetee";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax; // or None if cross-site
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // use HTTPS
        options.ExpireTimeSpan = TimeSpan.FromDays(14); //two weeks before cookie expire TODO change to 30 minutes until user check "keep being looged in"
        options.SlidingExpiration = true;
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

builder.Services.AddControllers();

// CORS is not required for Postman.
// When you start calling this API from Angular (browser), uncomment and set your frontend origin.
 builder.Services.AddCors(options =>
 {
     options.AddPolicy("AngularFront", p =>
         p.WithOrigins("http://localhost:4200")
          .AllowAnyHeader()
          .AllowAnyMethod()
     .AllowCredentials()); //for cookie, later

 });

// Infrastructure
builder.Services.AddScoped<IDbConnectionFactory>(_ =>
    new DbConnectionFactory(builder.Configuration.GetConnectionString("AppeteeDb")
        ?? throw new InvalidOperationException("Missing connection string: AppeteeDb")));

//##################### Infra implementation #####################
//Users
builder.Services.AddScoped<IUserQueries, UserQueries>();
builder.Services.AddScoped<IUserRepository, UserRepository>();

//Auth
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddSingleton<IPasswordHasher, AspNetIdentityPasswordHasher>();
builder.Services.AddSingleton<IAuthCookieService, AuthCookieService>();

//Diets
builder.Services.AddScoped<IDietQueries, DietQueries>();

//Ingredients
builder.Services.AddScoped<IIngredientQueries, IngredientQueries>();

//##################### Application implementation #####################
// Users
builder.Services.AddScoped<IUserService, UserService>();
//Auth
builder.Services.AddScoped<IAuthService, AuthService>();
//Diets
builder.Services.AddScoped<IDietService, DietService>();
//Ingredients
builder.Services.AddScoped<IIngredientService, IngredientService>();

//##################### Options #####################
// Options binding
builder.Services.Configure<AuthSessionOptions>(
    builder.Configuration.GetSection("AuthSession"));

builder.Services.Configure<AuthCookieOptions>(
    builder.Configuration.GetSection("AuthCookie"));


var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

// Security headers (API-safe baseline)
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    ctx.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    ctx.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    // Optional for pure API:
    // ctx.Response.Headers.TryAdd("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none';");
    await next();
});
app.UseRouting();

app.UseCors("AngularFront");

app.MapControllers();
app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler("/error");

app.Run();
