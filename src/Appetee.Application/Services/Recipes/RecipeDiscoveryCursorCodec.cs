// Purpose: Encodes and validates private versioned Recipe Discovery cursors.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-25T23:50:21-06:00

using Appetee.Application.Models.Recipes;
using Appetee.Application.utils;
using System.Text;
using System.Text.Json;

namespace Appetee.Application.Services.Recipes;

/// <summary>Converts validated browse/search cursor state to and from opaque Base64URL values.</summary>
internal static class RecipeDiscoveryCursorCodec
{
    internal const int CurrentVersion = 1;
    internal const string BrowseMode = "browse";
    internal const string SearchMode = "search";
    internal const int MaxEncodedLength = 512;
    private const int Sha256Base64UrlLength = 43;

    internal static string Encode(BrowseCursorV1 cursor)
    {
        Validate(cursor);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", cursor.V);
            writer.WriteString("mode", cursor.Mode);
            writer.WriteNumber("seed", cursor.Seed);
            writer.WriteNumber("rank", cursor.Rank);
            writer.WriteNumber("id", cursor.Id);
            writer.WriteString("criteria", cursor.Criteria);
            writer.WriteEndObject();
        }

        return EncodeBase64Url(stream.ToArray());
    }

    internal static string Encode(SearchCursorV1 cursor)
    {
        Validate(cursor);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", cursor.V);
            writer.WriteString("mode", cursor.Mode);
            writer.WriteNumber("rank", cursor.Rank);
            writer.WriteNumber("id", cursor.Id);
            writer.WriteString("criteria", cursor.Criteria);
            writer.WriteEndObject();
        }

        return EncodeBase64Url(stream.ToArray());
    }

    internal static BrowseCursorV1 DecodeBrowse(string encoded)
    {
        using var document = DecodeDocument(encoded);
        var root = document.RootElement;
        RequireExactShape(root, "v", "mode", "seed", "rank", "id", "criteria");

        var cursor = new BrowseCursorV1(
            ReadInt32(root, "v"),
            ReadString(root, "mode"),
            ReadInt32(root, "seed"),
            ReadInt64(root, "rank"),
            ReadInt32(root, "id"),
            ReadString(root, "criteria"));

        Validate(cursor);
        return cursor;
    }

    internal static SearchCursorV1 DecodeSearch(string encoded)
    {
        using var document = DecodeDocument(encoded);
        var root = document.RootElement;
        RequireExactShape(root, "v", "mode", "rank", "id", "criteria");

        var cursor = new SearchCursorV1(
            ReadInt32(root, "v"),
            ReadString(root, "mode"),
            ReadInt64(root, "rank"),
            ReadInt32(root, "id"),
            ReadString(root, "criteria"));

        Validate(cursor);
        return cursor;
    }

    internal static string EncodeBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static JsonDocument DecodeDocument(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > MaxEncodedLength)
            throw InvalidCursor();

        try
        {
            var bytes = DecodeBase64Url(encoded);
            return JsonDocument.Parse(
                bytes,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4,
                });
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or OverflowException)
        {
            throw InvalidCursor();
        }
    }

    private static byte[] DecodeBase64Url(string encoded)
    {
        if (encoded.Any(character =>
                !char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_'))
        {
            throw new FormatException();
        }

        var remainder = encoded.Length % 4;
        if (remainder == 1)
            throw new FormatException();

        var padded = encoded
            .Replace('-', '+')
            .Replace('_', '/');
        if (remainder > 0)
            padded = padded.PadRight(encoded.Length + (4 - remainder), '=');

        return Convert.FromBase64String(padded);
    }

    private static void RequireExactShape(
        JsonElement root,
        params string[] expectedPropertyNames)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw InvalidCursor();

        var remaining = expectedPropertyNames.ToHashSet(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
                throw InvalidCursor();
        }

        if (remaining.Count != 0)
            throw InvalidCursor();
    }

    private static int ReadInt32(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || !property.TryGetInt32(out var value))
        {
            throw InvalidCursor();
        }

        return value;
    }

    private static long ReadInt64(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || !property.TryGetInt64(out var value))
        {
            throw InvalidCursor();
        }

        return value;
    }

    private static string ReadString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            throw InvalidCursor();
        }

        return property.GetString() ?? throw InvalidCursor();
    }

    private static void Validate(BrowseCursorV1 cursor)
    {
        if (cursor.V != CurrentVersion
            || !string.Equals(cursor.Mode, BrowseMode, StringComparison.Ordinal)
            || cursor.Seed <= 0
            || cursor.Rank is < 0 or > uint.MaxValue
            || cursor.Id <= 0
            || !IsValidCriteria(cursor.Criteria))
        {
            throw InvalidCursor();
        }
    }

    private static void Validate(SearchCursorV1 cursor)
    {
        if (cursor.V != CurrentVersion
            || !string.Equals(cursor.Mode, SearchMode, StringComparison.Ordinal)
            || cursor.Rank < 0
            || cursor.Id <= 0
            || !IsValidCriteria(cursor.Criteria))
        {
            throw InvalidCursor();
        }
    }

    private static bool IsValidCriteria(string criteria) =>
        criteria.Length == Sha256Base64UrlLength
        && criteria.All(character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_');

    private static ValidationException InvalidCursor() =>
        new("cursor is invalid.");
}
