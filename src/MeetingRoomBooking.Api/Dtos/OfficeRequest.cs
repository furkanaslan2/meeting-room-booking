using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBooking.Api.Dtos;

public sealed class OfficeRequest
{
    [Required, StringLength(120, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string City { get; init; } = string.Empty;
}
