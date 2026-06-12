using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Artskart3.Tests.Unit;

public class UserRepositoryTests
{
    [Fact]
    public async Task GetUserById_WhenUserExists_ReturnsUser()
    {
        // Arrange
        await using var context = CreateDbContext();

        var userId = Guid.NewGuid();

        var user = new User
        {
            Id = userId,
            Name = "Test User",
            Email = "test@example.com"
        };

        context.Set<User>().Add(user);
        await context.SaveChangesAsync();

        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserById(userId);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetUserById_WhenUserDoesNotExist_ReturnsNull()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = CreateRepository(context);

        // Act
        var result = await repository.GetUserById(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateUser_WhenUserIsValid_SavesAndReturnsUser()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = CreateRepository(context);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "New User",
            Email = "new@example.com"
        };

        // Act
        var result = await repository.CreateUser(user);

        // Assert
        result.Should().BeEquivalentTo(user);

        var savedUser = await context.Set<User>().FindAsync(user.Id);
        savedUser.Should().NotBeNull();
        savedUser.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task CreateUser_WhenUserIsNull_ThrowsException()
    {
        // Arrange
        await using var context = CreateDbContext();
        var repository = CreateRepository(context);

        // Act
        var act = async () => await repository.CreateUser(null!);

        // Assert
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("Error creating user");
    }

    private static UserRepository CreateRepository(ArtskartDbContext context)
    {
        var loggerMock = new Mock<ILogger<UserRepository>>();

        return new UserRepository(
            context,
            loggerMock.Object);
    }

    private static ArtskartDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ArtskartDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ArtskartDbContext(options);
    }
}