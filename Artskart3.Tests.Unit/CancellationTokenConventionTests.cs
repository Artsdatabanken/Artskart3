using System.Reflection;
using Artskart3.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace Artskart3.Tests.Unit;

public class CancellationTokenConventionTests
{
    [Fact]
    public void AllControllerEndpoints_ShouldAcceptCancellationToken()
    {
        var controllerTypes = typeof(SearchController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        var endpointsWithoutCancellationToken = new List<string>();

        foreach (var controller in controllerTypes)
        {
            var actions = controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes()
                    .Any(a => a.GetType().Name.StartsWith("Http")));

            foreach (var action in actions)
            {
                var hasCancellationToken = action.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken));

                if (!hasCancellationToken)
                {
                    endpointsWithoutCancellationToken.Add($"{controller.Name}.{action.Name}");
                }
            }
        }

        endpointsWithoutCancellationToken.Should().BeEmpty(
            "all controller endpoints should accept a CancellationToken parameter to support request cancellation. " +
            $"Missing: {string.Join(", ", endpointsWithoutCancellationToken)}");
    }
}
