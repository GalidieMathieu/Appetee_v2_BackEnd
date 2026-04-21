using Microsoft.AspNetCore.Http;

namespace Appetee.Application.utils
{
    public abstract class ApiException : Exception
    {
        public int StatusCode { get; }

        protected ApiException(int statusCode, string message, Exception? inner = null)
            : base(message, inner)
        {
            StatusCode = statusCode;
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
