namespace Appetee.Infrastructure.MealPlans;

internal static class MealPlanSql
{
    internal const string LockUser = """
        SELECT id
        FROM users
        WHERE id = @CurrentUserId
        FOR UPDATE;
        """;

    internal const string GetSettingsRevision = """
        SELECT revision
        FROM meal_plan_user_settings
        WHERE user_id = @CurrentUserId
        FOR UPDATE;
        """;

    internal const string InsertSettingsAtRevisionZero = """
        INSERT INTO meal_plan_user_settings (
            user_id,
            current_nutrition_plan_id,
            revision,
            created_at,
            updated_at
        ) VALUES (
            @CurrentUserId,
            NULL,
            0,
            @TimestampUtc,
            @TimestampUtc
        );
        """;

    internal const string IncrementSettingsRevision = """
        UPDATE meal_plan_user_settings
        SET revision = revision + 1,
            updated_at = @TimestampUtc
        WHERE user_id = @CurrentUserId
          AND revision = @CurrentRevision;
        """;
}
