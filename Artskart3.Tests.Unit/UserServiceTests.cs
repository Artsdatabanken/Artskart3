using Artskart3.Core.Application.DTOs;
using Artskart3.Core.Application.Services.Implementations;
using Artskart3.Core.Domain.Entities;
using Artskart3.Core.Domain.RepositoryInterfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Artskart3.Tests.Unit;

public class UserServiceTests()
{
    [Fact]
    public async Task GetCurrentUser_WhenUserExists_ReturnsUser()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com"
        };

        var expectedUser = new UserDto
        {
            Name = "Test User",
            Email = "test@example.com",
        };

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync(user);

        var service = new UserService(userRepositoryMock.Object, NullLogger<UserService>.Instance);

        // Act
        var result = await service.GetCurrentUser(userId);

        // Assert
        result.Should().BeEquivalentTo(expectedUser);

        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);
    }

    [Fact]
    public async Task GetCurrentUser_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var userRepositoryMock = new Mock<IUserRepository>();

        userRepositoryMock
            .Setup(repository => repository.GetUserById(userId))
            .ReturnsAsync((User?)null);

        var service = new UserService(userRepositoryMock.Object, NullLogger<UserService>.Instance);
        var result = await service.GetCurrentUser(userId);
        result.Should().BeNull();
        userRepositoryMock.Verify(
            repository => repository.GetUserById(userId),
            Times.Once);
    }

    /// <summary>
    /// Uendret profil skal ikke gi en skriveoperasjon. GetOrCreateUser kjører ved
    /// HVER innlogging, så en blind oppdatering ville betydd én skriving per
    /// pålogging uten at noe var annerledes.
    /// </summary>
    [Fact]
    public async Task GetOrCreateUser_WhenProfileIsUnchanged_DoesNotWrite()
    {
        var userId = Guid.NewGuid();
        var existingUser = CreateUser(userId, "Kari Nordmann", "kari@example.com");

        var repository = CreateRepositoryReturning(existingUser);
        var service = new UserService(repository.Object, NullLogger<UserService>.Instance);

        var result = await service.GetOrCreateUser(
            CreateUser(userId, "Kari Nordmann", "kari@example.com"));

        result.Should().BeSameAs(existingUser);
        repository.Verify(r => r.UpdateUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.CreateUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Kjernen i rettelsen: den eksisterende raden ble tidligere returnert urørt,
    /// så et navnebytte hos identitetstjenesten slo aldri gjennom. Profildataene
    /// ble stående slik de var ved aller første innlogging.
    /// </summary>
    [Theory]
    [InlineData("Kari Hansen", "kari@example.com")]
    [InlineData("Kari Nordmann", "kari.hansen@example.com")]
    [InlineData("Kari Hansen", "kari.hansen@example.com")]
    public async Task GetOrCreateUser_WhenProfileChangedAtIdentityProvider_UpdatesStoredValues(
        string newName, string newEmail)
    {
        var userId = Guid.NewGuid();
        var existingUser = CreateUser(userId, "Kari Nordmann", "kari@example.com");

        // Forankret i fortiden med vilje. Uten dette kommer både den lagrede og den
        // nye UpdatedAt fra DateTime.UtcNow med få mikrosekunders mellomrom, og
        // BeAfter ville vært avhengig av klokkeoppløsningen.
        var lastWrittenAt = DateTime.UtcNow.AddDays(-30);
        existingUser.UpdatedAt = lastWrittenAt;

        var repository = CreateRepositoryReturning(existingUser);
        var service = new UserService(repository.Object, NullLogger<UserService>.Instance);

        var result = await service.GetOrCreateUser(CreateUser(userId, newName, newEmail));

        result.Name.Should().Be(newName);
        result.Email.Should().Be(newEmail);
        result.UpdatedAt.Should().BeAfter(lastWrittenAt, "UpdatedAt skal vise når profilen faktisk ble endret");

        repository.Verify(r => r.UpdateUser(existingUser, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// OnTicketReceived setter string.Empty når name- eller email-claimet mangler.
    /// Uten vakten mot tomme verdier ville en slik innlogging tømt en profil som
    /// var i orden.
    /// </summary>
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("", "kari@example.com")]
    [InlineData("Kari Nordmann", "")]
    public async Task GetOrCreateUser_WhenIdentityProviderSendsBlanks_KeepsStoredProfile(
        string incomingName, string incomingEmail)
    {
        var userId = Guid.NewGuid();
        var existingUser = CreateUser(userId, "Kari Nordmann", "kari@example.com");

        var repository = CreateRepositoryReturning(existingUser);
        var service = new UserService(repository.Object, NullLogger<UserService>.Instance);

        var result = await service.GetOrCreateUser(CreateUser(userId, incomingName, incomingEmail));

        result.Name.Should().Be("Kari Nordmann");
        result.Email.Should().Be("kari@example.com");
        repository.Verify(r => r.UpdateUser(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static User CreateUser(Guid id, string? name, string? email) => new()
    {
        Id = id,
        Name = name,
        Email = email
    };

    private static Mock<IUserRepository> CreateRepositoryReturning(User existingUser)
    {
        var repository = new Mock<IUserRepository>();

        repository
            .Setup(r => r.GetUserById(existingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        // Speiler den ekte implementasjonen, som returnerer entiteten den lagret.
        repository
            .Setup(r => r.UpdateUser(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        return repository;
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

        var service = new UserService(userRepositoryMock.Object, NullLogger<UserService>.Instance);

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
        var service = new UserService(userRepositoryMock.Object, NullLogger<UserService>.Instance);

        // Act
        var act = async () => await service.GetOrCreateUser(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();

        userRepositoryMock.Verify(
            repository => repository.GetUserById(It.IsAny<Guid>()),
            Times.Never);

        userRepositoryMock.Verify(
            repository => repository.CreateUser(It.IsAny<User>()),
            Times.Never);
    }
}
