using System.Security.Claims;
using FarmAndFriends.Api.Contracts.Animals;
using FarmAndFriends.Api.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FarmAndFriends.Api.Controllers;

[ApiController]
[Authorize]
[ServiceFilter(typeof(AnimalExceptionFilter))]
public sealed class AnimalsController(AnimalEnclosureService service) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id : throw new AnimalActionException("UNAUTHENTICATED", "Entre novamente para continuar.", 401);
    private Guid OperationKey => Guid.TryParse(Request.Headers["Idempotency-Key"], out var key) && key != Guid.Empty
        ? key : throw new AnimalActionException("INVALID_IDEMPOTENCY_KEY", "Envie um Idempotency-Key UUID válido.", 400);

    [HttpGet("shop/animal-enclosure")]
    public async Task<IActionResult> EnclosureOffer(CancellationToken ct) => Ok(await service.GetEnclosureOfferAsync(ActorId, ct));
    [HttpGet("shop/animals")]
    public async Task<IActionResult> Animals(CancellationToken ct) => Ok(await service.GetAnimalOffersAsync(ActorId, ct));
    [HttpGet("shop/animal-products")]
    public IActionResult Products() { _ = ActorId; return Ok(service.GetProductOffers()); }

    [HttpPost("shop/buy-animal-enclosure")]
    public async Task<IActionResult> BuyEnclosure(CancellationToken ct) =>
        Ok(await service.ExecuteAsync(ActorId, "buy-animal-enclosure", OperationKey, ct: ct));
    [HttpPost("shop/buy-animal")]
    public async Task<IActionResult> BuyAnimal(BuyAnimalRequest request, CancellationToken ct) =>
        Ok(await service.ExecuteAsync(ActorId, "buy-animal", OperationKey, itemId: request.AnimalTypeId, ct: ct));
    [HttpPost("shop/sell-animal-product")]
    public async Task<IActionResult> Sell(SellAnimalProductRequest request, CancellationToken ct) =>
        Ok(await service.ExecuteAsync(ActorId, "sell-animal-product", OperationKey,
            itemId: request.ItemId, quantity: request.Quantity, ct: ct));
    [HttpPost("farms/{farmId:guid}/animals/{animalId:guid}/collect")]
    public Task<IActionResult> Collect(Guid farmId, Guid animalId, AnimalCycleRequest request, CancellationToken ct) =>
        Action("collect", farmId, animalId, request, ct);
    [HttpPost("farms/{farmId:guid}/animals/{animalId:guid}/help")]
    public Task<IActionResult> Help(Guid farmId, Guid animalId, AnimalCycleRequest request, CancellationToken ct) =>
        Action("help", farmId, animalId, request, ct);
    [HttpPost("farms/{farmId:guid}/animals/{animalId:guid}/steal")]
    public Task<IActionResult> Steal(Guid farmId, Guid animalId, AnimalCycleRequest request, CancellationToken ct) =>
        Action("steal", farmId, animalId, request, ct);
    private async Task<IActionResult> Action(string action, Guid farmId, Guid animalId, AnimalCycleRequest request, CancellationToken ct) =>
        Ok(await service.ExecuteAsync(ActorId, action, OperationKey, farmId, animalId, request.ExpectedCycleId, ct: ct));
    [HttpGet("farms/{farmId:guid}/animals/{animalId:guid}/cycles/{cycleId:guid}/helpers")]
    public async Task<IActionResult> Helpers(Guid farmId, Guid animalId, Guid cycleId,
        [FromQuery] string? cursor = null, [FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await service.GetHelpersAsync(ActorId, farmId, animalId, cycleId, cursor, take, ct));
}

public sealed class AnimalExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not AnimalActionException error) return;
        var problem = new ProblemDetails { Status = error.Status,
            Title = "Não foi possível concluir a ação.", Detail = error.Message };
        problem.Extensions["code"] = error.Code;
        context.Result = new ObjectResult(problem) { StatusCode = error.Status };
        context.ExceptionHandled = true;
    }
}
