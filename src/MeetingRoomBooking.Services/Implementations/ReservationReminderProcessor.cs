using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class ReservationReminderProcessor(BookingDbContext database) : IReservationReminderProcessor
{
    public async Task<IReadOnlyList<ReservationReminderInfo>> ClaimDueAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var candidates = await database.Reservations.AsNoTracking()
            .Where(x => x.Status == ReservationStatus.Active && x.ReminderSentUtc == null &&
                x.StartUtc > nowUtc && x.StartUtc <= nowUtc.AddMinutes(15))
            .OrderBy(x => x.StartUtc).ThenBy(x => x.Id).Take(100)
            .Select(x => new ReservationReminderInfo(x.Id, x.RoomId, x.StartUtc))
            .ToListAsync(cancellationToken);

        var claimed = new List<ReservationReminderInfo>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var claimUtc = DateTime.UtcNow;
            // The conditional UPDATE is the single-winner claim, even with multiple API instances.
            var affected = await database.Reservations
                .Where(x => x.Id == candidate.ReservationId && x.RoomId == candidate.RoomId &&
                    x.StartUtc == candidate.StartsAtUtc && x.Status == ReservationStatus.Active &&
                    x.ReminderSentUtc == null && x.StartUtc > claimUtc &&
                    x.StartUtc <= claimUtc.AddMinutes(15))
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.ReminderSentUtc, (DateTime?)claimUtc),
                    cancellationToken);
            if (affected == 1)
            {
                claimed.Add(candidate with { StartsAtUtc = DateTime.SpecifyKind(candidate.StartsAtUtc, DateTimeKind.Utc) });
            }
        }

        return claimed;
    }
}
