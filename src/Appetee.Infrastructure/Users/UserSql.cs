namespace Appetee.Infrastructure.Users;

internal static class UserSql
{
    internal const string GetById = """
        SELECT
            id            AS Id,
            username      AS Username,
            display_name  AS DisplayName,
            email         AS Email,
            image_url     AS ImageUrl
        FROM users
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string CheckExistByEmail = """
    SELECT EXISTS(
        SELECT 1
        FROM users
        WHERE email = @email
        LIMIT 1
    );
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

    internal const string Create = """
        INSERT INTO users (username, display_name, email, password_hash, image_url)
        VALUES (@username, @displayName, @email, @passwordHash, @imageUrl);
        SELECT LAST_INSERT_ID();
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
