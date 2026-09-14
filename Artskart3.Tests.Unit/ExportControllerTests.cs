using System.Security.Claims;
using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Hvilken identitet eksportjobbene lagres under.
///
/// Controlleren falt tidligere tilbake på «name»-claimet når «sub» manglet. Alle
/// lesestier i ExportService filtrerer på UserId, og kolonnen er nvarchar(256)
/// uten formatkrav, så en jobb skrevet under visningsnavnet ble usynlig — og
/// umulig å laste ned — for den samme brukeren neste gang sub var på plass.
/// Visningsnavn er heller ikke unike, så to personer med likt navn delte
/// eksporthistorikk. Det er en datalekkasje, ikke bare et brukskrøll.
/// </summary>
public class ExportControllerTests
{
    // Med hex-bokstaver, slik at store/små bokstaver faktisk er en forskjell å teste.
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public async Task StartExport_WithValidSubClaim_UsesSubAsUserId()
    {
        var exportService = new Mock<IExportService>();
        exportService
            .Setup(s => s.StartExportAsync(
                It.IsAny<Guid>(),
                It.IsAny<ObservationSearchFilterDto>(),
                It.IsAny<List<string>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var controller = CreateController(exportService, new Claim("sub", UserId.ToString()));

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        exportService.Verify(s => s.StartExportAsync(
            UserId,
            It.IsAny<ObservationSearchFilterDto>(),
            It.IsAny<List<string>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Kjernen i saken: et navn er ikke lenger en gyldig identitet. Er sub borte,
    /// skal kallet avvises — ikke skrive en jobb ingen finner igjen.
    /// </summary>
    [Fact]
    public async Task StartExport_WithOnlyNameClaim_ReturnsUnauthorizedAndWritesNothing()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, new Claim("name", "Kari Nordmann"));

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartExport_WithoutAnyClaims_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService);

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Program.cs krever allerede at sub kan parses som Guid ved innlogging, og
    /// UserController gjør samme kontroll. Eksporten må være like streng — ellers
    /// er det nettopp her fritekst kan snike seg inn i UserId-kolonnen.
    /// </summary>
    [Fact]
    public async Task StartExport_WithNonGuidSubClaim_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, new Claim("sub", "Kari Nordmann"));

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Identitetstjenesten kan formatere sub med klammer, uten bindestreker eller
    /// med store bokstaver. Alle formene er samme identitet, og skal gi samme
    /// UserId — ellers ville samme person fått hver sin eksporthistorikk.
    ///
    /// Dette er gratis nå som kolonnen er uniqueidentifier: Guid.TryParse godtar
    /// alle formene og gir samme verdi. Med en strengkolonne måtte formatet
    /// normaliseres for hånd, og glapp det, ble det stille datasplitting.
    /// </summary>
    [Theory]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    [InlineData("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE")]
    [InlineData("aaaaaaaabbbbccccddddeeeeeeeeeeee")]
    [InlineData("{aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee}")]
    public async Task GetHistory_NormalisesSubToSameUserId(string sub)
    {
        var exportService = new Mock<IExportService>();
        exportService
            .Setup(s => s.GetUserExportHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var controller = CreateController(exportService, new Claim("sub", sub));

        await controller.GetHistory(CancellationToken.None);

        exportService.Verify(
            s => s.GetUserExportHistoryAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    /// <summary>
    /// Alle endepunktene bruker samme identitetssjekk. Går ett av dem utenom,
    /// kan det lese eller skrive under feil identitet.
    /// </summary>
    [Fact]
    public async Task EveryUserScopedEndpoint_RejectsMissingSub()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, new Claim("name", "Kari Nordmann"));

        var results = new List<IActionResult?>
        {
            (await controller.StartExport(ValidRequest(), CancellationToken.None)).Result,
            (await controller.GetStatus(1, CancellationToken.None)).Result,
            await controller.Download(1, CancellationToken.None),
            await controller.DownloadExcel(1, CancellationToken.None),
            await controller.Cancel(1, CancellationToken.None),
            (await controller.GetHistory(CancellationToken.None)).Result,
        };

        results.Should().AllBeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    private static StartExportRequestDto ValidRequest() => new()
    {
        Filter = new ObservationSearchFilterDto(),
        SelectedColumns = []
    };

    private static ExportController CreateController(Mock<IExportService> exportService, params Claim[] claims)
    {
        var controller = new ExportController(
            exportService.Object,
            new Mock<IBlobStorageService>().Object,
            NullLogger<ExportController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };

        return controller;
    }
}
