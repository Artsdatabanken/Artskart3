using System.Net;
using System.Net.Http.Json;
using Artskart3.Core.Application.Services.Interfaces;
using Artskart3.Core.Domain.Entities;
using Artskart3.Infrastructure.Data;
using Artskart3.Tests.Integration.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Artskart3.Tests.Integration.Tests;

[Collection(nameof(DatabaseCollection))]
public class UserIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserIntegrationTests(DatabaseFixture db)
    {
        _factory = new CustomWebApplicationFactory(
            db.ConnectionString,
            useTestAuthentication: true);
        
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-CSRF", "1");
    }
    
    public Task InitializeAsync() => Task.CompletedTask;
    
    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetCurrentUser_WhenAuthenticatedUserExists_ReturnUser()
    {
        var userId = Guid.NewGuid();
        var existingUser = new User
        {
            Id = userId,
            Name = "Existing Integration User",
            Email = "existing-user@example.com"
        };
        
        await AddUserToDatabase(existingUser);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/user");
        request.Headers.Add("X-Test-UserId", userId.ToString());
        
        var response = await _client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var returnedUser = await response.Content.ReadFromJsonAsync<User>();

        returnedUser.Should().NotBeNull();
        returnedUser!.Id.Should().Be(userId);
        returnedUser.Name.Should().Be(existingUser.Name);
        returnedUser.Email.Should().Be(existingUser.Email);
    }

    [Fact]
    public async Task GetOrCreateUser_WhenUserAlreadyExists_ReturnsExistingUserAndDoesNotCreateDuplicate()
    {
        var userId = Guid.NewGuid();
        
        var existingUser = new User
        {
            Id = userId,
            Name = "Existing Integration User",
            Email = "existing-user@example.com"
        };
        
        await AddUserToDatabase(existingUser);

        var incomingUser = new User
        {
            Id = userId,
            Name = "Changed User",
            Email = "changed@example.com"
        };

        await using var scope = _factory.Services.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        var result = await userService.GetOrCreateUser(incomingUser);
        
        result.Id.Should().Be(userId);
        result.Name.Should().Be(existingUser.Name);
        result.Email.Should().Be(existingUser.Email);
        
        var dbContext = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        var usesWithTheSameId = await dbContext.Set<User>()
            .Where(user => user.Id == userId)
            .ToListAsync();

        usesWithTheSameId.Should().ContainSingle();
    }

    private async Task AddUserToDatabase(User user)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ArtskartDbContext>();
        dbContext.Set<User>().Add(user);
        await dbContext.SaveChangesAsync();
    }
}