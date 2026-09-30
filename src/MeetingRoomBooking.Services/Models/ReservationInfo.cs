namespace MeetingRoomBooking.Services.Models;

public sealed record ParticipantInput(string Name, string? Email);

public sealed record ReservationInput(
    int RoomId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, string Title,
    IReadOnlyList<ParticipantInput> Participants);

public sealed record ParticipantInfo(int Id, string Name, string? Email);

public sealed record ReservationInfo(
    int Id, int RoomId, int OrganizerUserId, string Title, DateTimeOffset StartsAtUtc,
    DateTimeOffset EndsAtUtc, string Status, IReadOnlyList<ParticipantInfo> Participants);
