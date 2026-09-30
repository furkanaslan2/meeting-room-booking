using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Implementations;

internal static class ReservationTimeRules
{
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public static (DateTime StartUtc, DateTime EndUtc) Validate(DateTimeOffset startsAt, DateTimeOffset endsAt)
    {
        var startUtc = startsAt.UtcDateTime;
        var endUtc = endsAt.UtcDateTime;
        var duration = endUtc - startUtc;
        if (startUtc <= DateTime.UtcNow)
        {
            throw new ServiceException(400, "PAST_RESERVATION", "Geçmiş zamana rezervasyon yapılamaz.");
        }

        if (duration < TimeSpan.FromMinutes(15) || duration > TimeSpan.FromHours(4))
        {
            throw new ServiceException(400, "INVALID_DURATION", "Rezervasyon süresi 15 dakika ile 4 saat arasında olmalıdır.");
        }

        var localStart = TimeZoneInfo.ConvertTime(startsAt, TurkeyTimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(endsAt, TurkeyTimeZone);
        if (localStart.Date != localEnd.Date || localStart.TimeOfDay < TimeSpan.FromHours(8) ||
            localEnd.TimeOfDay > TimeSpan.FromHours(20))
        {
            throw new ServiceException(400, "OUTSIDE_WORK_HOURS", "Rezervasyon Türkiye saatiyle 08:00-20:00 arasında olmalıdır.");
        }

        return (startUtc, endUtc);
    }
}
