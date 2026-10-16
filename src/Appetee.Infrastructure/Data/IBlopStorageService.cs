/*
 * Purpose: Defines the Blob Storage operations used by backend media workflows.
 * Change reason: Add E-001 Phase 4 safe recognition of managed profile-media URLs.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

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
        /// Uploads an AVIF image stream and returns the blob URL.
        /// </summary>
        Task<string> UploadImageAsAvifAsync(Stream content, string? blobName = null, int quality = 50, CancellationToken ct = default);

        /// <summary>
        /// Deletes the specified blob if it exists.
        /// </summary>
        Task DeleteAsync(string blobName, CancellationToken ct = default);

        /// <summary>
        /// Resolves a URL only when it belongs to the configured storage container.
        /// </summary>
        bool TryGetBlobName(Uri blobUri, out string blobName);

        /// <summary>
        /// Returns the blob URI (does not check existence).
        /// </summary>
        Uri GetUri(string blobName);
    }
}
