using Microsoft.AspNetCore.Http;

namespace Appetee.Application.utils
{
    public abstract class ApiException : Exception
    {
        public int StatusCode { get; }
        public string? Code { get; }

        protected ApiException(
            int statusCode,
            string message,
            Exception? inner = null,
            string? code = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
            Code = code;
        }
    }

    public sealed class ValidationException : ApiException
    {
        public ValidationException(string message) : base(400, message) { }
    }
    public sealed class UnauthorizedException : ApiException
    {
        public UnauthorizedException(string message = "Unauthorized") : base(StatusCodes.Status401Unauthorized, message) { }
    }

    /// Signals the frontend to continue the safe pending-verification flow.
    public sealed class EmailVerificationRequiredException : ApiException
    {
        public EmailVerificationRequiredException()
            : base(
                StatusCodes.Status403Forbidden,
                "Email verification is required.",
                code: "email_verification_required") { }
    }

    /// Distinguishes an ended session from a request that never had authentication.
    public sealed class SessionExpiredException : ApiException
    {
        public SessionExpiredException()
            : base(
                StatusCodes.Status401Unauthorized,
                "Your session expired. Please log in again.",
                code: "session_expired") { }
    }

    /// Uses one outcome for unknown, expired, and previously consumed recovery proofs.
    public sealed class InvalidPasswordRecoveryTokenException : ApiException
    {
        public InvalidPasswordRecoveryTokenException()
            : base(
                StatusCodes.Status400BadRequest,
                "The password recovery link is invalid or expired.",
                code: "password_recovery_token_invalid") { }
    }

    public sealed class ForbiddenException : ApiException
    {
        public ForbiddenException(string message) : base(403, message) { }
    }

    public sealed class NotFoundException : ApiException
    {
        public NotFoundException(string message) : base(404, message) { }
    }

    public sealed class ConflictException : ApiException
    {
        public ConflictException(string message, Exception? inner = null) : base(409, message, inner) { }
    }

    public sealed class InternalServerException : ApiException
    {
        public InternalServerException(string message = "An internal server error occurred.", Exception? inner = null)
            : base(StatusCodes.Status500InternalServerError, message, inner) { }
    }
}
