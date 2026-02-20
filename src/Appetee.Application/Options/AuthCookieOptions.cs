using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Options
{
    public sealed class AuthCookieOptions
    {
        public string CookieName { get; init; } = "appetee_session";

        // Local dev over HTTP: false. Production over HTTPS: true.
        public bool UseSecureCookies { get; init; } = false;

        // "Lax" (recommended default), "Strict", or "None"
        // If your frontend is on a different site, you will likely need "None" + Secure.
        public string SameSite { get; init; } = "Lax";

        public string Path { get; init; } = "/";

        // Optional: set in production if you need it (e.g., ".yourdomain.com")
        //public string? Domain { get; init; } = null;
    }
}
