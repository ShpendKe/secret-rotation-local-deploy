using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Microsoft.Extensions.DependencyInjection;
using SecretRotationExtension.EntraId;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension(
        name: "SecretRotation",
        version: "0.0.1",
        isSingleton: true,
        typeAssembly: typeof(Program).Assembly)
    .WithResourceHandler<SecretRotationEntraIdHandler>();

var app = builder.Build();

app.MapBicepExtension();

await app.RunAsync();
