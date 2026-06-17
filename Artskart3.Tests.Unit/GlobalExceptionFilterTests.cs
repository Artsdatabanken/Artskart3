using Artskart3.Api.Filters;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class GlobalExceptionFilterTests
{
    private readonly Mock<ILogger<GlobalExceptionFilter>> _loggerMock = new();
    private readonly GlobalExceptionFilter _sut;

    public GlobalExceptionFilterTests()
    {
        _sut = new GlobalExceptionFilter(_loggerMock.Object);
    }

    [Fact]
    public void OnException_WhenApplicationException_Returns503()
    {
        var context = CreateExceptionContext(new ApplicationException("Service unavailable"));

        _sut.OnException(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(503);
    }

    [Fact]
    public void OnException_WhenUnexpectedException_Returns500()
    {
        var context = CreateExceptionContext(new InvalidOperationException("Unexpected"));

        _sut.OnException(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public void OnException_SetsExceptionHandledToTrue()
    {
        var context = CreateExceptionContext(new Exception("Test"));

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
    }

    [Fact]
    public void OnException_WhenAlreadyHandled_DoesNotSetResult()
    {
        var context = CreateExceptionContext(new Exception("Test"));
        context.ExceptionHandled = true;

        _sut.OnException(context);

        context.Result.Should().BeNull();
    }

    private static ExceptionContext CreateExceptionContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(new RouteValueDictionary { { "controller", "Test" }, { "action", "Index" } }),
            new ActionDescriptor()
        );
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception
        };
    }
}
