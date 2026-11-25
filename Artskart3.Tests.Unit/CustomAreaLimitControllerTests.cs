using System.Security.Claims;
using Artskart3.Api.Controllers;
using Artskart3.Core.Application.Configuration;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Artskart3.Tests.Unit;

/// <summary>
/// Et egendefinert område med for mange lokasjoner avvises før søket eller eksporten kjøres.
/// </summary>
public class CustomAreaLimitControllerTests
{
    private const string LargeArea = "POLYGON ((0 0, 1000 0, 1000 1000, 0 0))";
    private readonly StubCustomAreaService _customAreas = new(new() { [LargeArea] = null });

    [Fact]
    public async Task Search_WithTooManyLocations_ReturnsBadRequestWithoutSearching()
    {
        var searchService = new Mock<ISearchService>(MockBehavior.Strict);
        var controller = new SearchController(
            searchService.Object, new Mock<ISpeciesService>().Object, new Mock<IFacetCountService>().Object,
            new Mock<ILogger<SearchController>>().Object,
            Options.Create(new PaginationOptions()), _customAreas);

        var count = await controller.GetLocationCount(new LocationSearchFilterDto { CustomAreaWkt = LargeArea });
        var observations = await controller.GetObservations(new ObservationSearchFilterDto { CustomAreaWkt = LargeArea });
        var polygons = await controller.GetLocationPolygons(new LocationSearchFilterDto { CustomAreaWkt = LargeArea });
        var areaCounts = await controller.GetAreaCounts(1, new LocationSearchFilterDto { CustomAreaWkt = LargeArea });

        count.Result.Should().BeOfType<BadRequestObjectResult>();
        observations.Result.Should().BeOfType<BadRequestObjectResult>();
        polygons.Result.Should().BeOfType<BadRequestObjectResult>();
        areaCounts.Should().BeOfType<BadRequestObjectResult>();
        searchService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Export_WithTooManyLocations_ReturnsBadRequestAndWritesNothing()
    {
        var exportService = new Mock<IExportService>(MockBehavior.Strict);
        var controller = new ExportController(
            exportService.Object, new Mock<IBlobStorageService>().Object, new Mock<IAnonymousUserTokenService>().Object,
            NullLogger<ExportController>.Instance, _customAreas)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Test")),
                },
            },
        };
        var request = new StartExportRequestDto { Filter = new ObservationSearchFilterDto { CustomAreaWkt = LargeArea }, SelectedColumns = [] };

        var start = await controller.StartExport(request, CancellationToken.None);
        var summary = await controller.GetSummary(request, CancellationToken.None);

        start.Result.Should().BeOfType<BadRequestObjectResult>();
        summary.Result.Should().BeOfType<BadRequestObjectResult>();
        exportService.VerifyNoOtherCalls();
    }
}
