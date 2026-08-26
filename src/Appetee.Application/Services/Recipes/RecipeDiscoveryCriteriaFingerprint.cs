// Purpose: Produces the stable authenticated-criteria hash bound into Recipe Discovery cursors.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-25T23:50:21-06:00

using Appetee.Application.Models.Recipes;
using System.Security.Cryptography;
using System.Text.Json;

namespace Appetee.Application.Services.Recipes;

/// <summary>Hashes canonical discovery criteria so cursors cannot cross users or query shapes.</summary>
internal static class RecipeDiscoveryCriteriaFingerprint
{
    internal static string Create(RecipeDiscoveryCriteria criteria)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("userId", criteria.CurrentUserId);
            WriteStrings(writer, "searchTerms", criteria.EffectiveSearchTerms);
            WriteIntegers(writer, "ingredientIds", criteria.IngredientIds);
            writer.WriteBoolean("requireAllIngredients", criteria.RequireAllIngredients);
            WriteStrings(writer, "badges", criteria.CanonicalBadges);
            WriteNullableNumber(writer, "maxTotalMinutes", criteria.MaxTotalMinutes);
            if (criteria.MaxDifficulty.HasValue)
                writer.WriteString("maxDifficulty", criteria.MaxDifficulty.Value.ToString());
            else
                writer.WriteNull("maxDifficulty");
            writer.WriteBoolean("savedOnly", criteria.SavedOnly);
            writer.WriteNumber("limit", criteria.PageSize);
            writer.WriteEndObject();
        }

        return RecipeDiscoveryCursorCodec.EncodeBase64Url(
            SHA256.HashData(stream.ToArray()));
    }

    private static void WriteStrings(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (var value in values
                     .Select(NormalizeString)
                     .Where(value => value.Length > 0)
                     .Distinct(StringComparer.Ordinal)
                     .Order(StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    private static void WriteIntegers(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<int> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (var value in values.Distinct().Order())
            writer.WriteNumberValue(value);
        writer.WriteEndArray();
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        int? value)
    {
        if (value.HasValue)
            writer.WriteNumber(propertyName, value.Value);
        else
            writer.WriteNull(propertyName);
    }

    private static string NormalizeString(string value) =>
        string.Join(
                ' ',
                value.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();
}
