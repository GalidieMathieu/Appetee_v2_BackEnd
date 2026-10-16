/*
 * Purpose: Implements validated, claim-scoped current-account operations.
 * Change reason: Add E-001 Phase 4 durable account closure and cleanup handoff.
 * Created: Existing file; original timestamp was not recorded.
 * Last updated: 2026-09-11T00:32:56-06:00
 */

using Appetee.Application.Abstractions.Users;
using Appetee.Application.Dtos;
using Appetee.Application.Dtos.MealPlans;
using Appetee.Application.Requests.MealPlans;
using Appetee.Application.RowData.MealPlans;
using Appetee.Application.Models.MealPlans;
using Appetee.Application.Requests;
using Appetee.Application.utils;

namespace Appetee.Application.Services.Users;

public sealed class UserService : IUserService
{
    private readonly IUserQueries _queries;
    private readonly IUserRepository _repository;
    private readonly IAccountClosureRepository _accountClosureRepository;
    private readonly IAccountClosureProcessor _accountClosureProcessor;
    private readonly TimeProvider _timeProvider;

    public UserService(
        IUserQueries queries,
        IUserRepository repository,
        IAccountClosureRepository accountClosureRepository,
        IAccountClosureProcessor accountClosureProcessor,
        TimeProvider timeProvider)
    {
        _queries = queries;
        _repository = repository;
        _accountClosureRepository = accountClosureRepository;
        _accountClosureProcessor = accountClosureProcessor;
        _timeProvider = timeProvider;
    }

    public async Task<NutritionProfileDto> GetNutritionProfileAsync(int currentUserId, CancellationToken ct)
    {
        var row = await _queries.GetNutritionProfileAsync(currentUserId, ct)
            ?? throw new UnauthorizedException("Session is no longer valid.");
        return MapNutritionProfile(row, row.Revision);
    }

    public async Task<NutritionProfileDto> UpdateNutritionProfileAsync(
        int currentUserId, NutritionProfileRequest request, long expectedRevision, CancellationToken ct)
    {
        MealPlanRules.ValidateNutritionProfile(request, _timeProvider.GetUtcNow().UtcDateTime.Year);
        var result = await _repository.UpdateNutritionProfileAsync(currentUserId, request, expectedRevision, ct);
        return MapNutritionProfile(result.Payload, result.Revision);
    }

    private static NutritionProfileDto MapNutritionProfile(NutritionProfileRowData row, long revision)
    {
        if (row.CalculationSex is null && row.BirthYear is null && row.HeightCm is null && row.WeightKg is null)
            return new NutritionProfileDto(revision, null);
        if (row.CalculationSex is not ("male" or "female") || row.BirthYear is null
            || row.HeightCm is null || row.WeightKg is null)
            throw new InternalServerException("The saved nutrition profile is incomplete.");
        return new NutritionProfileDto(revision, new NutritionProfileValue(
            row.CalculationSex, row.BirthYear.Value, row.HeightCm.Value, row.WeightKg.Value));
    }

    public Task<CurrentUserProfileDto?> GetCurrentProfileAsync(
        int currentUserId,
        CancellationToken ct) =>
        _queries.GetCurrentProfileAsync(currentUserId, ct);

    public async Task<CurrentUserProfileDto?> UpdateCurrentProfileAsync(
        int currentUserId,
        UpdateCurrentUserProfileRequest request,
        CancellationToken ct)
    {
        if (request.Username is null && request.ImageUrl is null)
        {
            throw new ValidationException(
                "At least one profile field is required.");
        }

        if (request.Username is not null)
        {
            var username = request.Username.Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ValidationException("Username is required.");
            }

            if (username.Length > 50)
            {
                throw new ValidationException(
                    "Username too long (max 50).");
            }

            request = request with { Username = username };
        }

        if (request.ImageUrl is not null && request.ImageUrl.Length > 255)
        {
            throw new ValidationException(
                "ImageUrl too long (max 255).");
        }

        await _repository.UpdateCurrentProfileAsync(
            currentUserId,
            request,
            ct);

        return await _queries.GetCurrentProfileAsync(currentUserId, ct);
    }

    // Relational deletion commits before optional media work; the durable job makes retries safe.
    public async Task<bool> CloseCurrentAccountAsync(
        int currentUserId,
        CancellationToken ct)
    {
        var cleanupId = await _accountClosureRepository
            .CloseCurrentAccountAsync(currentUserId, ct);

        if (cleanupId is null)
        {
            return false;
        }

        await _accountClosureProcessor.TryProcessAsync(cleanupId.Value, ct);
        return true;
    }

}
