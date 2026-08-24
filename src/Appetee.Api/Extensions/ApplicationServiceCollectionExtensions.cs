/*
 * Purpose: Registers non-authentication application use cases outside Program.cs.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T01:18:52-06:00
 */

using Appetee.Application.Services.Diets;
using Appetee.Application.Services.Ingredients;
using Appetee.Application.Services.Recipes;
using Appetee.Application.Services.Users;

namespace Appetee.Api.Extensions;

/// <summary>Provides application-layer service registrations for the non-authentication features.</summary>
internal static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAppeteeApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDietService, DietService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IRecipeService, RecipeService>();
        return services;
    }
}
