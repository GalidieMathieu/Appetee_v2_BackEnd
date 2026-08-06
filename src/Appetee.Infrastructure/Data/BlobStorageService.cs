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
