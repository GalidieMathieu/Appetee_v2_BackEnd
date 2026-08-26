// Purpose: Binds the public Phase 4 Recipe Discovery pagination inputs.
// Created: 2026-08-25T23:50:21-06:00
// Last updated: 2026-08-25T23:50:21-06:00

namespace Appetee.Application.Requests;

/// <summary>Provides the opaque cursor and bounded page size for recipe discovery.</summary>
public sealed record RecipeDiscoveryRequest
{
    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
