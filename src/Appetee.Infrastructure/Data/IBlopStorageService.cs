using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Appetee.Infrastructure.Data
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Uploads a raw file stream without image conversion and returns the blob URL.
        /// </summary>
        Task<string> UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default);

        /// <summary>
        /// Converts any supported image (jpg, png, avif, ...) to AVIF and uploads it.
        /// Returns the uploaded blob URL.
        /// </summary>
        Task<string> UploadImageAsAvifAsync(Stream content, string? blobName = null, int quality = 50, CancellationToken ct = default);

        /// <summary>
        /// Deletes the specified blob if it exists.
        /// </summary>
        Task DeleteAsync(string blobName, CancellationToken ct = default);

        /// <summary>
        /// Returns the blob URI (does not check existence).
        /// </summary>
        Uri GetUri(string blobName);
    }
}