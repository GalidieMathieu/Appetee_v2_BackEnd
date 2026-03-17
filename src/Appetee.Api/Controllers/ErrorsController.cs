using Appetee.Application.utils;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Appetee.Api.Controllers
{
    [ApiController]
    public sealed class ErrorsController : ControllerBase
    {
        private readonly ILogger<ErrorsController> _logger;
        private readonly IHostEnvironment _env;

        public ErrorsController(ILogger<ErrorsController> logger, IHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        [Route("/error")]
        public IActionResult Error()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            var ex = feature?.Error;

            var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

            if (ex is ApiException apiEx)
            {
                var pd = new ProblemDetails
                {
                    Status = apiEx.StatusCode,
                    Title = ReasonPhrases.GetReasonPhrase(apiEx.StatusCode),
                    Detail = apiEx.Message
                };

                pd.Extensions["traceId"] = traceId;

                // Log at Information for known API exceptions
                _logger.LogInformation(ex, "API error handled: {Message} (TraceId: {TraceId})", ex.Message, traceId);

                return StatusCode(apiEx.StatusCode, pd);
            }

            // Unknown exception -> Internal Server Error
            var status = StatusCodes.Status500InternalServerError;

            _logger.LogError(ex, "Unhandled exception (TraceId: {TraceId})", traceId);

            var detail = _env.IsDevelopment() ? ex?.ToString() : "An unexpected error occurred. Please contact support.";

            var generalPd = new ProblemDetails
            {
                Status = status,
                Title = ReasonPhrases.GetReasonPhrase(status),
                Detail = detail
            };

            generalPd.Extensions["traceId"] = traceId;

            return StatusCode(status, generalPd);
        }
    }
}
