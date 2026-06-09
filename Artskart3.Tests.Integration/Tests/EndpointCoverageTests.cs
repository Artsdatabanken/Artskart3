using System.Reflection;
using Artskart3.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Artskart3.Tests.Integration.Tests;

/// <summary>
/// Feiler hvis noen kontroller-handling i Artskart3.Api mangler minst én integrasjonstest.
/// Konvensjon: en integrasjonstest «dekker» en handling når metodenavnet starter med
/// handlingens metodenavn, f.eks. «GetLocations_WithNoFilter_Returns200» dekker «GetLocations».
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
            "alle kontroller-handlinger må ha minst én integrasjonstest der metodenavnet starter med handlingsnavnet");
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
