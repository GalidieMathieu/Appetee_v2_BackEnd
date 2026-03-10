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

        public async Task<AuthResult> LoginAsync(LoginRequest user , CancellationToken ct)
        {
            using var conn = await _db.CreateOpenConnectionAsync(ct);

            var row = await conn.QuerySingleOrDefaultAsync<LoginRow>(
                new CommandDefinition(AuthSql.getUserForLogIn, new { user.Email }, cancellationToken: ct)
            );


            if (row is null)
            {
                throw new UnauthorizedException("Invalid credentials.");
            }

            if(!_passwordHasher.Verify(user.Password ,row.PasswordHash))
            {
                throw new UnauthorizedException("Invalid credentials.");
            }

            return new AuthResult(row.Id , row.Username);
        }
    }
}
