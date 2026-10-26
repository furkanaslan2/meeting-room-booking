namespace MeetingRoomBooking.Data.Entities;

public class ReservationAuditEntry
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public int ActorUserId { get; set; }
    public User Actor { get; set; } = null!;
    public ReservationAuditAction Action { get; set; }
    public DateTime OccurredUtc { get; set; }
    public string? BeforeJson { get; set; }
    public string AfterJson { get; set; } = string.Empty;
}
