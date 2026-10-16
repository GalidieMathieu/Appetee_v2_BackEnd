/*
 * Purpose: Provides deterministic in-memory Blob behavior for backend integration tests.
 * Change reason: Add E-001 Phase 4 managed-URL parsing and injected delete failures.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Infrastructure.Data;
using System.Collections.Concurrent;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed class TestBlobStorageService : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _failureLock = new();
    private int _deleteFailuresRemaining;

    public async Task<string> UploadAsync(
        string blobName,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _blobs[blobName] = buffer.ToArray();
        return GetUri(blobName).ToString();
    }

    public Task<string> UploadImageAsAvifAsync(
        Stream content,
        string? blobName = null,
        int quality = 50,
        CancellationToken ct = default)
    {
        blobName ??= $"{Guid.NewGuid():N}.avif";
        if (!blobName.EndsWith(".avif", StringComparison.OrdinalIgnoreCase))
        {
            blobName = Path.ChangeExtension(blobName, ".avif");
        }

        return UploadAsync(blobName, content, "image/avif", ct);
    }

    public Task DeleteAsync(string blobName, CancellationToken ct = default)
    {
        lock (_failureLock)
        {
            if (_deleteFailuresRemaining > 0)
            {
                _deleteFailuresRemaining--;
                throw new InvalidOperationException("Injected Blob delete failure.");
            }
        }

        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    public bool TryGetBlobName(Uri blobUri, out string blobName)
    {
        blobName = string.Empty;

        if (!blobUri.IsAbsoluteUri
            || !string.Equals(blobUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(blobUri.Host, "test.local", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = Uri.UnescapeDataString(blobUri.AbsolutePath).Trim('/');
        var segments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0
            || segments.Any(segment => segment is "." or ".." || segment.Contains('\\')))
        {
            return false;
        }

        blobName = candidate;
        return true;
    }

    public Uri GetUri(string blobName) => new($"https://test.local/{blobName}");

    public bool Contains(string blobName) => _blobs.ContainsKey(blobName);

    public void FailNextDeleteAttempts(int count)
    {
        lock (_failureLock)
        {
            _deleteFailuresRemaining = count;
        }
    }

    public void Reset()
    {
        _blobs.Clear();
        FailNextDeleteAttempts(0);
    }
}
