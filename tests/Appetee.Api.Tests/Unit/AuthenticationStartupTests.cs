/*
 * Purpose: Verifies typed authentication, recovery, and ACS Email configuration mapping and startup validation.
 * Created: 2026-08-23T01:18:52-06:00
 * Last updated: 2026-08-23T19:24:09-06:00
 */

using Appetee.Api.Extensions;
using Appetee.Application.Abstractions.Auth;
using Appetee.Application.Services.Auth;
using Appetee.Infrastructure.Auth;
using Azure.Communication.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Appetee.Api.Tests.Unit;

public sealed class AuthenticationStartupTests
{
    [Fact]
    public void Configuration_MapsAndResolvesAuthenticationRecoveryAndEmailServices()
    {
        using var provider = BuildProvider(
            Environments.Production,
            ValidConfiguration());

        var session = provider.GetRequiredService<AuthSessionSettings>();
        var recovery = provider.GetRequiredService<PasswordRecoverySettings>();
        var delivery = provider.GetRequiredService<CommunicationEmailDeliverySettings>();

        Assert.Equal(TimeSpan.FromDays(7), session.RememberedSessionLifetime);
        Assert.Equal(TimeSpan.FromDays(30), session.AbsoluteSessionLifetime);
        Assert.Equal(TimeSpan.FromMinutes(60), recovery.TokenLifetime);
        Assert.Equal(TimeSpan.FromSeconds(60), recovery.RequestCooldown);
        Assert.Equal(
            "https://appetee.test/auth/reset-password",
            delivery.ResetPageUrl.AbsoluteUri);
        Assert.Equal("DoNotReply@test.azurecomm.net", delivery.FromAddress);
        Assert.IsType<EmailClient>(provider.GetRequiredService<EmailClient>());
        Assert.IsType<AzureCommunicationPasswordRecoveryEmailSender>(
            provider.GetRequiredService<IPasswordRecoveryEmailSender>());
    }

    [Theory]
    [InlineData("CommunicationEmail:Endpoint")]
    [InlineData("CommunicationEmail:AccessKey")]
    [InlineData("CommunicationEmail:FromAddress")]
    public void Configuration_RejectsMissingCommunicationEmailSetting(string key)
    {
        var values = ValidConfiguration();
        values.Remove(key);

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<EmailClient>);
    }

    [Theory]
    [InlineData("http://test.communication.azure.com")]
    [InlineData("https://user@test.communication.azure.com")]
    [InlineData("https://test.communication.azure.com?region=test")]
    [InlineData("relative-endpoint")]
    public void Configuration_RejectsUnsafeCommunicationEmailEndpoint(string endpoint)
    {
        var values = ValidConfiguration();
        values["CommunicationEmail:Endpoint"] = endpoint;

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<EmailClient>);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("Appetee <DoNotReply@test.azurecomm.net>")]
    public void Configuration_RejectsInvalidCommunicationEmailSender(string fromAddress)
    {
        var values = ValidConfiguration();
        values["CommunicationEmail:FromAddress"] = fromAddress;

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<CommunicationEmailDeliverySettings>);
    }

    [Theory]
    [InlineData("http://appetee.test/auth/reset-password")]
    [InlineData("https://appetee.test/auth/reset-password?source=email")]
    [InlineData("https://user@appetee.test/auth/reset-password")]
    public void ProductionConfiguration_RejectsUnsafeResetPageUrl(string resetPageUrl)
    {
        var values = ValidConfiguration();
        values["PasswordRecovery:ResetPageUrl"] = resetPageUrl;

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<PasswordRecoverySettings>);
    }

    [Theory]
    [InlineData("PasswordRecovery:TokenLifetimeMinutes", "4")]
    [InlineData("PasswordRecovery:TokenLifetimeMinutes", "1441")]
    [InlineData("PasswordRecovery:RequestCooldownSeconds", "-1")]
    [InlineData("PasswordRecovery:RequestCooldownSeconds", "3601")]
    public void Configuration_RejectsUnsafeRecoveryPolicyBoundary(
        string key,
        string value)
    {
        var values = ValidConfiguration();
        values[key] = value;

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<PasswordRecoverySettings>);
    }

    [Fact]
    public void ProductionJson_TargetsFrontendRouteAndContainsNoEmailCredential()
    {
        var solutionRoot = FindSolutionRoot();
        var apiProject = Path.Combine(solutionRoot, "src", "Appetee.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProject)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Production.json")
            .Build();

        Assert.Equal(
            "https://appetee-awhsgkeqamfqh0dg.eastus-01.azurewebsites.net/auth/reset-password",
            configuration["PasswordRecovery:ResetPageUrl"]);
        Assert.Equal(
            "https://comuncationserviceappetee.unitedstates.communication.azure.com/",
            configuration["CommunicationEmail:Endpoint"]);
        Assert.Equal(
            "DoNotReply@dd7dfcdc-41c2-4501-964c-eebcf80e427a.azurecomm.net",
            configuration["CommunicationEmail:FromAddress"]);
        Assert.Null(configuration["CommunicationEmail:AccessKey"]);
    }

    [Fact]
    public void DevelopmentJson_UsesLocalFrontendAndKeyVaultWithoutEmailCredential()
    {
        var solutionRoot = FindSolutionRoot();
        var apiProject = Path.Combine(solutionRoot, "src", "Appetee.Api");
        var configuration = new ConfigurationBuilder()
            .SetBasePath(apiProject)
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json")
            .Build();

        Assert.Equal(
            "https://localhost:4200/auth/reset-password",
            configuration["PasswordRecovery:ResetPageUrl"]);
        Assert.Equal(
            "https://appeteekeystorage.vault.azure.net/",
            configuration["KeyVault:Uri"]);
        Assert.Equal(
            "https://comuncationserviceappetee.unitedstates.communication.azure.com/",
            configuration["CommunicationEmail:Endpoint"]);
        Assert.Equal(
            "DoNotReply@dd7dfcdc-41c2-4501-964c-eebcf80e427a.azurecomm.net",
            configuration["CommunicationEmail:FromAddress"]);
        Assert.Null(configuration["CommunicationEmail:AccessKey"]);
    }

    [Fact]
    public void Configuration_RejectsAbsoluteSessionShorterThanRememberedSession()
    {
        var values = ValidConfiguration();
        values["Authentication:AbsoluteSessionDays"] = "6";

        using var provider = BuildProvider(Environments.Production, values);

        Assert.Throws<OptionsValidationException>(
            provider.GetRequiredService<AuthSessionSettings>);
    }

    private static ServiceProvider BuildProvider(
        string environmentName,
        Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddAppeteeAuthentication(
            configuration,
            new StubHostEnvironment(environmentName));
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidConfiguration() =>
        new()
        {
            ["Authentication:CookieName"] = "__Host-appetee",
            ["Authentication:RememberedSessionDays"] = "7",
            ["Authentication:AbsoluteSessionDays"] = "30",
            ["PasswordRecovery:TokenLifetimeMinutes"] = "60",
            ["PasswordRecovery:RequestCooldownSeconds"] = "60",
            ["PasswordRecovery:ResetPageUrl"] =
                "https://appetee.test/auth/reset-password",
            ["CommunicationEmail:Endpoint"] =
                "https://test.communication.azure.com",
            ["CommunicationEmail:AccessKey"] = "unit-test-placeholder-key",
            ["CommunicationEmail:FromAddress"] =
                "DoNotReply@test.azurecomm.net",
        };

    private static string FindSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Appetee.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate the Appetee solution root.");
    }

    /// <summary>Supplies a controlled environment name to startup configuration tests.</summary>
    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Appetee.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
