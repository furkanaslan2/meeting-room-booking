namespace MeetingRoomBooking.Data.Entities;

public class Reservation
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int OrganizerUserId { get; set; }
    public User Organizer { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Active;
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public ICollection<ReservationParticipant> Participants { get; set; } = new List<ReservationParticipant>();
}
