using Appetee.Application.Abstractions.Auth;
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

            var normalizedRequest = request with { Email = request.Email.Trim() };

            if (!MailAddress.TryCreate(normalizedRequest.Email, out _))
                throw new ValidationException("Email must be valid.");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new ValidationException("Password is required.");

            var loginAttempt = await _authQueries.LoginAsync(normalizedRequest, ct);

            if (loginAttempt.Outcome == LoginOutcome.InvalidCredentials)
                throw new UnauthorizedException("Invalid email or password.");

            if (loginAttempt.Outcome == LoginOutcome.EmailVerificationRequired)
                throw new EmailVerificationRequiredException();

            var userAuth = loginAttempt.AuthResult
                ?? throw new InternalServerException();

            if (userAuth.userId <= 0 || string.IsNullOrWhiteSpace(userAuth.userName))
                throw new InternalServerException();

            await _cookieService.SignInAsync(
                http,
                userAuth.userId,
                userAuth.userName,
                request.RememberMe);

            return userAuth;
        }

        public Task LogOutAsync(HttpContext http, CancellationToken ct) =>
            _cookieService.SignOutAsync(http);

        public async Task<EmailExistsDto> ExistsByEmailAsync(
            string email,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ValidationException("Email is required.");

            var normalizedEmail = email.Trim();

            if (!MailAddress.TryCreate(normalizedEmail, out _))
                throw new ValidationException("Email must be valid.");

            var exists = await _authQueries.ExistsByEmailAsync(
                normalizedEmail,
                ct);

            return new EmailExistsDto(exists);
        }

        public UserSessionDto? GetSession(HttpContext context)
        {
            if (context.Items.ContainsKey(AuthSessionContext.ExpiredSessionItemKey))
                throw new SessionExpiredException();

            var userSess = context?.User;
            if (userSess is null) throw new UnauthorizedException();
            return _cookieService.GetSession(userSess);
        }
    }
}
