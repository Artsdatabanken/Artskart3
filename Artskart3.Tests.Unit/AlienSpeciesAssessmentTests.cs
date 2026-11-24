using System.Net;
using System.Text;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.Enums;
using Artskart3.Infrastructure.Services.AlienSpecies;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Artskart3.Tests.Unit;

public class AlienSpeciesAssessmentTests
{
    [Fact]
    public async Task Client_FollowsODataPagination()
    {
        var handler = new StubHttpMessageHandler(
            JsonResponse("""
                {
                  "value": [{
                    "id": 1,
                    "category": "LO",
                    "area": "N",
                    "scientificNameInformation": {"nameId": 100},
                    "previousAssessments": []
                  }],
                  "@odata.nextLink": "https://lister.artsdatabanken.no/odata/v1/AlienSpeciesAssessment?$select=id,area,taxonId,category&$skiptoken=id-1"
                }
                """),
            JsonResponse("""
                {
                  "value": [{
                    "id": 2,
                    "category": "HI",
                    "area": "S",
                    "scientificNameInformation": {"nameId": 101},
                    "previousAssessments": []
                  }]
                }
                """));
        var client = CreateClient(handler);

        var result = await client.GetCurrentAsync();

        result.PageCount.Should().Be(2);
        result.Items.Select(item => item.Id).Should().Equal(1, 2);
        handler.RequestUris.Should().AllSatisfy(uri =>
            uri.Host.Should().Be("lister.artsdatabanken.no"));
    }

    [Fact]
    public async Task Client_RejectsContinuationOutsideListerOrigin()
    {
        var handler = new StubHttpMessageHandler(
            JsonResponse("""
                {
                  "value": [],
                  "@odata.nextLink": "https://example.com/odata/v1/AlienSpeciesAssessment?$skiptoken=x"
                }
                """));
        var client = CreateClient(handler);

        var act = () => client.GetCurrentAsync();

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*outside the configured origin*");
    }

    [Fact]
    public void Normalizer_Maps2023ScientificNameAndAreas()
    {
        var snapshot = new AlienSpeciesAssessment2023(
            17,
            " s ",
            new AlienSpeciesScientificName(314),
            " hi ");

        var normalized = AlienSpeciesAssessmentNormalizer.NormalizeSnapshot2023([snapshot]);

        normalized.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new NormalizedTaxonAssessment(
                314, "S", "HI", "17", TaxonAssessmentSource.Snapshot2023));
    }

    [Fact]
    public void Normalizer_PrefersHighestRiskCategoryForDuplicate()
    {
        var assessments = new[]
        {
            new AlienSpeciesAssessment2023(
                6336,
                "N",
                new AlienSpeciesScientificName(220512),
                "NR"),
            new AlienSpeciesAssessment2023(
                7354,
                "N",
                new AlienSpeciesScientificName(220512),
                "LO")
        };

        var normalized = AlienSpeciesAssessmentNormalizer.NormalizeSnapshot2023(assessments);

        normalized.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new NormalizedTaxonAssessment(
                220512, "N", "LO", "7354", TaxonAssessmentSource.Snapshot2023));
    }

    [Fact]
    public void Normalizer_UsesHighestAssessmentIdForEqualDuplicateCategories()
    {
        var assessments = new[]
        {
            new AlienSpeciesAssessment2023(
                1, "N", new AlienSpeciesScientificName(100), "LO"),
            new AlienSpeciesAssessment2023(
                2, "N", new AlienSpeciesScientificName(100), "LO")
        };

        var normalized = AlienSpeciesAssessmentNormalizer.NormalizeSnapshot2023(assessments);

        normalized.Should().ContainSingle()
            .Which.ExternalAssessmentId.Should().Be("2");
    }

    [Fact]
    public void Normalizer_RejectsDuplicateAssessmentKeys()
    {
        var assessments = new[]
        {
            new CurrentAlienSpeciesAssessment(
                1, "N", "LO", new AlienSpeciesScientificNameInformation(100)),
            new CurrentAlienSpeciesAssessment(
                2, "N", "HI", new AlienSpeciesScientificNameInformation(100))
        };

        var act = () => AlienSpeciesAssessmentNormalizer.NormalizeCurrent(assessments);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*Multiple Current assessments*");
    }

    [Fact]
    public void Normalizer_KeepsPrevious2023AssessmentReference()
    {
        var assessment = new CurrentAlienSpeciesAssessment(
            59,
            "N",
            "LO",
            new AlienSpeciesScientificNameInformation(121192),
            [
                new AlienSpeciesPreviousAssessment(2023, "59"),
                new AlienSpeciesPreviousAssessment(2018, "FA3/N/1068")
            ]);

        var normalized = AlienSpeciesAssessmentNormalizer.NormalizeCurrent([assessment]);

        normalized.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new NormalizedTaxonAssessment(
                121192,
                "N",
                "LO",
                "59",
                TaxonAssessmentSource.Current,
                "59"));
    }

    [Fact]
    public void Merger_PrefersCurrentEvenWhenRetained()
    {
        var current = Assessment(TaxonAssessmentSource.Current, DateTime.UtcNow, false);
        var snapshot = Assessment(TaxonAssessmentSource.Snapshot2023, DateTime.UtcNow.AddDays(-1), false);
        AlienSpeciesAssessmentMerger.SelectEffective([current, snapshot]);
        current.IsEffective.Should().BeTrue();
        snapshot.IsEffective.Should().BeFalse();

        current.IsRetained = true;
        AlienSpeciesAssessmentMerger.SelectEffective([current, snapshot]);
        current.IsEffective.Should().BeTrue();
        snapshot.IsEffective.Should().BeFalse();

        snapshot.IsRetained = true;
        AlienSpeciesAssessmentMerger.SelectEffective([current, snapshot]);
        current.IsEffective.Should().BeTrue();
        snapshot.IsEffective.Should().BeFalse();
    }

    private static AlienSpeciesAssessmentClient CreateClient(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://lister.artsdatabanken.no/odata/v1/"),
                Timeout = TimeSpan.FromSeconds(10)
            },
            Options.Create(new AlienSpeciesAssessmentOptions()),
            NullLogger<AlienSpeciesAssessmentClient>.Instance);

    private static TaxonAssessment Assessment(
        TaxonAssessmentSource source, DateTime lastSeenAt, bool retained) =>
        new()
        {
            TaxonId = 1,
            CategoryTypeId = 2,
            Area = "N",
            Source = source,
            LastSeenAt = lastSeenAt,
            IsRetained = retained
        };

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
