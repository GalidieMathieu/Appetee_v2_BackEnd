namespace Appetee.Application.Models.MealPlans;

public static class MealDefaults
{
    public static IReadOnlyList<string> Names(int count) => Array.AsReadOnly(count switch
    {
        1 => new[]{"Lunch"},
        2 => ["Lunch","Dinner"],
        3 => ["Breakfast","Lunch","Dinner"],
        4 => ["Breakfast","Lunch","Afternoon Snack","Dinner"],
        5 => ["Breakfast","Morning Snack","Lunch","Afternoon Snack","Dinner"],
        6 => ["Breakfast","Morning Snack","Lunch","Afternoon Snack","Dinner","Evening Snack"],
        7 => ["Breakfast","Morning Snack","Lunch","Afternoon Snack","Dinner","Evening Snack","Late Snack"],
        8 => ["Breakfast","Morning Snack","Lunch","Afternoon Snack","Dinner","Evening Snack","Late Snack","Extra Meal"],
        _ => throw new ArgumentOutOfRangeException(nameof(count))
    });
}
