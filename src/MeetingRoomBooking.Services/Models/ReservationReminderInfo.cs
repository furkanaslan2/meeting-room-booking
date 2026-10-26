namespace MeetingRoomBooking.Services.Models;

public sealed record ReservationReminderInfo(int ReservationId, int RoomId, DateTime StartsAtUtc);
