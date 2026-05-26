using System.Reflection;
using Artskart3.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Fails if any controller action in Artskart3.Api lacks at least one integration test.
/// Convention: an integration test "covers" an action when its method name starts with
/// the action method name, e.g. "GetLocations_WithNoFilter_Returns200" covers "GetLocations".
/// </summary>
public class EndpointCoverageTests
{
    private static readonly Assembly ApiAssembly = typeof(SearchController).Assembly;
    private static readonly Assembly TestAssembly = typeof(EndpointCoverageTests).Assembly;

    [Fact]
    public void AllControllerActions_HaveAtLeastOneIntegrationTest()
    {
        var actions = GetApiActions();
        var testMethodNames = GetIntegrationTestMethodNames();

        var uncovered = actions
            .Where(a => !testMethodNames.Any(name => name.StartsWith(a.ActionName, StringComparison.Ordinal)))
            .Select(a => $"{a.ControllerName}.{a.ActionName}")
            .OrderBy(x => x)
            .ToList();

        uncovered.Should().BeEmpty(
            "every controller action must have at least one integration test whose name starts with the action name");
    }

    private static IEnumerable<(string ControllerName, string ActionName)> GetApiActions()
    {
        return ApiAssembly.GetTypes()
            .Where(t => t.GetCustomAttribute<ApiControllerAttribute>() != null
                        && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any())
                .Select(m => (ControllerName: controller.Name, ActionName: m.Name)));
    }

    private static HashSet<string> GetIntegrationTestMethodNames()
    {
        return TestAssembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Where(m => m.GetCustomAttribute<FactAttribute>() != null
                        || m.GetCustomAttribute<TheoryAttribute>() != null)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);
    }
}
