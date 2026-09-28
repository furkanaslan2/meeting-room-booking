namespace MeetingRoomBooking.Services.Models;

public sealed record AuthResult(string Token, DateTime ExpiresUtc, UserInfo User);

public sealed record UserInfo(int Id, string FullName, string Email, string Role, int? OfficeId);
