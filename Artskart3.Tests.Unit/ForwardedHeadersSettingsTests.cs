using Artskart3.Api.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Artskart3.Tests.Unit;

public class ForwardedHeadersSettingsTests
{
    private static ForwardedHeadersSettings Bind(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build()
            .GetSection(ForwardedHeadersSettings.SectionName)
            .Get<ForwardedHeadersSettings>() ?? new ForwardedHeadersSettings();

    [Fact]
    public void DefaultsToTheHeaderApplicationGatewaySendsToday()
    {
        Bind(new()).OriginalHostHeaderName.Should().Be("X-Original-Host");
    }

    [Fact]
    public void CanBeOverriddenForAnIngressThatSendsTheStandardHeader()
    {
        Bind(new() { ["ForwardedHeaders:OriginalHostHeaderName"] = "X-Forwarded-Host" })
            .OriginalHostHeaderName.Should().Be("X-Forwarded-Host");
    }
}
