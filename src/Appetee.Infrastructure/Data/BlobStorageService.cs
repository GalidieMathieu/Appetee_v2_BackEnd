using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Appetee.Application.utils;

namespace Appetee.Infrastructure.Data
{
    public sealed class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;

        public BlobStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            if (blobServiceClient is null) throw new ValidationException(nameof(blobServiceClient));
            if (configuration is null) throw new ValidationException(nameof(configuration));

            var containerName = configuration["AzureStorage:ContainerName"]
                                ?? throw new InternalServerException("Missing configuration: AzureStorage:ContainerName");

            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Ensure container exists; in production prefer explicit provisioning and RBAC.
            _containerClient.CreateIfNotExists(PublicAccessType.None);
        }

        public async Task<string> UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");
            if (content is null) throw new ValidationException("content is required");

            var blobClient = _containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" };

            if (content.CanSeek) content.Position = 0;

            await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct).ConfigureAwait(false);
            return blobClient.Uri.ToString();
        }

        public async Task<string> UploadImageAsAvifAsync(Stream content, string? blobName = null, int quality = 50, CancellationToken ct = default)
        {
            if (content is null) throw new ValidationException("content is required");

            // Generate a blob name if not provided
            blobName ??= $"{Guid.NewGuid():N}.avif";
            if (!blobName.EndsWith(".avif", StringComparison.OrdinalIgnoreCase))
                blobName = Path.ChangeExtension(blobName, ".avif");

            if (content.CanSeek) content.Position = 0;

            var blobClient = _containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = "image/avif" };

            await blobClient.UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct).ConfigureAwait(false);
            return blobClient.Uri.ToString();
        }


        public async Task DeleteAsync(string blobName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");

            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        }

        public Uri GetUri(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");
            return _containerClient.GetBlobClient(blobName).Uri;
        }
    }
}