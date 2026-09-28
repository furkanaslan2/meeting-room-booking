namespace MeetingRoomBooking.Data.Entities;

public class RoomEquipment
{
    public int RoomId { get; set; }
    public Room Room { get; set; } = null!;
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
}
