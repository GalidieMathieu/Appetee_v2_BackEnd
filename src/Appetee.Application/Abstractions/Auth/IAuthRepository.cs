using Appetee.Application.Dtos;
using System;
using System.Collections.Generic;
using System.Text;

namespace Appetee.Application.Abstractions.Auth
{
    public interface IAuthRepository
    {
        Task<int> CreateUserAsync(string username,
           string email,
           string passwordHash,
           IReadOnlyList<int> ?DietIds,
           IReadOnlyList<int> ?IngredientRestrictionIds,
           CancellationToken ct);

        //Task RevokeSessionAsync(string sessionToken, CancellationToken ct);
    }
}
