namespace Appetee.Infrastructure.Users;

internal static class UserSql
{
    internal const string GetCurrentProfile = """
        SELECT
            username  AS Username,
            image_url AS ImageUrl
        FROM users
        WHERE id = @currentUserId
        LIMIT 1;
    """;

    internal const string UpdateCurrentProfile = """
        UPDATE users
        SET
            username  = COALESCE(@username, username),
            image_url = COALESCE(@imageUrl, image_url)
        WHERE id = @currentUserId;
    """;

    // Used by transactional sign-up preference persistence.
    internal const string DeleteDietFromUserById = """
        DELETE FROM user_diets
        WHERE user_id = @id;
    """;

    // Used by transactional sign-up restriction persistence.
    internal const string DeleteIngredientUserByID = """
        DELETE FROM user_ingredient_restrictions
        WHERE user_id = @id;
    """;
}
