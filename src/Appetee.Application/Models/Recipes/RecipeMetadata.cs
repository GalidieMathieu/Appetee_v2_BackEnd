namespace Appetee.Application.Models.Recipes
{
    public enum RecipeDifficulty
    {
        Easy,
        Medium,
        Hard,
    }

    public static class RecipeBadgeValues
    {
        public const string HighProtein = "High Protein";
        public const string LowCalorie = "Low Calorie";
        public const string LowCarb = "Low Carb";
        public const string HighFiber = "High Fiber";
        public const string QuickMeal = "Quick Meal";
        public const string MealPrep = "Meal Prep";
        public const string FreezerFriendly = "Freezer Friendly";
        public const string BudgetFriendly = "Budget Friendly";
        public const string FewIngredients = "Few Ingredients";

        private static readonly string[] OrderedValues =
        [
            HighProtein,
            LowCalorie,
            LowCarb,
            HighFiber,
            QuickMeal,
            MealPrep,
            FreezerFriendly,
            BudgetFriendly,
            FewIngredients,
        ];

        public static IReadOnlyList<string> All => OrderedValues;

        public static bool IsValid(string badge) =>
            GetPriority(badge) is not null;

        public static int? GetPriority(string badge)
        {
            var index = Array.IndexOf(OrderedValues, badge);
            return index < 0 ? null : index + 1;
        }

        public static IEnumerable<string> Order(IEnumerable<string> badges) =>
            badges.OrderBy(badge => GetPriority(badge) ?? int.MaxValue);
    }
}
