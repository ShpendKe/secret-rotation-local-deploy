using System.Text.Json;
using Bicep.Local.Rpc;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SecretRotationExtension.EntraId;

namespace SecretRotationExtension.Test;

public class CreateOrUpdateEntraIdSecretsSpecs
{
    private readonly SecretRotationEntraIdHandler _handler;

    public CreateOrUpdateEntraIdSecretsSpecs()
    {
        _handler = new SecretRotationEntraIdHandler(
            new SecretRotatorFactory(
                A.Fake<ILogger<SecretRotator>>(),
                _ => new FakeSecretClient())
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
    }

    [Fact]
    public async Task ShouldOnlyRotateSecretsSetForRotation()
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
            .Where(s => s is { IsRenewed: true, IsExpiringSoon: true })
            .Select(s => (s.AppRegistrationName, s.SecretName))
            .Should()
            .BeEquivalentTo([
                ("AppRegWithSecret", "ExpiringSecret")
            ]);
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
            .BeEquivalentTo([
                ("AppRegWithSecret", "AnotherExpiringSecret"),
                ("AnotherAppRegWithSecret", "SomeExpiringSecret")
            ]);
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
                secret.SecretName == "Secret");

        result.AppsWithExpiringSecrets
            .Should()
            .ContainSingle(secret =>
                secret.AppRegistrationName == "AnotherAppRegWithSecret" &&
                secret.SecretName == "SomeSecret");
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
                new SecretsToRotate { AppRegistrationName = "AppRegWithSecret", SecretName = "NewSecret" }
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
