using System.Security.Claims;
using Artskart3.Api.Controllers;
using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace Artskart3.Tests.Unit;

public class UserControllerTests
{
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

        var controller = new UserController(userServiceMock.Object)
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
        result.Should().BeEquivalentTo(expectedUser);

        userServiceMock.Verify(
            service => service.GetCurrentUser(userId),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_WhenSubClaimIsMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var userServiceMock = new Mock<IUserService>();

        var controller = new UserController(userServiceMock.Object)
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
        var act = async () => await controller.GetCurrentUser();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        userServiceMock.Verify(
            service => service.GetCurrentUser(It.IsAny<Guid>()),
            Times.Never);
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
}