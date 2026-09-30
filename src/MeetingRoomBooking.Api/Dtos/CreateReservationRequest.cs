using System.ComponentModel.DataAnnotations;
using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Api.Dtos;

public sealed class ReservationParticipantRequest
{
    [Required, MaxLength(150)]
    public string Name { get; init; } = string.Empty;

    [EmailAddress, MaxLength(255)]
    public string? Email { get; init; }
}

public sealed class CreateReservationRequest
{
    [Range(1, int.MaxValue)]
    public int RoomId { get; init; }

    public DateTimeOffset StartsAt { get; init; }

    public DateTimeOffset EndsAt { get; init; }

    [Required, MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [Required]
    public List<ReservationParticipantRequest> Participants { get; init; } = [];

    public ReservationInput ToInput() => new(RoomId, StartsAt, EndsAt, Title,
        Participants?.Select(x => new ParticipantInput(x?.Name ?? string.Empty, x?.Email)).ToList() ?? []);
}
