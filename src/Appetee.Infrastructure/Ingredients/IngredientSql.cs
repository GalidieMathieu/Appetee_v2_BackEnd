internal static class IngredientSql
{
    internal const string GetById = """
        SELECT
            id            AS id,
            name          AS name
        FROM ingredients
        WHERE id = @id
        LIMIT 1;
    """;

    internal const string GetAll = """
        SELECT id as id, name as name from ingredients
        ORDER BY id DESC;
        """;

    internal const string GetSomeByIds = """
        SELECT id FROM ingredients WHERE id IN @Ids;
        """;

    internal const string deleteAll = """DELETE FROM ingredients WHERE id = @id""";
}