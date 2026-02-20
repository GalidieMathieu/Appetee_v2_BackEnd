using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Infrastructure.Auth
{
    internal static class AuthSql
    {
        public const string InsertUser = """
        INSERT INTO users (username, display_name, email, password_hash, image_url, created_at, updated_at)
        VALUES (@Username, @DisplayName, @Email, @PasswordHash, @ImageUrl, UTC_TIMESTAMP(), UTC_TIMESTAMP());
        """;

        public const string LastInsertId = "SELECT LAST_INSERT_ID();";

        // ONE session per user: user_id is PK, session_token is UNIQUE
        public const string InsertSession = """
        INSERT INTO authentication (user_id, session_token, expires_at, revoked_at)
        VALUES (@UserId, @SessionToken, @ExpiresAtUtc, NULL)
        ON DUPLICATE KEY UPDATE
            session_token = VALUES(session_token),
            expires_at = VALUES(expires_at),
            revoked_at = NULL;
        """;

        public const string RevokeSessionByToken = """
        UPDATE authentication
        SET revoked_at = UTC_TIMESTAMP()
        WHERE session_token = @SessionToken
          AND revoked_at IS NULL;
        """;

        public const string GetUserIdByToken = """
        SELECT user_id
        FROM authentication
        WHERE session_token = @SessionToken
          AND revoked_at IS NULL
          AND expires_at > UTC_TIMESTAMP()
        LIMIT 1;
        """;

        public const string GetUserForLogin = """
        SELECT
            id AS Id,
            username AS Username,
            display_name AS DisplayName,
            email AS Email,
            password_hash AS PasswordHash,
            image_url AS ImageUrl
        FROM users
        WHERE email = @Identifier OR username = @Identifier
        LIMIT 1;
        """;

        public const string GetUserById = """
        SELECT
            id AS Id,
            username AS Username,
            display_name AS DisplayName,
            email AS Email,
            image_url AS ImageUrl
        FROM users
        WHERE id = @UserId
        LIMIT 1;
        """;
    }
}
