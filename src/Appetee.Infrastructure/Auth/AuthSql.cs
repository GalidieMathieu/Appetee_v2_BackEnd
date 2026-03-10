using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Infrastructure.Auth
{
    internal static class AuthSql
    {
        public const string InsertUser = """
        INSERT INTO users (username, email, password_hash, created_at, updated_at)
        VALUES (@Username, @Email, @PasswordHash, UTC_TIMESTAMP(), UTC_TIMESTAMP());
        """;

        public const string LastInsertId = "SELECT LAST_INSERT_ID();";


        public const string getUserForLogIn = """
        SELECT id AS Id,username AS Username , password_hash AS PasswordHash
        FROM users
        WHERE email = @email
        LIMIT 1;
        """;
    }
}
