using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;

namespace Appetee.Application.Services.MealPlans;

public interface INutritionCalculationService
{
    NutritionCalculationDto Calculate(NutritionCalculationRequest request, CancellationToken ct = default);
}
