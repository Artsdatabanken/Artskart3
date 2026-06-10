using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Moq;

namespace Artskart3.Tests.Unit;

public class UserServiceTests
{
    [Fact]
    public async Task GetCurrentUser_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var expectedUser = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com"
        };

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync(expectedUser);

        var service = new UserService(userRepositoryMock.Object);

        // Act
        var result = await service.GetCurrentUser(userId);

        // Assert
        result.Should().BeEquivalentTo(expectedUser);

        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserDoesNotExist_ThrowsException()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync((User?)null);

        var service = new UserService(userRepositoryMock.Object);

        // Act
        var act = async () => await service.GetCurrentUser(userId);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error getting user");

        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateUser_WhenUserExists_ReturnsExistingUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var incomingUser = new User
        {
            Id = userId,
            Name = "Incoming User",
            Email = "incoming@example.com"
        };

        var existingUser = new User
        {
            Id = userId,
            Name = "Existing User",
            Email = "existing@example.com"
        };

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync(existingUser);

        var service = new UserService(userRepositoryMock.Object);

        // Act
        var result = await service.GetOrCreateUser(incomingUser);

        // Assert
        result.Should().BeEquivalentTo(existingUser);

        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);

        userRepositoryMock.Verify(
            repository => repository.CreateUser(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task GetOrCreateUser_WhenUserDoesNotExist_CreatesAndReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var newUser = new User
        {
            Id = userId,
            Name = "New User",
            Email = "new@example.com"
        };

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync((User?)null);

        userRepositoryMock
            .Setup(repository => repository.CreateUser(newUser))
            .ReturnsAsync(newUser);

        var service = new UserService(userRepositoryMock.Object);

        // Act
        var result = await service.GetOrCreateUser(newUser);

        // Assert
        result.Should().BeEquivalentTo(newUser);

        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);

        userRepositoryMock.Verify(
            repository => repository.CreateUser(newUser),
            Times.Once);
    }

    [Fact]
    public async Task GetOrCreateUser_WhenUserIsNull_ThrowsException()
    {
        // Arrange
        var userRepositoryMock = new Mock<IUserRepository>();
        var service = new UserService(userRepositoryMock.Object);

        // Act
        var act = async () => await service.GetOrCreateUser(null!);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error creating user");

        userRepositoryMock.Verify(
            repository => repository.GetUserById(It.IsAny<Guid>()),
            Times.Never);

        userRepositoryMock.Verify(
            repository => repository.CreateUser(It.IsAny<User>()),
            Times.Never);
    }
}