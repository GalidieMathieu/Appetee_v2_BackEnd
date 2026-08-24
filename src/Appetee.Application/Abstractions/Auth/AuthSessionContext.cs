/*
 * Purpose: Defines request-scoped authentication markers shared by the API and Application layers.
 * Created: 2026-08-21T11:12:27-06:00
 * Last updated: 2026-08-21T11:39:25-06:00
 */

namespace Appetee.Application.Abstractions.Auth;

public static class AuthSessionContext
{
    public const string ExpiredSessionItemKey = "appetee:session_expired";
}
