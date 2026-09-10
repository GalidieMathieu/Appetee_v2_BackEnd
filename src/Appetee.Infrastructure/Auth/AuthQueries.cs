using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.utils;
using Appetee.Infrastructure.Data;
using Dapper;

namespace Appetee.Infrastructure.Auth
{
    public sealed class AuthQueries : IAuthQueries
    {
        private readonly IDbConnectionFactory _db;
        private readonly IPasswordHasher _passwordHasher;
        public AuthQueries(IDbConnectionFactory db , IPasswordHasher passwordHasher)
        {
            _db = db;
            _passwordHasher = passwordHasher;
        } 

        public async Task<LoginAttempt> LoginAsync(LoginRequest user , CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var row = await conn.QuerySingleOrDefaultAsync<LoginRow>(
                new CommandDefinition(AuthSql.getUserForLogIn, new { user.Email }, cancellationToken: ct)
            );


            if (row is null)
            {
                return LoginAttempt.InvalidCredentials();
            }

            if(!_passwordHasher.Verify(user.Password ,row.PasswordHash))
            {
                return LoginAttempt.InvalidCredentials();
            }

            return LoginAttempt.Authenticated(new AuthResult(row.Id , row.Username));
        }

        public async Task<bool> ExistsByEmailAsync(
            string email,
            CancellationToken ct)
        {
            using var connection = await _db.CreateOpenConnectionAsync(ct);

            return await connection.QuerySingleAsync<bool>(
                new CommandDefinition(
                    AuthSql.EmailExists,
                    new { email },
                    cancellationToken: ct));
        }

    }
}
