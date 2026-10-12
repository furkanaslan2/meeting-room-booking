namespace MeetingRoomBooking.Services.Models;

public sealed record BusySlotInfo(DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc);

public sealed record RoomCalendarInfo(int RoomId, DateOnly WeekStart, IReadOnlyList<BusySlotInfo> BusySlots);
