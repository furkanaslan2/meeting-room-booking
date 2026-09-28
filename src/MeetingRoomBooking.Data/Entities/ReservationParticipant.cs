namespace MeetingRoomBooking.Data.Entities;

public class ReservationParticipant
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public Reservation Reservation { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
}
