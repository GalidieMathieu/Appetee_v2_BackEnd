using Appetee.Application.utils;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Diagnostics;
using Appetee.Api.Diagnostics;

namespace Appetee.Api.Controllers
{
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public sealed class ErrorsController : ControllerBase
    {
        private const int ClientClosedRequestStatusCode = 499;
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

            var traceId = HttpContext.Items[RequestDiagnosticsMiddleware.ContextKey] is RequestLogContext request
                ? request.TraceId : Activity.Current?.TraceId.ToString();

            if (string.IsNullOrWhiteSpace(traceId))
            {
                traceId =
                    HttpContext.TraceIdentifier;
            }

            if (exception is OperationCanceledException
                && HttpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogDebug(
                    "Request was canceled by the client. " +
                    "HTTP {HttpMethod} {RequestPath}; " +
                    "StatusCode {StatusCode}; " +
                    "TraceId {TraceId}",
                    method,
                    originalPath,
                    ClientClosedRequestStatusCode,
                    traceId);

                return StatusCode(ClientClosedRequestStatusCode);
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

                var detail = statusCode >= StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred." : apiException.Message;

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

                if (!string.IsNullOrWhiteSpace(apiException.Code))
                {
                    problemDetails.Extensions["code"] =
                        apiException.Code;
                }

                if (apiException.Errors is { Count: > 0 })
                {
                    problemDetails.Extensions["errors"] =
                        apiException.Errors;
                }

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

            const string internalErrorDetail = "An unexpected error occurred.";

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

            if (statusCode
                >= StatusCodes
                    .Status500InternalServerError)
            {
                /*
                 * Keep causes available to the privacy provider, which emits only
                 * types, classified codes and method frames, never raw messages.
                 */
                _logger.LogError(
                    Appetee.Application.Diagnostics.LogEvents.RequestException,
                    exception,
                    "Request failed. " +
                    "HTTP {HttpMethod} {RequestPath}; " +
                    "StatusCode {StatusCode}; " +
                    "ExceptionType {ExceptionType}; " +
                    "TraceId {TraceId}",
                    method,
                    path,
                    statusCode,
                    exceptionType,
                    traceId);

                return;
            }

            /*
             * Expected API/client errors stay warnings. The privacy provider
             * classifies known failures without exposing submitted values.
             */
            _logger.LogWarning(
                Appetee.Application.Diagnostics.LogEvents.RequestException,
                exception,
                "Request completed with API error. " +
                "HTTP {HttpMethod} {RequestPath}; " +
                "StatusCode {StatusCode}; " +
                "ExceptionType {ExceptionType}; " +
                "TraceId {TraceId}",
                method,
                path,
                statusCode,
                exceptionType,
                traceId);
        }
    }
}
