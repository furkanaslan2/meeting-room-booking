using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Api.Dtos;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipment")]
public sealed class EquipmentController(IRoomService roomService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EquipmentInfo>), StatusCodes.Status200OK)]
    public async Task<PagedResult<EquipmentInfo>> List(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        await roomService.ListEquipmentAsync(page, pageSize, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(EquipmentInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(EquipmentRequest request, CancellationToken cancellationToken)
    {
        var equipment = await roomService.CreateEquipmentAsync(request.Name, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, equipment);
    }
}
