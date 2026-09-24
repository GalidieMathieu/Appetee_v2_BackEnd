/*
 * Purpose: Provides bounded Azure Blob operations for backend media workflows.
 * Change reason: Add E-001 Phase 4 managed-URL validation before profile-media deletion.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.utils;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Appetee.Infrastructure.Data
{
    public sealed class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<BlobStorageService> _logger;
        private readonly string _containerName;
        private readonly string _storageAccountName;

        public BlobStorageService(
            BlobServiceClient blobServiceClient,
            IConfiguration configuration,
            ILogger<BlobStorageService> logger)
        {
            if (blobServiceClient is null) throw new ValidationException(nameof(blobServiceClient));
            if (configuration is null) throw new ValidationException(nameof(configuration));
            _logger = logger ?? throw new ValidationException(nameof(logger));

            var containerName = configuration["AzureStorage:ContainerName"]
                ?? throw new InvalidOperationException("Missing configuration: AzureStorage:ContainerName");

            if (string.IsNullOrWhiteSpace(containerName))
                throw new InvalidOperationException("Missing configuration: AzureStorage:ContainerName");

            _containerName = containerName;
            _storageAccountName = GetStorageAccountName(blobServiceClient.Uri);
            _containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            _logger.LogInformation(
                "Azure Blob client configured for storage account {StorageAccountName} and container {ContainerName}",
                _storageAccountName,
                _containerName);
        }

        public async Task<string> UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");
            if (content is null) throw new ValidationException("content is required");

            var blobClient = _containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = contentType ?? "application/octet-stream" };
            var length = content.CanSeek ? content.Length : (long?)null;

            if (content.CanSeek) content.Position = 0;

            try
            {
                _logger.LogDebug(
                    "Uploading blob {BlobName} to container {ContainerName} with content type {ContentType} and stream length {StreamLength}",
                    blobName,
                    _containerName,
                    headers.ContentType,
                    length);

                var response = await blobClient
                    .UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "Blob upload completed. Operation {Operation}; Status {Status}; ErrorCode {ErrorCode}; ContainerName {ContainerName}; BlobName {BlobName}",
                    nameof(UploadAsync),
                    response.GetRawResponse().Status,
                    null,
                    _containerName,
                    blobName);

                return ToSafeUrl(blobClient.Uri);
            }
            catch (RequestFailedException ex)
            {
                LogAzureFailure(ex, nameof(UploadAsync), blobName);
                throw;
            }
        }

        public async Task<string> UploadImageAsAvifAsync(Stream content, string? blobName = null, int quality = 50, CancellationToken ct = default)
        {
            if (content is null) throw new ValidationException("content is required");

            blobName ??= $"{Guid.NewGuid():N}.avif";
            if (!blobName.EndsWith(".avif", StringComparison.OrdinalIgnoreCase))
            {
                blobName = Path.ChangeExtension(blobName, ".avif");
            }

            if (content.CanSeek) content.Position = 0;

            var blobClient = _containerClient.GetBlobClient(blobName);
            var headers = new BlobHttpHeaders { ContentType = "image/avif" };
            var length = content.CanSeek ? content.Length : (long?)null;

            try
            {
                _logger.LogDebug(
                    "Uploading AVIF image blob {BlobName} to container {ContainerName} with stream length {StreamLength}",
                    blobName,
                    _containerName,
                    length);

                var response = await blobClient
                    .UploadAsync(content, new BlobUploadOptions { HttpHeaders = headers }, ct)
                    .ConfigureAwait(false);

                _logger.LogInformation(
                    "AVIF image upload completed. Operation {Operation}; Status {Status}; ErrorCode {ErrorCode}; ContainerName {ContainerName}; BlobName {BlobName}",
                    nameof(UploadImageAsAvifAsync),
                    response.GetRawResponse().Status,
                    null,
                    _containerName,
                    blobName);

                return ToSafeUrl(blobClient.Uri);
            }
            catch (RequestFailedException ex)
            {
                LogAzureFailure(ex, nameof(UploadImageAsAvifAsync), blobName);
                throw;
            }
        }

        public async Task DeleteAsync(string blobName, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");

            var blobClient = _containerClient.GetBlobClient(blobName);

            try
            {
                _logger.LogDebug(
                    "Deleting blob {BlobName} from container {ContainerName}",
                    blobName,
                    _containerName);

                var response = await blobClient.DeleteIfExistsAsync(cancellationToken: ct).ConfigureAwait(false);

                _logger.LogInformation(
                    "Blob delete completed. Operation {Operation}; Status {Status}; ErrorCode {ErrorCode}; ContainerName {ContainerName}; BlobName {BlobName}; Deleted {Deleted}",
                    nameof(DeleteAsync),
                    response.GetRawResponse().Status,
                    null,
                    _containerName,
                    blobName,
                    response.Value);
            }
            catch (RequestFailedException ex)
            {
                LogAzureFailure(ex, nameof(DeleteAsync), blobName);
                throw;
            }
        }

        // Only exact HTTPS URLs under this configured account/container can become delete targets.
        public bool TryGetBlobName(Uri blobUri, out string blobName)
        {
            blobName = string.Empty;

            if (!blobUri.IsAbsoluteUri
                || !string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(blobUri.Host, _containerClient.Uri.Host, StringComparison.OrdinalIgnoreCase)
                || blobUri.Port != _containerClient.Uri.Port)
            {
                return false;
            }

            var path = Uri.UnescapeDataString(blobUri.AbsolutePath).TrimStart('/');
            var containerPrefix = $"{_containerName}/";

            if (!path.StartsWith(containerPrefix, StringComparison.Ordinal)
                || path.Length == containerPrefix.Length)
            {
                return false;
            }

            var candidate = path[containerPrefix.Length..];
            var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length == 0
                || segments.Any(segment => segment is "." or ".." || segment.Contains('\\')))
            {
                return false;
            }

            blobName = candidate;
            return true;
        }

        public Uri GetUri(string blobName)
        {
            if (string.IsNullOrWhiteSpace(blobName)) throw new ValidationException("blobName is required");
            return _containerClient.GetBlobClient(blobName).Uri;
        }

        private void LogAzureFailure(RequestFailedException ex, string operation, string blobName)
        {
            _logger.LogError(
                ex,
                "Azure Blob operation failed. Operation {Operation}; Status {Status}; ErrorCode {ErrorCode}; ContainerName {ContainerName}; BlobName {BlobName}",
                operation,
                ex.Status,
                ex.ErrorCode,
                _containerName,
                blobName);
        }

        private static string GetStorageAccountName(Uri serviceUri)
        {
            var host = serviceUri.Host;
            var dotIndex = host.IndexOf('.');
            return dotIndex > 0 ? host[..dotIndex] : host;
        }

        private static string ToSafeUrl(Uri uri)
        {
            var builder = new UriBuilder(uri)
            {
                Query = string.Empty,
                Fragment = string.Empty
            };

            return builder.Uri.ToString();
        }
    }
}
