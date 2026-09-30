using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MeetingRoomBooking.Api.Dtos;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reservations")]
public sealed class ReservationsController(IReservationService reservationService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReservationInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10, CancellationToken cancellationToken = default)
        => Ok(await reservationService.ListAsync(GetActorId(), page, pageSize, cancellationToken));

    [HttpGet("mine")]
    [ProducesResponseType(typeof(PagedResult<ReservationInfo>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine([FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10, CancellationToken cancellationToken = default)
        => Ok(await reservationService.MineAsync(GetActorId(), page, pageSize, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReservationInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
        => Ok(await reservationService.GetAsync(GetActorId(), id, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(ReservationInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var result = await reservationService.CreateAsync(GetActorId(), request.ToInput(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ReservationInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, CreateReservationRequest request, CancellationToken cancellationToken)
        => Ok(await reservationService.UpdateAsync(GetActorId(), id, request.ToInput(), cancellationToken));

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        await reservationService.CancelAsync(GetActorId(), id, cancellationToken);
        return NoContent();
    }

    private int GetActorId() => int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
