using Appetee.Infrastructure.Data;

namespace Appetee.Infrastructure.Auth
{
    public sealed class AuthQueries //: IAuthQueries
    {
        private readonly IDbConnectionFactory _db;
        public AuthQueries(IDbConnectionFactory db) => _db = db;

        
    }
}
