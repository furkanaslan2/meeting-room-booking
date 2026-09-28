namespace MeetingRoomBooking.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Employee;
    public int? OfficeId { get; set; }
    public Office? Office { get; set; }
    public ICollection<Reservation> OrganizedReservations { get; set; } = new List<Reservation>();
}
