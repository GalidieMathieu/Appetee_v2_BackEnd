using Appetee.Application.Models.Catalogue;
using System.Net.Http.Json;

namespace Appetee.Api.Tests.Infrastructure;

internal static class CatalogueResponseExtensions
{
    internal static async Task<T?> ReadCatalogueResourceAsync<T>(this HttpContent content)
    {
        var envelope=await content.ReadFromJsonAsync<CatalogueResourceDto<T>>();
        return envelope is null ? default : envelope.Resource;
    }
}
