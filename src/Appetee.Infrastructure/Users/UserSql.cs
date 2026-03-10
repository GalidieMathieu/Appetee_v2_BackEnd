namespace Appetee.Infrastructure.Users;

internal static class UserSql
{
    internal const string GetById = """
        SELECT
            id            AS Id,
            username      AS Username,
            email         AS Email
        FROM users
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string GetIngredientUserByID = """
        SELECT *  FROM user_ingredient_restrictions
        WHERE user_id = @id
        """;
    internal const string GetDietFromUserById = """
        SELECT *  FROM user_diets
        WHERE user_id = @id
        """;

    internal const string CheckExistByEmail = """
    SELECT EXISTS(
        SELECT 1
        FROM users
        WHERE email = @email
        LIMIT 1
    );
    """;

    internal const string GetUserWithPreferencesById = """
        SELECT
            id       AS Id,
            username AS Username,
            email    AS Email
        FROM users
        WHERE id = @id
        LIMIT 1;

        SELECT diet_id
        FROM user_diets
        WHERE user_id = @id
        ORDER BY diet_id;

        SELECT ingredient_id
        FROM user_ingredient_restrictions
        WHERE user_id = @id
        ORDER BY ingredient_id;
        """;



    internal const string List = """
        SELECT
            id           AS Id,
            username     AS Username,
            display_name AS DisplayName,
            email        AS Email,
            image_url    AS ImageUrl,
            created_at   AS CreatedAt,
            updated_at   AS UpdatedAt
        FROM users
        ORDER BY id DESC
        LIMIT @take OFFSET @skip;
    """;

    internal const string DeleteIngredientUserByID = """
        DELETE FROM user_ingredient_restrictions
        WHERE user_id = @id
        """;
    internal const string DeleteDietFromUserById = """
        DELETE FROM user_diets
        WHERE user_id = @id
        """;

    internal const string UpdateProfile = """
        UPDATE users
        SET
            display_name = COALESCE(@displayName, display_name),
            image_url    = COALESCE(@imageUrl, image_url)
        WHERE id = @id;
    """;


    internal const string DeleteById = """
        DELETE FROM users
        WHERE id = @id;
    """;
}
