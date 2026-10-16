using Appetee.Application.utils;
using Microsoft.Extensions.Primitives;
using System.Globalization;

namespace Appetee.Api.Helpers;

/// <summary>Parses the originating Meal Plan revision without a JSON-number round trip.</summary>
public static class MealPlanRevisionHeader
{
    public const string HeaderName = "X-Meal-Plan-Revision";

    public static long Parse(IHeaderDictionary headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (!headers.TryGetValue(HeaderName, out var values) || values.Count == 0)
        {
            throw CreateValidationException(
                "The X-Meal-Plan-Revision header is required.",
                "revision_required");
        }

        return Parse(values);
    }

    internal static long Parse(StringValues values)
    {
        if (values.Count != 1
            || string.IsNullOrEmpty(values[0])
            || !values[0]!.All(character => character is >= '0' and <= '9')
            || !long.TryParse(
                values[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var revision))
        {
            throw CreateValidationException(
                "The X-Meal-Plan-Revision header must be a nonnegative signed-BIGINT decimal string.",
                "revision_invalid");
        }

        return revision;
    }

    private static ValidationException CreateValidationException(string message, string code) =>
        new(
            message,
            code,
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["revision"] = [message],
            });
}
