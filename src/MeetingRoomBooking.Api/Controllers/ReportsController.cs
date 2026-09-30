using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,OfficeManager")]
[Route("api/reports")]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet("rooms")]
    [ProducesResponseType(typeof(RoomUsageReportInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<RoomUsageReportInfo> Rooms(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        [FromQuery, Range(1, int.MaxValue)] int? officeId = null,
        CancellationToken cancellationToken = default) =>
        await reportService.GetRoomUsageAsync(int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!),
            officeId, page, pageSize, cancellationToken);
}
