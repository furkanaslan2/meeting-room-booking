namespace MeetingRoomBooking.Data.Entities;

public class Room
{
    public int Id { get; set; }
    public int OfficeId { get; set; }
    public Office Office { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int Floor { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<RoomEquipment> RoomEquipment { get; set; } = new List<RoomEquipment>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}
