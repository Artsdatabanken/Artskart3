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
    private static readonly Guid AnonymousUserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private const string ValidAnonymousToken = "signert-av-serveren";

    [Fact]
    public async Task StartExport_WithValidSubClaim_UsesSubAsUserId()
    {
        var exportService = new Mock<IExportService>();
        exportService
            .Setup(s => s.StartExportAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<ObservationSearchFilterDto>(),
                It.IsAny<List<string>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var controller = CreateController(exportService, CreateTokenService(), new Claim("sub", UserId.ToString()));

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        exportService.Verify(s => s.StartExportAsync(
            UserId,
            false,
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
        var controller = CreateController(exportService, CreateTokenService(), new Claim("name", "Kari Nordmann"));

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartExport_WithoutAnyClaims_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService());

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Program.cs krever allerede at sub kan parses som Guid ved innlogging, og
    /// UserController gjør samme kontroll. Eksporten må være like streng — ellers
    /// er det nettopp her fritekst kan snike seg inn i UserId-kolonnen.
    ///
    /// En gyldig anonym header skal heller ikke redde kallet: er brukeren
    /// autentisert, er en ugyldig sub en feiltilstand, og jobben skal ikke lagres
    /// på en anonym ID bak ryggen på brukeren.
    /// </summary>
    [Fact]
    public async Task StartExport_WithNonGuidSubClaim_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var tokenService = CreateTokenService();
        var controller = CreateController(exportService, tokenService, new Claim("sub", "Kari Nordmann"));
        controller.Request.Headers["X-Anonymous-User-Id"] = ValidAnonymousToken;

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Anonyme brukere må ha et token serveren selv har signert. En vilkårlig GUID
    /// i headeren godtas ikke — ellers kunne man sendt en annen brukers sub og
    /// overtatt eksportene deres.
    /// </summary>
    [Fact]
    public async Task StartExport_WithUnsignedAnonymousHeader_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService());
        controller.Request.Headers["X-Anonymous-User-Id"] = UserId.ToString();

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartExport_WithValidAnonymousToken_UsesAnonymousId()
    {
        var exportService = new Mock<IExportService>();
        exportService
            .Setup(s => s.StartExportAsync(
                It.IsAny<Guid>(),
                It.IsAny<bool>(),
                It.IsAny<ObservationSearchFilterDto>(),
                It.IsAny<List<string>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var controller = CreateController(exportService, CreateTokenService(acceptToken: true));
        controller.Request.Headers["X-Anonymous-User-Id"] = ValidAnonymousToken;

        var result = await controller.StartExport(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        exportService.Verify(s => s.StartExportAsync(
            AnonymousUserId,
            true,
            It.IsAny<ObservationSearchFilterDto>(),
            It.IsAny<List<string>>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
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

        var controller = CreateController(exportService, CreateTokenService(), new Claim("sub", sub));

        await controller.GetHistory(CancellationToken.None);

        exportService.Verify(
            s => s.GetUserExportHistoryAsync(UserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClaimAnonymousExports_WithAuthenticatedUser_TransfersToSub()
    {
        var exportService = new Mock<IExportService>();
        exportService
            .Setup(s => s.ClaimAnonymousExportsAsync(UserId, AnonymousUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var controller = CreateController(exportService, CreateTokenService(acceptToken: true), new Claim("sub", UserId.ToString()));
        controller.Request.Headers["X-Anonymous-User-Id"] = ValidAnonymousToken;

        var result = await controller.ClaimAnonymousExports(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        exportService.Verify(
            s => s.ClaimAnonymousExportsAsync(UserId, AnonymousUserId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ClaimAnonymousExports_WithoutSubClaim_ReturnsUnauthorized()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService(acceptToken: true));
        controller.Request.Headers["X-Anonymous-User-Id"] = ValidAnonymousToken;

        var result = await controller.ClaimAnonymousExports(CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ClaimAnonymousExports_WithInvalidAnonymousHeader_ReturnsBadRequest()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService(), new Claim("sub", UserId.ToString()));
        controller.Request.Headers["X-Anonymous-User-Id"] = "not-a-guid";

        var result = await controller.ClaimAnonymousExports(CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Alle endepunktene bruker samme identitetssjekk. Går ett av dem utenom,
    /// kan det lese eller skrive under feil identitet.
    /// </summary>
    [Fact]
    public async Task EveryUserScopedEndpoint_RejectsMissingSub()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService(), new Claim("name", "Kari Nordmann"));

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

    [Fact]
    public async Task StartAndSummary_WithInvalidFilter_ReturnBadRequestAndWriteNothing()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = CreateController(exportService, CreateTokenService(), new Claim("sub", UserId.ToString()));
        var request = new StartExportRequestDto
        {
            Filter = new ObservationSearchFilterDto { CadastralUnitIds = ["1101-1"] },
            SelectedColumns = []
        };

        var start = await controller.StartExport(request, CancellationToken.None);
        var summary = await controller.GetSummary(request, CancellationToken.None);

        start.Result.Should().BeOfType<BadRequestObjectResult>();
        summary.Result.Should().BeOfType<BadRequestObjectResult>();
        exportService.VerifyNoOtherCalls();
    }

    private static StartExportRequestDto ValidRequest() => new()
    {
        Filter = new ObservationSearchFilterDto(),
        SelectedColumns = []
    };

    private static Mock<IAnonymousUserTokenService> CreateTokenService(bool acceptToken = false)
    {
        var tokenService = new Mock<IAnonymousUserTokenService>();
        if (acceptToken)
        {
            var anonymousId = AnonymousUserId;
            tokenService
                .Setup(s => s.TryGetUserId(ValidAnonymousToken, out anonymousId))
                .Returns(true);
        }
        return tokenService;
    }

    private static ExportController CreateController(
        Mock<IExportService> exportService,
        Mock<IAnonymousUserTokenService> tokenService,
        params Claim[] claims)
    {
        var controller = new ExportController(
            exportService.Object,
            new Mock<IBlobStorageService>().Object,
            tokenService.Object,
            NullLogger<ExportController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // Uten authenticationType regnes identiteten som uautentisert,
                    // slik anonyme kall gjør gjennom middlewaren.
                    User = new ClaimsPrincipal(claims.Length > 0 ? new ClaimsIdentity(claims, "Test") : new ClaimsIdentity())
                }
            }
        };

        return controller;
    }
}
