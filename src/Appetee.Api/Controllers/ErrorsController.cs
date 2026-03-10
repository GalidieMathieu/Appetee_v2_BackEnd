using Appetee.Application.utils;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

[ApiController]
public class ErrorsController : ControllerBase
{
    [Route("/error")]
    public IActionResult Error()
    {
        var ex = HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (ex is ApiException apiEx)
        {
            return Problem(
                statusCode: apiEx.StatusCode,
                title: ReasonPhrases.GetReasonPhrase(apiEx.StatusCode),
                detail: apiEx.Message
            );
        }

        return Problem(statusCode: 500, title: "Server error");
    }
}
