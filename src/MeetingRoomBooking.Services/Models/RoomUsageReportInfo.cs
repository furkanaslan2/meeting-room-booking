namespace MeetingRoomBooking.Services.Models;

public sealed record RoomUsageInfo(
    int RoomId, int OfficeId, string RoomName, int ReservationCount,
    double OccupiedMinutes, double OccupancyPercent);

public sealed record RoomUsageReportInfo(
    DateTimeOffset PeriodStartUtc, DateTimeOffset PeriodEndUtc, double AvailableWorkMinutes,
    IReadOnlyList<RoomUsageInfo> Items, int Page, int PageSize, int TotalCount);
