namespace MeetingRoomBooking.Services.Models;

public sealed record ReservationAuditSnapshot(
    int RoomId, string Title, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    string Status, int ParticipantCount);

public sealed record ReservationAuditInfo(
    int Id, int ReservationId, int ActorUserId, string ActorFullName, string Action,
    DateTimeOffset OccurredAtUtc, ReservationAuditSnapshot? Before, ReservationAuditSnapshot After);
