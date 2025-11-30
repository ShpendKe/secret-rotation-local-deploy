using System.Text.Json;
using Bicep.Local.Rpc;
using FakeItEasy;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using SecretRotationExtension.EntraId;

namespace SecretRotationExtension.Test;

public class CreateOrUpdateSpecs
{
    private readonly SecretRotationEntraIdHandler _handler;

    private readonly IEnumerable<AppRegistration> _sampleAppRegistrations =
    [
        new(
            "AppRegWithoutSecret",
            Guid.NewGuid().ToString(),
            []),
        new(
            "AppRegWithSecret",
            Guid.NewGuid().ToString(),
            [
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("ExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10)),
                new Secret("AnotherExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10))
            ]),
        new(
            "AnotherAppRegWithSecret",
            Guid.NewGuid().ToString(),
            [
                new Secret("Secret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(180)),
                new Secret("SomeExpiringSecret", Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(10))
            ])
    ];

    private readonly ISecretClient _secretClientFake;

    public CreateOrUpdateSpecs()
    {
        _secretClientFake = A.Fake<ISecretClient>();

        _handler = new SecretRotationEntraIdHandler(
            A.Fake<ILogger<SecretRotationEntraIdHandler>>(),
            _secretClientFake
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

        SetupClient();

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

        SetupClient();

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

        SetupClient();

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

    private void SetupClient()
    {
        A.CallTo(() => _secretClientFake.RecreateSecret(A<string>.Ignored, A<string>.Ignored, A<int>.Ignored))
            .ReturnsLazily(a => Task.FromResult(
                (a.Arguments[0] as string, DateTimeOffset.UtcNow.AddDays((int)a.Arguments[2]!)))!);

        A.CallTo(() => _secretClientFake.GetAppRegistrationWithExpiringDates())
            .Returns(_sampleAppRegistrations);
    }
}
