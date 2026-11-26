using Artskart3.Core.Application.Validation;
using FluentAssertions;

namespace Artskart3.Tests.Unit;

public class OriginalObservationUrlTests
{
    [Theory]
    [InlineData("https://mobil.artsobservasjoner.no/sighting/5299619", "https://mobil.artsobservasjoner.no/sighting/5299619")]
    [InlineData("https://www.gbif.org/occurrence/1931476664|https://www.inaturalist.org/observations/15165063", "https://www.gbif.org/occurrence/1931476664")]
    [InlineData("Schartau et al. 1997| https://vannmiljo.miljodirektoratet.no/#/searchregistrations?waterLocationID=100046 ", "https://vannmiljo.miljodirektoratet.no/#/searchregistrations?waterLocationID=100046")]
    [InlineData("http://egiset.ihar.edu.pl?nr=51173", "http://egiset.ihar.edu.pl?nr=51173")]
    [InlineData("https://data.d4science.org/DownloadServlet?ids=082e883f;&viewContent=true", "https://data.d4science.org/DownloadServlet?ids=082e883f;&viewContent=true")]
    [InlineData("https://doi.org/10.3390/plants10122767|https://biomemory.cnr.it/dbrecord/b50707a5", "https://biomemory.cnr.it/dbrecord/b50707a5")]
    public void FromAssociatedReferences_ReturnsFirstObservationPage(string references, string expected)
    {
        OriginalObservationUrl.FromAssociatedReferences(references).Should().Be(expected);
    }

    [Theory]
    [InlineData("https://www.rovbase.no")]
    [InlineData("http://www.rovbase.no/")]
    [InlineData("http://dx.doi.org/10.1016/j.ecolind.2012.03.011")]
    [InlineData("https://a.no/1; https://b.no/2")]
    [InlineData("https://example.org/sighting/1 (accessed 2020)")]
    [InlineData("#http://www.jbjordal.no/publikasjoner/Rogaland2007.PDF#")]
    [InlineData("10.5194/bg-12-1|images/frame.jpg|javascript:alert(1)|https://user@example.org/1")]
    [InlineData("")]
    [InlineData(null)]
    public void FromAssociatedReferences_SkipsValuesThatAreNotObservationPages(string? references)
    {
        OriginalObservationUrl.FromAssociatedReferences(references).Should().BeNull();
    }
}
