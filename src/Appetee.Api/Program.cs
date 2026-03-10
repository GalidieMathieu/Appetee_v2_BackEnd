using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Abstractions.Diets;
using Appetee.Application.Abstractions.Ingredients;
using Appetee.Application.Abstractions.Users;
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

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularFront", p =>
        p.WithOrigins("https://localhost:4200")
         .AllowAnyHeader()
         .AllowAnyMethod()
    .AllowCredentials()); //for cookie, later

});


//cookie
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-appetee";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None; // or None if cross-site
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

builder.Services.AddAuthorization();


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
builder.Services.AddScoped<IAuthQueries, AuthQueries>();
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



var app = builder.Build();
app.UseExceptionHandler("/error");

app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment()) app.UseHsts();

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
