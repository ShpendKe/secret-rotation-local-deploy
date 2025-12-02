using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SecretRotationExtension.EntraId;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddSingleton<ISecretClient, EntraIdSecretClient>()
    .AddBicepExtension(
        name: "SecretRotation",
        version: ThisAssembly.AssemblyInformationalVersion.Split('+')[0],
        isSingleton: true,
        typeAssembly: typeof(Program).Assembly)
    .WithResourceHandler<SecretRotationEntraIdHandler>();

var app = builder.Build();

app.MapBicepExtension();

await app.RunAsync();
