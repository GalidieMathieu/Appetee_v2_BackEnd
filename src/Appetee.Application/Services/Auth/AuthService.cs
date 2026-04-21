using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Models.Auth;
using Appetee.Application.Requests.Auth;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Http;
using System.Net.Mail;

namespace Appetee.Application.Services.Auth
{
    public sealed class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        private readonly IAuthQueries _authQueries;

        private readonly IAuthCookieService _cookieService;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            IAuthRepository authRepository,
            IAuthQueries authQueries,
            IUserQueries userQueries,
            IPasswordHasher passwordHasher,
            IAuthCookieService cookieService)
        {
            _authRepository = authRepository;
            _authQueries = authQueries;
            _passwordHasher = passwordHasher;
            _cookieService = cookieService;
        }

        public async Task<AuthResult> SignUpAsync(HttpContext http, SignUpRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Username))
                throw new ValidationException("Username is required.");

            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ValidationException("Email is required.");

            if (!MailAddress.TryCreate(request.Email, out _))
                throw new ValidationException("Email must be valid.");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ValidationException("Password is required.");

            var passwordHash = _passwordHasher.Hash(request.Password);
            var username = request.Username.Trim();
            var userId = await _authRepository.CreateUserAsync(
                username,
                request.Email.Trim(),
                passwordHash,
                request.DietIds,
                request.IngredientRestrictionIds,
                ct);

            //if USer is ok then 
            await _cookieService.SignInAsync(http, userId, username);

            // You can also map from request, but querying keeps canonical DB values.

            return new AuthResult(userId, username);

        }


        public async Task<AuthResult> LogInAsync(HttpContext http, LoginRequest request, CancellationToken ct)
        {
            // Minimal validation (keep consistent with your existing validation approach)
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new ValidationException("Email is required.");

            if (!MailAddress.TryCreate(request.Email, out _))
                throw new ValidationException("Email must be valid.");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ValidationException("Password is required.");

            AuthResult userAuth = await _authQueries.LoginAsync(request, ct);

            if(userAuth is null)
            {
                throw new UnauthorizedException("Invalid credentials.");
            }

            if(userAuth.userId > 0)
                await _cookieService.SignInAsync(http, userAuth.userId, userAuth.userName);

            return userAuth;
        }

        public Task LogOutAsync(HttpContext http, CancellationToken ct) =>
            _cookieService.SignOutAsync(http);

        public UserSessionDto? GetSession(HttpContext context)
        {
            var userSess = context?.User;
            if (userSess is null) throw new UnauthorizedException();
            return _cookieService.GetSession(userSess);
        }
    }
}
