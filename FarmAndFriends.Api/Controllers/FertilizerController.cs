using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Fertilizer;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(AnimalExceptionFilter))]
public sealed class FertilizerController(FertilizerService service) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new AnimalActionException("UNAUTHENTICATED", "Entre novamente para continuar.", 401);
    private Guid OperationKey => Guid.TryParse(Request.Headers["Idempotency-Key"], out var key) && key != Guid.Empty
        ? key : throw new AnimalActionException("INVALID_IDEMPOTENCY_KEY", "Envie um Idempotency-Key UUID válido.", 400);

    [HttpGet("shop/fertilizer")]
    public IActionResult Offer() { _ = ActorId; return Ok(service.GetOffer()); }

    [HttpPost("shop/buy-fertilizer")]
    public async Task<IActionResult> Buy(BuyFertilizerRequest request, CancellationToken ct) =>
        Ok(await service.BuyAsync(ActorId, OperationKey, request.Quantity, ct));

    [HttpPost("farms/{farmId:guid}/plots/{plotId:guid}/fertilize")]
    public async Task<IActionResult> Fertilize(Guid farmId, Guid plotId,
        FertilizePlotRequest request, CancellationToken ct) =>
        Ok(await service.ApplyAsync(ActorId, OperationKey, farmId, plotId, request, ct));
}
