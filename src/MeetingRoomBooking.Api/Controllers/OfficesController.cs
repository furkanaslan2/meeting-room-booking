using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Api.Dtos;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/offices")]
public sealed class OfficesController(IOfficeService officeService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OfficeInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<PagedResult<OfficeInfo>> List(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        await officeService.ListAsync(page, pageSize, cancellationToken);

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OfficeInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<OfficeInfo> Get(int id, CancellationToken cancellationToken) =>
        await officeService.GetAsync(id, cancellationToken);

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(OfficeInfo), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(OfficeRequest request, CancellationToken cancellationToken)
    {
        var office = await officeService.CreateAsync(request.Name, request.City, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = office.Id }, office);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(OfficeInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<OfficeInfo> Update(int id, OfficeRequest request, CancellationToken cancellationToken) =>
        await officeService.UpdateAsync(id, request.Name, request.City, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await officeService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
