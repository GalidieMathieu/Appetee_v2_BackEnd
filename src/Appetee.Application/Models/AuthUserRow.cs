using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Models
{
    // Internal auth read model (includes password hash). Never return this from controllers.
    public sealed record AuthUserRow(
        int Id,
        string Username,
        string DisplayName,
        string Email,
        string PasswordHash,
        string? ImageUrl
    );
}
