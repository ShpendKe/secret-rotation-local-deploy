using System.Text.Json;
using Bicep.Local.Rpc;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SecretRotationExtension.EntraId;

namespace SecretRotationExtension.Test;

public class CreateOrUpdateEntraIdSecretsSpecs
{
    private readonly FakeSecretClient _client;
    private readonly SecretRotationEntraIdHandler _handler;

    public CreateOrUpdateEntraIdSecretsSpecs()
    {
        _client = new FakeSecretClient();
        _handler = new SecretRotationEntraIdHandler(
            new SecretRotatorFactory(
                A.Fake<ILogger<SecretRotator>>(),
                _ => _client)
        );
    }

    [Fact]
    public async Task ShouldOnlyConsiderAppRegistrationWithSecrets()
    {
        // Arrange
        var sourceProperties = new SecretRotationSourceEntraId
        {
            Id = Guid.NewGuid().ToString(),
            SecretsToRotate =
            [
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "ExpiringSecret" }
            ]
        };

        // Act
        var response = await _handler.CreateOrUpdate(
            CreateRequest(sourceProperties),
            CancellationToken.None);

        // Assert
        var result =
            JsonSerializer.Deserialize<SecretRotationSourceEntraId>(
                response.Resource.Properties,
                JsonSerializerOptions.Web)!;

        result.AppsWithExpiringSecrets
            .Should()
            .NotContain(s => s.AppRegistrationName == "AppRegWithoutSecret");
            
        _client.RotatedOrCreatedSecrets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldOnlyRotateExpiringSecretsSetForRotation()
    {
        // Arrange
        var sourceProperties = new SecretRotationSourceEntraId
        {
            Id = Guid.NewGuid().ToString(),
            SecretsToRotate =
            [
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "ExpiringSecret" },
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "Secret" },
            ]
        };

        // Act
        var response = await _handler.CreateOrUpdate(
            CreateRequest(sourceProperties),
            CancellationToken.None);

        // Assert
        var result =
            JsonSerializer.Deserialize<SecretRotationSourceEntraId>(response.Resource.Properties,
                JsonSerializerOptions.Web)!;

        result.AppsWithExpiringSecrets
            .Where(s => s is { IsRenewed: true, IsExpiringSoon: true })
            .Select(s => (s.AppRegistrationName, s.SecretName))
            .Should()
            .BeEquivalentTo([
                ("AppRegWithSecret", "ExpiringSecret")
            ]);

        _client.RotatedOrCreatedSecrets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldListAllAppsWithSecretsExpiringWithinThreshold()
    {
        // Arrange
        var sourceProperties = new SecretRotationSourceEntraId
        {
            Id = Guid.NewGuid().ToString(),
            SecretsToRotate =
            [
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "ExpiringSecret" }
            ]
        };

        // Act
        var response = await _handler.CreateOrUpdate(
            CreateRequest(sourceProperties),
            CancellationToken.None);

        // Assert
        var result =
            JsonSerializer.Deserialize<SecretRotationSourceEntraId>(response.Resource.Properties,
                JsonSerializerOptions.Web)!;

        result.AppsWithExpiringSecrets
            .Where(s => s is { IsRenewed: false, IsExpiringSoon: true })
            .Select(s => (s.AppRegistrationName, s.SecretName))
            .Should()
            .BeEmpty();
            
        _client.RotatedOrCreatedSecrets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldTakeLastStartedSecretIfMultipleSecretsWithSameNameOfSameAppRegistration()
    {
        // Arrange
        var sourceProperties = new SecretRotationSourceEntraId
        {
            Id = Guid.NewGuid().ToString(),
            SecretsToRotate =
            [
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "ExpiringSecret" }
            ]
        };

        // Act
        var response = await _handler.CreateOrUpdate(
            CreateRequest(sourceProperties),
            CancellationToken.None);

        // Assert
        var result =
            JsonSerializer.Deserialize<SecretRotationSourceEntraId>(
                response.Resource.Properties,
                JsonSerializerOptions.Web)!;

        result.AppsWithExpiringSecrets
            .Should()
            .ContainSingle(secret =>
                secret.AppRegistrationName == "AppRegWithSecret" &&
                secret.SecretName == "ExpiringSecret");

        _client.RotatedOrCreatedSecrets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShouldCreateNewSecretIfNoSecretWithSameNameExistsForAppRegistration()
    {
        // Arrange
        var sourceProperties = new SecretRotationSourceEntraId
        {
            Id = Guid.NewGuid().ToString(),
            SecretsToRotate =
            [
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "NewSecret" },
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret2", SecretName = "NewSecret2" }
            ]
        };

        // Act
        var response = await _handler.CreateOrUpdate(
            CreateRequest(sourceProperties),
            CancellationToken.None);

        // Assert
        var result =
            JsonSerializer.Deserialize<SecretRotationSourceEntraId>(
                response.Resource.Properties,
                JsonSerializerOptions.Web)!;

        result.AppsWithExpiringSecrets
            .Should()
            .ContainSingle(secret => secret.SecretName == "NewSecret");

        _client.RotatedOrCreatedSecrets.Should().HaveCount(1);
    }    

    private static ResourceSpecification CreateRequest<T>(T sourceProperties)
    {
        return new ResourceSpecification
        {
            Properties = JsonSerializer.Serialize(
                sourceProperties,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Type = typeof(T).Name
        };
    }
}
