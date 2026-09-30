using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class ReportService(BookingDbContext database) : IReportService
{
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task<RoomUsageReportInfo> GetRoomUsageAsync(int actorId, int? officeId,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken)
            ?? throw new ServiceException(401, "UNAUTHORIZED", "Kullanıcı bulunamadı.");
        var scopedOfficeId = actor.Role switch
        {
            UserRole.Admin => officeId,
            UserRole.OfficeManager when actor.OfficeId.HasValue &&
                (!officeId.HasValue || officeId == actor.OfficeId) => actor.OfficeId,
            _ => throw new ServiceException(403, "FORBIDDEN", "Yalnızca kendi ofisinizin raporunu görebilirsiniz.")
        };

        var nowUtc = DateTime.UtcNow;
        var todayInTurkey = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(nowUtc, TurkeyTimeZone));
        var firstDay = todayInTurkey.AddDays(-29);
        var periodStartUtc = TimeZoneInfo.ConvertTimeToUtc(firstDay.ToDateTime(TimeOnly.MinValue), TurkeyTimeZone);
        var availableWorkMinutes = 0.0;
        for (var day = 0; day < 30; day++)
        {
            var date = firstDay.AddDays(day);
            var workStart = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(8, 0)), TurkeyTimeZone);
            var workEnd = TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(20, 0)), TurkeyTimeZone);
            if (workStart < nowUtc)
            {
                availableWorkMinutes += (Min(workEnd, nowUtc) - workStart).TotalMinutes;
            }
        }

        var roomsQuery = database.Rooms.AsNoTracking().AsQueryable();
        var reservationsQuery = database.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Active && x.StartUtc < nowUtc && x.EndUtc > periodStartUtc);
        if (scopedOfficeId.HasValue)
        {
            roomsQuery = roomsQuery.Where(x => x.OfficeId == scopedOfficeId.Value);
            reservationsQuery = reservationsQuery.Where(x => x.Room.OfficeId == scopedOfficeId.Value);
        }

        var rooms = await roomsQuery.Select(x => new { x.Id, x.OfficeId, x.Name }).ToListAsync(cancellationToken);
        var reservations = await reservationsQuery.Select(x => new { x.RoomId, x.StartUtc, x.EndUtc })
            .ToListAsync(cancellationToken);
        var metrics = reservations.GroupBy(x => x.RoomId).ToDictionary(group => group.Key,
            group => (Count: group.Count(), Minutes: group.Sum(x =>
                (Min(x.EndUtc, nowUtc) - Max(x.StartUtc, periodStartUtc)).TotalMinutes)));

        var usage = rooms.Select(room =>
        {
            var (count, minutes) = metrics.TryGetValue(room.Id, out var metric) ? metric : (0, 0.0);
            var percent = availableWorkMinutes > 0 ? minutes / availableWorkMinutes * 100 : 0;
            return new RoomUsageInfo(room.Id, room.OfficeId, room.Name, count,
                Math.Round(minutes, 2), Math.Round(percent, 2));
        }).OrderByDescending(x => x.OccupiedMinutes).ThenByDescending(x => x.ReservationCount)
            .ThenBy(x => x.RoomId).ToList();

        return new RoomUsageReportInfo(new DateTimeOffset(periodStartUtc), new DateTimeOffset(nowUtc),
            Math.Round(availableWorkMinutes, 2),
            usage.Skip((page - 1) * pageSize).Take(pageSize).ToList(), page, pageSize, usage.Count);
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;
}
