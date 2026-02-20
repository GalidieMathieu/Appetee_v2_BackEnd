using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Abstractions.Users;
using Appetee.Application.Models.Auth;
using Appetee.Application.Options;
using Appetee.Application.Requests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Appetee.Application.Services.Auth
{
    public sealed class AuthService : IAuthService
    {
        private readonly IAuthRepository _authRepository;
        //private readonly IAuthQueries _authQueries;
        private readonly IUserQueries _userQueries;

        private readonly IAuthCookieService _cookieService;
        private readonly IPasswordHasher _passwordHasher;

        public AuthService(
            IAuthRepository authRepository,
            //IAuthQueries authQueries,
            IUserQueries userQueries,
            IPasswordHasher passwordHasher,
            IAuthCookieService cookieService)
        {
            _authRepository = authRepository;
            //_authQueries = authQueries;
            _passwordHasher = passwordHasher;
            _userQueries = userQueries;
            _cookieService = cookieService;
        }

        public async Task<AuthResult> SignUpAsync(HttpContext http  , SignUpRequest request, CancellationToken ct)
        {
            // Minimal validation (keep consistent with your existing validation approach)
            if (string.IsNullOrWhiteSpace(request.Username)) throw new ArgumentException("Username is required.");
            if (string.IsNullOrWhiteSpace(request.Email)) throw new ArgumentException("Email is required.");
            if (string.IsNullOrWhiteSpace(request.Password)) throw new ArgumentException("Password is required.");

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
            await _cookieService.SignInAsync(http, userId , username);

            // You can also map from request, but querying keeps canonical DB values.
            var user = await _userQueries.GetByIdAsync(userId, ct)
                       ?? throw new InvalidOperationException("User was created but cannot be loaded.");

            return new AuthResult(userId , username);

        }

        private static string GenerateSessionToken()
        {
            // 32 bytes -> base64url, good practical entropy for session tokens
            Span<byte> bytes = stackalloc byte[32];
            RandomNumberGenerator.Fill(bytes);

            var token = Convert.ToBase64String(bytes);
            token = token.Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return token;
        }
    }
}
