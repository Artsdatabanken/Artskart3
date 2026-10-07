using System.Security.Claims;
using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Artskart3.Tests.Unit;

public class UserControllerTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private static readonly Guid FilterId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task GetCurrentUser_WhenSubClaimExists_ReturnsCurrentUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var expectedUser = new UserDto
        {
            Name = "Test User",
            Email = "test@example.com"
        };

        var userServiceMock = new Mock<IUserService>();

        userServiceMock
            .Setup(service => service.GetCurrentUser(userId))
            .ReturnsAsync(expectedUser);

        var controller = new UserController(userServiceMock.Object, Mock.Of<ISavedFilterService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreateClaimsPrincipal(userId)
                }
            }
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedUser);

        userServiceMock.Verify(
            service => service.GetCurrentUser(userId),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_WhenSubClaimIsMissing_ReturnsBadRequest()
    {
        // Arrange
        var userServiceMock = new Mock<IUserService>();

        var controller = new UserController(userServiceMock.Object, Mock.Of<ISavedFilterService>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        // Act
        var result = await controller.GetCurrentUser();

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();

        userServiceMock.Verify(
            service => service.GetCurrentUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetSavedFilters_WithValidSubClaim_UsesSubAsUserId()
    {
        var service = new Mock<ISavedFilterService>();
        service.Setup(s => s.GetUserFiltersAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var controller = CreateController(service, new Claim("sub", UserId.ToString()));

        var result = await controller.GetSavedFilters(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        service.Verify(s => s.GetUserFiltersAsync(UserId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSavedFilters_WithoutSubClaim_ReturnsUnauthorized()
    {
        var service = new Mock<ISavedFilterService>(MockBehavior.Strict);
        var controller = CreateController(service, new Claim("name", "Kari Nordmann"));

        var result = await controller.GetSavedFilters(CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task CreateSavedFilter_WithInvalidSubClaim_ReturnsUnauthorizedAndWritesNothing()
    {
        var service = new Mock<ISavedFilterService>(MockBehavior.Strict);
        var controller = CreateController(service, new Claim("sub", "ikke-en-guid"));

        var result = await controller.CreateSavedFilter(ValidRequest(), CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteSavedFilter_WhenNotFound_ReturnsNotFound()
    {
        var service = new Mock<ISavedFilterService>();
        service.Setup(s => s.DeleteAsync(FilterId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController(service, new Claim("sub", UserId.ToString()));

        var result = await controller.DeleteSavedFilter(FilterId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteSavedFilter_WhenDeleted_ReturnsNoContent()
    {
        var service = new Mock<ISavedFilterService>();
        service.Setup(s => s.DeleteAsync(FilterId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController(service, new Claim("sub", UserId.ToString()));

        var result = await controller.DeleteSavedFilter(FilterId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SetDefaultSavedFilter_PassesTrueToService()
    {
        var service = new Mock<ISavedFilterService>();
        service.Setup(s => s.SetDefaultAsync(FilterId, UserId, true, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController(service, new Claim("sub", UserId.ToString()));

        var result = await controller.SetDefaultSavedFilter(FilterId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        service.Verify(s => s.SetDefaultAsync(FilterId, UserId, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ClearDefaultSavedFilter_WhenNotFound_ReturnsNotFound()
    {
        var service = new Mock<ISavedFilterService>();
        service.Setup(s => s.SetDefaultAsync(FilterId, UserId, false, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController(service, new Claim("sub", UserId.ToString()));

        var result = await controller.ClearDefaultSavedFilter(FilterId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(Guid userId)
    {
        var claims = new[]
        {
            new Claim("sub", userId.ToString()),
            new Claim("name", "Test User"),
            new Claim("email", "test@example.com")
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static CreateSavedFilterRequestDto ValidRequest() => new()
    {
        Name = "Pattedyr i Trøndelag",
        Filter = new ObservationSearchFilterDto { TaxonGroupIds = [1] },
    };

    private static UserController CreateController(Mock<ISavedFilterService> service, params Claim[] claims) =>
        new(Mock.Of<IUserService>(), service.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
}
