using Appetee.Application.Requests.MealPlans;
using Appetee.Application.utils;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace Appetee.Application.Models.MealPlans;

/// <summary>Owns the approved static structural limits shared by Meal Plan use cases.</summary>
public static class MealPlanRules
{
    public const int MaxPlansPerUser = 50;
    public const int MinDefaultMealsPerDay = 1;
    public const int MaxDefaultMealsPerDay = 8;
    public const int MaxMealsPerDay = 12;
    public const int MaxItemsPerMeal = 20;
    public const int MinRecipeQuantity = 1;
    public const int MaxRecipeQuantity = 20;
    public const int MaxPlanNameLength = 80;
    public const int MaxMealNameLength = 80;
    public const int MaxManualItemNameLength = 120;
    public const int SqlCommandTimeoutSeconds = 15;
    public const int CopyOperationTimeoutSeconds = 30;

    public static void ValidateNutritionProfile(NutritionProfileRequest request, int calculationYear)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.CalculationSex is not ("male" or "female"))
            errors["calculationSex"] = ["Choose male or female."];
        var age = (long)calculationYear - request.BirthYear;
        if (request.BirthYear is < 1900 or > 9999 || age is < 18 or > 65)
            errors["birthYear"] = ["Age this year must be between 18 and 65."];
        if (age == 18 && !request.AdultConfirmed)
            errors["adultConfirmed"] = ["Confirm that you are at least 18 years old."];
        if (request.HeightCm is < 120m or > 230m || Scale(request.HeightCm) > 2)
            errors["heightCm"] = ["Height must be 120–230 cm with at most two decimal places."];
        if (request.WeightKg is < 35m or > 250m || Scale(request.WeightKg) > 2)
            errors["weightKg"] = ["Weight must be 35–250 kg with at most two decimal places."];
        if (errors.Count > 0)
            throw new ValidationException("Nutrition profile validation failed.", "validation_failed", errors);
    }

    private static int Scale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xff;

    public static void ValidateOrder(IReadOnlyList<int>? ids,int maximum)
    {
        if(ids is null || ids.Count>maximum || ids.Any(id=>id<=0) || ids.Distinct().Count()!=ids.Count)
            throw InvalidPlanField("orderedIds","Supply a bounded list of unique positive signed integer IDs.");
    }

    public static void ValidateOrderMembership(IReadOnlyList<int> ids,IReadOnlyList<int> current)
    {
        if(ids.Count!=current.Count || !ids.ToHashSet().SetEquals(current))
            throw InvalidPlanField("orderedIds","Supply the complete current membership of this saved parent.");
    }

    public static void ValidateMealTarget(MealTargetRequest? target)
    {
        if(target is null || target.HasMealId==target.HasDefaultSlot)
            throw InvalidPlanField("meal","Supply exactly one mealId or defaultSlot.");
        if(target.HasMealId && target.MealId is not >0)
            throw InvalidPlanField("meal.mealId","Use a positive signed integer meal ID.");
        if(target.HasDefaultSlot && target.DefaultSlot is not (>=1 and <=8))
            throw InvalidPlanField("meal.defaultSlot","Use an integer default slot from 1 to 8.");
    }

    public static void ValidateRecipeQuantity(int quantity)
    {
        if(quantity is < MinRecipeQuantity or > MaxRecipeQuantity)
            throw InvalidPlanField("quantity","Use a whole serving count from 1 to 20.");
    }

    public static ManualItemRequest NormalizeManualItem(ManualItemRequest? request,string prefix="")
    {
        if(request is null)throw InvalidPlanField(prefix.TrimEnd('.'),"Supply a complete manual item.");
        var errors=new Dictionary<string,string[]>(StringComparer.Ordinal);
        var name=request.Name;
        var invalid=name is null;
        var remaining=name.AsSpan();
        while(!remaining.IsEmpty)
        {
            if(Rune.DecodeFromUtf16(remaining,out var rune,out var consumed)!=OperationStatus.Done)
            {invalid=true;break;}
            if(Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator)
                invalid=true;
            remaining=remaining[consumed..];
        }
        var normalized=invalid ? "" : name!.Trim().Normalize(NormalizationForm.FormC);
        if(invalid || normalized.EnumerateRunes().Count() is <1 or >MaxManualItemNameLength)
            errors[prefix+"name"]=["Use a plain-text name of 1–120 Unicode scalar values without line/control characters."];
        Validate("caloriesKcal",request.CaloriesKcal,20000m);
        Validate("proteinG",request.ProteinG,5000m);
        Validate("carbsG",request.CarbsG,5000m);
        Validate("fatG",request.FatG,5000m);
        if(errors.Count>0)throw new ValidationException("Manual item validation failed.","validation_failed",errors);
        return request with {Name=normalized};

        void Validate(string field,decimal? value,decimal maximum)
        {
            if(value is null || value<.01m || value>maximum || Scale(value.Value)>2)
                errors[prefix+field]=["Use a strictly positive value within the documented bound, with at most two decimal places."];
        }
    }

    public static string NormalizePlanName(string? name)
    {
        var invalid = name is null;
        var remaining = name.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out var rune, out var consumed) != OperationStatus.Done)
            { invalid = true; break; }
            if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control
                or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator) invalid = true;
            remaining = remaining[consumed..];
        }
        if (invalid) throw InvalidPlanField("name", "Use a plain-text plan name without line or control characters.");
        var normalized = name!.Trim().Normalize(NormalizationForm.FormC);
        var count = normalized.EnumerateRunes().Count();
        if (count is < 1 or > MaxPlanNameLength)
            throw InvalidPlanField("name", "Plan name must contain 1–80 Unicode scalar values.");
        return normalized;
    }

    public static ValidationException InvalidPlanField(string field, string message) =>
        new("Nutrition plan validation failed.", "validation_failed",
            new Dictionary<string, string[]> { [field] = [message] });

    public static string DuplicatePlanName(string sourceName, int copyNumber)
    {
        if (copyNumber < 1) throw new ArgumentOutOfRangeException(nameof(copyNumber));
        var suffix = copyNumber == 1 ? " - Copy" : " - Copy " + copyNumber.ToString(CultureInfo.InvariantCulture);
        var prefix = string.Concat(sourceName.EnumerateRunes().Take(MaxPlanNameLength - suffix.Length).Select(r => r.ToString())).TrimEnd();
        return prefix + suffix;
    }
}
