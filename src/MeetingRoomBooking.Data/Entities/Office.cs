namespace MeetingRoomBooking.Data.Entities;

public class Office
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public ICollection<Room> Rooms { get; set; } = new List<Room>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
