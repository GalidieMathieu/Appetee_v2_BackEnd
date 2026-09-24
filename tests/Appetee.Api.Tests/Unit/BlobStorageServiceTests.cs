/*
 * Purpose: Verifies managed Blob URL recognition without performing Azure network operations.
 * Change reason: Add E-001 Phase 4 negative security coverage for profile-media deletion targets.
 * Created: 2026-09-11T00:32:56-06:00
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Infrastructure.Data;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Appetee.Api.Tests.Unit;

public sealed class BlobStorageServiceTests
{
    [Theory]
    [InlineData("https://appetee.blob.core.windows.net/images/users/profile.avif", true)]
    [InlineData("https://appetee.blob.core.windows.net/other/users/profile.avif", false)]
    [InlineData("https://other.blob.core.windows.net/images/users/profile.avif", false)]
    [InlineData("http://appetee.blob.core.windows.net/images/users/profile.avif", false)]
    [InlineData("https://appetee.blob.core.windows.net/images/users%5C..%5Crecipe.avif", false)]
    public void TryGetBlobName_OnlyAcceptsConfiguredContainerUrls(
        string value,
        bool expected)
    {
        var service = CreateService();

        var accepted = service.TryGetBlobName(new Uri(value), out var blobName);

        Assert.Equal(expected, accepted);
        Assert.Equal(expected ? "users/profile.avif" : string.Empty, blobName);
    }

    private static BlobStorageService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureStorage:ContainerName"] = "images",
            })
            .Build();

        return new BlobStorageService(
            new BlobServiceClient(
                new Uri("https://appetee.blob.core.windows.net")),
            configuration,
            NullLogger<BlobStorageService>.Instance);
    }
}
