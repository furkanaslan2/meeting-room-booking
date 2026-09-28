namespace MeetingRoomBooking.Data.Entities;

public class RevokedToken
{
    public string JwtId { get; set; } = string.Empty;
    public DateTime ExpiresUtc { get; set; }
}
