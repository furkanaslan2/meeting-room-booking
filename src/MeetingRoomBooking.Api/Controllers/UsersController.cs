using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Api.Dtos;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MeetingRoomBooking.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public sealed class UsersController(IAuthService authService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<PagedResult<UserInfo>> List(
        [FromQuery, Range(1, 1000000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 10,
        CancellationToken cancellationToken = default) =>
        await authService.ListUsersAsync(page, pageSize, cancellationToken);

    [HttpPut("{id:int}/role")]
    [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<UserInfo> ChangeRole(int id, ChangeRoleRequest request, CancellationToken cancellationToken) =>
        await authService.ChangeRoleAsync(id, request.Role, request.OfficeId, cancellationToken);
}
