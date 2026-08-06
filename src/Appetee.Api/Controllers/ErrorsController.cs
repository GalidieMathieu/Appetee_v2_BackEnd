using Appetee.Application.utils;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class ErrorsController : ControllerBase
    {
        private readonly ILogger<ErrorsController> _logger;
        private readonly IHostEnvironment _env;

        public ErrorsController(
            ILogger<ErrorsController> logger,
            IHostEnvironment env)
        {
            _logger =
                logger
                ?? throw new ArgumentNullException(
                    nameof(logger));

            _env =
                env
                ?? throw new ArgumentNullException(
                    nameof(env));
        }

        /*
         * Error responses must not be cached by browsers, proxies,
         * or intermediary services.
         */
        [Route("/error")]
        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            /*
             * IExceptionHandlerPathFeature provides:
             * - The original exception
             * - The original request path
             *
             * HttpContext.Request.Path may contain "/error" because the
             * exception middleware re-executes the request through this
             * endpoint.
             */
            var feature =
                HttpContext.Features
                    .Get<IExceptionHandlerPathFeature>();

            var exception = feature?.Error;

            var originalPath =
                feature?.Path
                ?? HttpContext.Request.Path.Value
                ?? string.Empty;

            var method =
                HttpContext.Request.Method;

            var traceId =
                Activity.Current?.TraceId.ToString();

            if (string.IsNullOrWhiteSpace(traceId))
            {
                traceId =
                    HttpContext.TraceIdentifier;
            }

            if (exception is ApiException apiException)
            {
                var statusCode =
                    apiException.StatusCode;

                LogHandledException(
                    exception,
                    statusCode,
                    method,
                    originalPath,
                    traceId);

                var detail =
                    statusCode
                        >= StatusCodes
                            .Status500InternalServerError
                    && !_env.IsDevelopment()
                        ? "An unexpected error occurred."
                        : apiException.Message;

                var problemDetails =
                    new ProblemDetails
                    {
                        Status = statusCode,
                        Title =
                            ReasonPhrases.GetReasonPhrase(
                                statusCode),
                        Detail = detail,
                        Instance = originalPath
                    };

                problemDetails.Extensions["traceId"] =
                    traceId;

                return StatusCode(
                    statusCode,
                    problemDetails);
            }

            const int internalServerError =
                StatusCodes
                    .Status500InternalServerError;

            LogHandledException(
                exception,
                internalServerError,
                method,
                originalPath,
                traceId);

            var internalErrorDetail =
                _env.IsDevelopment()
                    ? exception?.ToString()
                      ?? "No exception details were available."
                    : "An unexpected error occurred.";

            var generalProblemDetails =
                new ProblemDetails
                {
                    Status = internalServerError,
                    Title =
                        ReasonPhrases.GetReasonPhrase(
                            internalServerError),
                    Detail = internalErrorDetail,
                    Instance = originalPath
                };

            generalProblemDetails.Extensions["traceId"] =
                traceId;

            return StatusCode(
                internalServerError,
                generalProblemDetails);
        }

        private void LogHandledException(
            Exception? exception,
            int statusCode,
            string method,
            string path,
            string traceId)
        {
            var exceptionType =
                exception?.GetType().FullName
                ?? "Unknown";

            var exceptionMessage =
                exception?.Message
                ?? "No exception details were available.";

            if (statusCode
                >= StatusCodes
                    .Status500InternalServerError)
            {
                /*
                 * Supplying the exception as the first argument records:
                 * - Exception type
                 * - Message
                 * - Inner exception
                 * - Stack trace
                 */
                _logger.LogError(
                    exception,
                    "Request failed. " +
                    "HTTP {HttpMethod} {RequestPath}; " +
                    "StatusCode {StatusCode}; " +
                    "ExceptionType {ExceptionType}; " +
                    "ExceptionMessage {ExceptionMessage}; " +
                    "TraceId {TraceId}",
                    method,
                    path,
                    statusCode,
                    exceptionType,
                    exceptionMessage,
                    traceId);

                return;
            }

            /*
             * Expected API/client errors are logged without passing the
             * exception object. This prevents unnecessary stack traces from
             * filling production logs for normal 4xx responses.
             */
            _logger.LogWarning(
                "Request completed with API error. " +
                "HTTP {HttpMethod} {RequestPath}; " +
                "StatusCode {StatusCode}; " +
                "ExceptionType {ExceptionType}; " +
                "ExceptionMessage {ExceptionMessage}; " +
                "TraceId {TraceId}",
                method,
                path,
                statusCode,
                exceptionType,
                exceptionMessage,
                traceId);
        }
    }
}
