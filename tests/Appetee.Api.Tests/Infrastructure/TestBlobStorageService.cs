using Appetee.Infrastructure.Data;
using System.Collections.Concurrent;

namespace Appetee.Api.Tests.Infrastructure;

internal sealed class TestBlobStorageService : IBlobStorageService
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

    public async Task<string> UploadAsync(string blobName, Stream content, string contentType, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        _blobs[blobName] = buffer.ToArray();
        return GetUri(blobName).ToString();
    }

    public Task<string> UploadImageAsAvifAsync(Stream content, string? blobName = null, int quality = 50, CancellationToken ct = default)
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
        _blobs.TryRemove(blobName, out _);
        return Task.CompletedTask;
    }

    public Uri GetUri(string blobName) => new($"https://test.local/{blobName}");
}
