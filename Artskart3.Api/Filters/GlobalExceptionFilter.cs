using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Artskart3.Api.Filters;

public class GlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<GlobalExceptionFilter> _logger;

    public GlobalExceptionFilter(ILogger<GlobalExceptionFilter> logger)
    {
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.ExceptionHandled)
            return;

        var endpoint = $"{context.RouteData.Values["controller"]}/{context.RouteData.Values["action"]}";

        switch (context.Exception)
        {
            case ApplicationException ex:
                _logger.LogWarning(ex, "Application error at {Endpoint}", endpoint);
                context.Result = new ObjectResult(new { error = "An error occurred while processing your request. Please try again later." })
                {
                    StatusCode = 503
                };
                break;

            default:
                _logger.LogError(context.Exception, "Unexpected error at {Endpoint}", endpoint);
                context.Result = new ObjectResult(new { error = "An unexpected error occurred while processing your request. Please try again later." })
                {
                    StatusCode = 500
                };
                break;
        }

        context.ExceptionHandled = true;
    }
}
