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
    [HttpPost]
    [ProducesResponseType(typeof(ReservationInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var organizerId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var result = await reservationService.CreateAsync(organizerId, request.ToInput(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
