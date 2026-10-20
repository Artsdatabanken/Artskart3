using Artskart3.Api.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

public class DataProtectionExtensionsTests
{
    private static ILogger CreateLogger() => Mock.Of<ILogger>();

    private static IConfiguration BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider Configure(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddConfiguredDataProtection(BuildConfig(values), CreateLogger());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithoutStorageConfigured_LeavesKeyRingOnTheDefaultRepository()
    {
        // Ingen lagring satt betyr at vi ikke overstyrer noe, og Data Protection
        // faller tilbake til sin egen standard. Riktig lokalt; i et deployet miljoe
        // er det nettopp det som gjoer at brukerne logges ut ved utrulling.
        var options = Configure(new()).GetRequiredService<IOptions<KeyManagementOptions>>();

        options.Value.XmlRepository.Should().BeNull();
    }

    [Fact]
    public void WithBlobUri_PersistsKeyRingOutsideTheContainer()
    {
        var options = Configure(new()
        {
            ["DataProtection:BlobUri"] =
                "https://konto.blob.core.windows.net/dataprotection/artskart3-keys.xml"
        }).GetRequiredService<IOptions<KeyManagementOptions>>();

        options.Value.XmlRepository.Should().NotBeNull();
    }

    [Fact]
    public void ApplicationName_IsAppliedAsDiscriminator()
    {
        var options = Configure(new() { ["DataProtection:ApplicationName"] = "Artskart3Test" })
            .GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>();

        options.Value.ApplicationDiscriminator.Should().Be("Artskart3Test");
    }

    [Fact]
    public void ApplicationName_DefaultsToArtskart3()
    {
        var options = Configure(new())
            .GetRequiredService<IOptions<Microsoft.AspNetCore.DataProtection.DataProtectionOptions>>();

        options.Value.ApplicationDiscriminator.Should().Be("Artskart3");
    }
}
