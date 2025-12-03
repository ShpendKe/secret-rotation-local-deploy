using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SecretRotationExtension;
using SecretRotationExtension.EntraId;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddSingleton<SecretRotatorFactory>(provider =>
        new SecretRotatorFactory(
            provider.GetRequiredService<ILogger<SecretRotator>>(),
            tenantId => new EntraIdSecretClient(tenantId)))
    .AddBicepExtension(
        name: "SecretRotation",
        version: ThisAssembly.AssemblyInformationalVersion.Split('+')[0],
        isSingleton: true,
        typeAssembly: typeof(Program).Assembly)
    .WithResourceHandler<SecretRotationEntraIdHandler>();

var app = builder.Build();

app.MapBicepExtension();

await app.RunAsync();
