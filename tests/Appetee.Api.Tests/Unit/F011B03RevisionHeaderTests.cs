using Appetee.Api.Controllers;
using Appetee.Api.Helpers;
using Appetee.Application.utils;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace Appetee.Api.Tests.Unit;

public sealed class F011B03RevisionHeaderTests
{
    [Fact]
    public void WriteResult_SerializesRevisionAsAnExactDecimalString()
    {
        var result = new Appetee.Application.Models.MealPlans.MealPlanWriteResult<int>(
            long.MaxValue, true, 1);
        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(result));
        Assert.Equal("9223372036854775807", json.RootElement.GetProperty("Revision").GetString());
        _ = new ConflictException("Legacy explicit null", null);
    }

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("00042", 42L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void Parse_AcceptsNonnegativeSignedBigIntDecimalStrings(string value, long expected)
    {
        var headers = new HeaderDictionary
        {
            [MealPlanRevisionHeader.HeaderName] = value,
        };

        Assert.Equal(expected, MealPlanRevisionHeader.Parse(headers));
    }

    [Fact]
    public void Parse_RejectsMissingHeaderWithStableCodeAndFieldError()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            MealPlanRevisionHeader.Parse(new HeaderDictionary()));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal("revision_required", exception.Code);
        Assert.Contains("revision", exception.Errors!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("\"1\"")]
    [InlineData("9223372036854775808")]
    public void Parse_RejectsMalformedOrOverflowValues(string value)
    {
        var headers = new HeaderDictionary
        {
            [MealPlanRevisionHeader.HeaderName] = value,
        };

        var exception = Assert.Throws<ValidationException>(() =>
            MealPlanRevisionHeader.Parse(headers));

        Assert.Equal("revision_invalid", exception.Code);
        Assert.Contains("revision", exception.Errors!);
    }

    [Fact]
    public void Parse_RejectsMultipleHeaderValues()
    {
        var headers = new HeaderDictionary
        {
            [MealPlanRevisionHeader.HeaderName] = new StringValues(["1", "2"]),
        };

        var exception = Assert.Throws<ValidationException>(() =>
            MealPlanRevisionHeader.Parse(headers));

        Assert.Equal("revision_invalid", exception.Code);
    }

    [Fact]
    public void ApiExceptions_PreserveLegacyConstructorsAndExposeStructuredErrors()
    {
        var legacyValidation = new ValidationException("Legacy validation");
        var legacyConflict = new ConflictException("Legacy conflict", new InvalidOperationException());
        var structured = new ValidationException(
            "Invalid fields.",
            "validation_failed",
            new Dictionary<string, string[]>
            {
                ["profile.heightCm"] = ["Height is outside the supported range."],
            });

        Assert.Equal(400, legacyValidation.StatusCode);
        Assert.Null(legacyValidation.Code);
        Assert.Equal(409, legacyConflict.StatusCode);
        Assert.IsType<InvalidOperationException>(legacyConflict.InnerException);
        Assert.Equal("validation_failed", structured.Code);
        Assert.Contains("profile.heightCm", structured.Errors!);
    }

    [Fact]
    public void ErrorsController_AddsCodeErrorsAndTraceIdToProblemDetails()
    {
        var exception = new ValidationException(
            "Invalid fields.",
            "validation_failed",
            new Dictionary<string, string[]>
            {
                ["profile.heightCm"] = ["Height is outside the supported range."],
            });
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "b03-trace-id",
        };
        context.Request.Method = HttpMethods.Put;
        context.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature
        {
            Error = exception,
            Path = "/api/users/me/nutrition-profile",
        });
        var controller = new ErrorsController(
            NullLogger<ErrorsController>.Instance,
            new TestHostEnvironment())
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };

        var result = Assert.IsType<ObjectResult>(controller.Error());
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("validation_failed", problem.Extensions["code"]);
        Assert.Equal("b03-trace-id", problem.Extensions["traceId"]);
        var errors = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string[]>>(
            problem.Extensions["errors"]);
        Assert.Contains("profile.heightCm", errors);
        using var json = System.Text.Json.JsonDocument.Parse(
            System.Text.Json.JsonSerializer.Serialize(problem));
        Assert.Equal("validation_failed", json.RootElement.GetProperty("code").GetString());
        Assert.Equal("b03-trace-id", json.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("Height is outside the supported range.",
            json.RootElement.GetProperty("errors").GetProperty("profile.heightCm")[0].GetString());
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Appetee.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
