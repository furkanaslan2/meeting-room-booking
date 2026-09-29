using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Api.Dtos;

public class RoomDetailsRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Capacity { get; init; }

    public int Floor { get; init; }

    public bool IsActive { get; init; } = true;

    [Required]
    public int[] EquipmentIds { get; init; } = [];

    public RoomInput ToInput() => new(Name, Capacity, Floor, IsActive, EquipmentIds);
}

public sealed class CreateRoomRequest : RoomDetailsRequest
{
    [Range(1, int.MaxValue)]
    public int OfficeId { get; init; }
}

public sealed class EquipmentRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;
}
