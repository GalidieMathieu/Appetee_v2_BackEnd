internal static class DietSql
{
    internal const string GetById = """
        SELECT
            id            AS Id,
            name          AS name
        FROM diets
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string GetAll = """
        SELECT * from diets
        ORDER BY id DESC;
        """;
    internal const string GetSomeByIds ="""
        SELECT id FROM diets WHERE id IN @Ids;
        """;

    internal const string Create = """
        INSERT INTO diets (name)
        VALUES (@name);
        SELECT LAST_INSERT_ID();
    """;

    internal const string UpdateProfile = """
        UPDATE diets
        SET
            name = COALESCE(@name, name)
        WHERE id = @id;
    """;


    internal const string DeleteById = """
        DELETE FROM diets
        WHERE id = @id;
    """;
}
