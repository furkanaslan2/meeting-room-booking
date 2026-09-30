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
[Route("api/rooms")]
public sealed class RoomsController(IRoomService roomService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoomInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<RoomInfo>> List(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int? officeId = null,
        [FromQuery, Range(1, int.MaxValue)] int? minCapacity = null,
        [FromQuery, Range(1, int.MaxValue)] int? equipmentId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default) =>
        await roomService.ListAsync(page, pageSize, officeId, minCapacity, equipmentId, isActive, cancellationToken);

    [HttpGet("available")]
    [ProducesResponseType(typeof(PagedResult<RoomInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<PagedResult<RoomInfo>> Available(
        [FromQuery, Required] DateTimeOffset? startsAt,
        [FromQuery, Required] DateTimeOffset? endsAt,
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int? officeId = null,
        [FromQuery, Range(1, int.MaxValue)] int? minCapacity = null,
        [FromQuery, Range(1, int.MaxValue)] int? equipmentId = null,
        CancellationToken cancellationToken = default) =>
        await roomService.SearchAvailableAsync(startsAt!.Value, endsAt!.Value, page, pageSize,
            officeId, minCapacity, equipmentId, cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RoomInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<RoomInfo> Get(int id, CancellationToken cancellationToken) =>
        await roomService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin,OfficeManager")]
    [ProducesResponseType(typeof(RoomInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateRoomRequest request, CancellationToken cancellationToken)
    {
        var room = await roomService.CreateAsync(GetActorId(), request.OfficeId, request.ToInput(), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = room.Id }, room);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,OfficeManager")]
    [ProducesResponseType(typeof(RoomInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<RoomInfo> Update(int id, RoomDetailsRequest request, CancellationToken cancellationToken) =>
        await roomService.UpdateAsync(GetActorId(), id, request.ToInput(), cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,OfficeManager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await roomService.DeleteAsync(GetActorId(), id, cancellationToken);
        return NoContent();
    }

    private int GetActorId() => int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
}
