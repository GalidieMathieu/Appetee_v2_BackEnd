// Purpose: Verifies central API error handling distinguishes client request cancellation from server failures.
// Created: 2026-08-25T15:25:01-06:00
// Last updated: 2026-08-25T15:25:01-06:00

using Appetee.Api.Controllers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;

namespace Appetee.Api.Tests.Unit;

public sealed class ErrorsControllerTests
{
    [Fact]
    public void Error_Returns499_WhenTheClientAbortedTheRequest()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var controller = CreateController(
            new OperationCanceledException(cancellation.Token),
            cancellation.Token);

        var result = Assert.IsType<StatusCodeResult>(controller.Error());

        Assert.Equal(499, result.StatusCode);
    }

    [Fact]
    public void Error_Returns500_WhenCancellationWasNotCausedByRequestAbort()
    {
        var controller = CreateController(
            new OperationCanceledException(),
            CancellationToken.None);

        var result = Assert.IsType<ObjectResult>(controller.Error());

        Assert.Equal(StatusCodes.Status500InternalServerError, result.StatusCode);
    }

    private static ErrorsController CreateController(
        Exception exception,
        CancellationToken requestAborted)
    {
        var context = new DefaultHttpContext
        {
            RequestAborted = requestAborted,
        };
        context.Request.Method = HttpMethods.Get;
        context.Features.Set<IExceptionHandlerPathFeature>(
            new ExceptionHandlerFeature
            {
                Error = exception,
                Path = "/api/recipes",
            });

        return new ErrorsController(
            NullLogger<ErrorsController>.Instance,
            new TestHostEnvironment())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = context,
            },
        };
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Appetee.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
