namespace MeetingRoomBooking.Data.Entities;

public class Equipment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<RoomEquipment> RoomEquipment { get; set; } = new List<RoomEquipment>();
}
