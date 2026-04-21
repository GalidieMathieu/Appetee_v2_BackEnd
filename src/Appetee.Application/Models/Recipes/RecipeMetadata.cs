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
        public const string FreezerFriendly = "freezer-friendly";
        public const string BudgetFocused = "budget-focused";
        public const string HighProtein = "high-protein";

        public static bool IsValid(string badge) =>
            badge is FreezerFriendly or BudgetFocused or HighProtein;
    }
}
