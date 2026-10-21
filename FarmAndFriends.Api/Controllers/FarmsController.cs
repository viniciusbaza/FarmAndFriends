using System.Data;
using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Farms;
using FarmAndFriends.Api.Domain.Services;
using FarmAndFriends.Api.Infrastructure.Data;
using FarmAndFriends.Api.Mappers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Route("farms")]
[Authorize]
[ServiceFilter(typeof(AnimalExceptionFilter))]
public class FarmsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FriendshipService _friendshipService;
    private readonly CropCareService _cropCareService;
    private readonly PestService _pestService;
    private readonly LandExpansionService _landExpansionService;
    private readonly AnimalEnclosureService _animalService;
    private readonly FertilizerService _fertilizerService;

    public FarmsController(
        AppDbContext context,
        FriendshipService friendshipService,
        CropCareService cropCareService,
        PestService pestService,
        LandExpansionService landExpansionService,
        AnimalEnclosureService animalService,
        FertilizerService fertilizerService)
    {
        _context = context;
        _friendshipService = friendshipService;
        _cropCareService = cropCareService;
        _pestService = pestService;
        _landExpansionService = landExpansionService;
        _animalService = animalService;
        _fertilizerService = fertilizerService;
    }

    [HttpGet("my")]
    public async Task<IActionResult> GetMyFarm(CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farmId = await _context.Farms
            .AsNoTracking()
            .Where(farm => farm.UserId == userId)
            .Select(farm => (Guid?)farm.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!farmId.HasValue)
            return NotFound("Fazenda não encontrada.");

        await _pestService.ProcessFarmAsync(farmId.Value, cancellationToken);
        var snapshot = await LoadFarmSnapshotAsync(
            farmId.Value,
            userId,
            cancellationToken);

        if (snapshot == null)
            return NotFound("Fazenda não encontrada.");

        var farm = snapshot.Farm;
        var animalState = await _animalService.GetStateAsync(
            userId,
            farm.Id,
            cancellationToken);

        return Ok(FarmMapper.ToFarmResponse(
            farm,
            snapshot.ObservedAt,
            snapshot.CareStates,
            _pestService.GetNextPestCheckAt(farm.Plots, snapshot.ObservedAt),
            _landExpansionService.GetOffer(farm.Id, farm.Plots),
            animalState: animalState,
            fertilizedAt: snapshot.FertilizedAt));
    }

    [HttpGet("{farmId}")]
    public async Task<IActionResult> GetPublicFarm(Guid farmId, CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farmIdentity = await _context.Farms
            .AsNoTracking()
            .Where(farm => farm.Id == farmId)
            .Select(farm => new { farm.Id, farm.UserId })
            .SingleOrDefaultAsync(cancellationToken);

        if (farmIdentity == null)
            return NotFound("Fazenda não encontrada.");

        if (farmIdentity.UserId == userId)
            return BadRequest(
                "Use /farms/my para acessar sua própria fazenda.");

        if (!await _friendshipService.AreFriendsAsync(
                userId,
                farmIdentity.UserId))
        {
            return Forbid();
        }

        await _pestService.ProcessFarmAsync(farmId, cancellationToken);
        var snapshot = await LoadFarmSnapshotAsync(
            farmId,
            userId,
            cancellationToken);

        if (snapshot == null)
            return NotFound("Fazenda não encontrada.");

        var farm = snapshot.Farm;
        var animalState = await _animalService.GetStateAsync(
            userId,
            farm.Id,
            cancellationToken);

        return Ok(FarmMapper.ToFarmResponse(
            farm,
            snapshot.ObservedAt,
            snapshot.CareStates,
            _pestService.GetNextPestCheckAt(farm.Plots, snapshot.ObservedAt),
            landOffer: null,
            isOwnerView: false,
            animalState: animalState,
            fertilizedAt: snapshot.FertilizedAt));
    }

    [HttpGet("{farmId}/theft-log")]
    public async Task<IActionResult> GetTheftLog(Guid farmId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farm = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == farmId);

        if (farm == null)
            return NotFound("Fazenda não encontrada.");

        if (farm.UserId != userId)
            return Forbid();

        var logs = await _context.TheftLogs
            .AsNoTracking()
            .Include(log => log.Seed)
            .Include(log => log.ThiefUser)
            .Where(log => log.FarmId == farmId)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new TheftLogResponse(
                log.ThiefUser.Username,
                log.Seed.CropName,
                log.Quantity,
                log.GotBonus,
                log.CreatedAt))
            .ToListAsync();

        return Ok(logs);
    }

    [HttpGet("{farmId}/notifications")]
    public async Task<IActionResult> GetNotifications(Guid farmId)
    {
        if (!TryGetCurrentUserId(out var userId))
            return Unauthorized();

        var farm = await _context.Farms
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == farmId);

        if (farm == null)
            return NotFound();

        if (farm.UserId != userId)
            return Forbid();

        var notifications = await _context.TheftLogs
            .AsNoTracking()
            .Include(log => log.ThiefUser)
            .Include(log => log.Seed)
            .Where(log => log.FarmId == farmId)
            .OrderByDescending(log => log.CreatedAt)
            .Take(10)
            .Select(log => new TheftNotificationResponse(
                log.GotBonus
                    ? $"{log.ThiefUser.Username} te roubou {log.Quantity} {log.Seed.CropName}(s) e ganhou bônus, sortudo! ⭐"
                    : $"{log.ThiefUser.Username} roubou {log.Quantity} {log.Seed.CropName}(s)",
                log.CreatedAt))
            .ToListAsync();

        return Ok(notifications);
    }

    private async Task<FarmReadSnapshot?> LoadFarmSnapshotAsync(
        Guid farmId,
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var farm = await LoadFarmAsync(farmId, cancellationToken);
        if (farm == null)
            return null;

        var observedAt = _pestService.UtcNow;
        var careStates = await _cropCareService.GetFarmCareStatesAsync(
            farm.Id,
            viewerUserId,
            farm.UserId,
            farm.Plots,
            observedAt,
            cancellationToken);
        var fertilizedAt = await _fertilizerService.GetCurrentApplicationsAsync(
            farm.Id,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(farm, observedAt, careStates, fertilizedAt);
    }

    private async Task<Domain.Entities.Farm?> LoadFarmAsync(
        Guid farmId,
        CancellationToken cancellationToken) =>
        await _context.Farms
            .AsNoTracking()
            .Include(farm => farm.User)
            .Include(farm => farm.Plots)
            .SingleOrDefaultAsync(farm => farm.Id == farmId, cancellationToken);

    private sealed record FarmReadSnapshot(
        Domain.Entities.Farm Farm,
        DateTime ObservedAt,
        IReadOnlyDictionary<Guid, PlotCropCareState> CareStates,
        IReadOnlyDictionary<Guid, DateTime> FertilizedAt);

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            out userId);
}
