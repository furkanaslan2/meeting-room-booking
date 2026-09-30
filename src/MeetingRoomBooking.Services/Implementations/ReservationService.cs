using System.Data;
using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Data.Repositories;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class ReservationService(BookingDbContext database, RoomLockRepository roomLocks) : IReservationService
{
    private static readonly TimeZoneInfo TurkeyTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task<ReservationInfo> CreateAsync(int organizerId, ReservationInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var startUtc = input.StartsAt.UtcDateTime;
        var endUtc = input.EndsAt.UtcDateTime;

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var room = await roomLocks.LockAsync(input.RoomId, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        if (!room.IsActive)
        {
            throw new ServiceException(409, "ROOM_INACTIVE", "Pasif odaya rezervasyon yapılamaz.");
        }

        if (startUtc <= DateTime.UtcNow)
        {
            throw new ServiceException(400, "PAST_RESERVATION", "Geçmiş zamana rezervasyon yapılamaz.");
        }

        if (input.Participants.Count + 1 > room.Capacity)
        {
            throw new ServiceException(400, "CAPACITY_EXCEEDED", "Katılımcılar ve düzenleyen kişi oda kapasitesini aşıyor.");
        }

        var overlaps = await database.Reservations.AsNoTracking().AnyAsync(x =>
            x.RoomId == input.RoomId && x.Status == ReservationStatus.Active &&
            x.StartUtc < endUtc && startUtc < x.EndUtc, cancellationToken);
        if (overlaps)
        {
            throw new ServiceException(409, "ROOM_CONFLICT", "Bu saatte oda dolu.");
        }

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            RoomId = room.Id,
            OrganizerUserId = organizerId,
            Title = input.Title.Trim(),
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = ReservationStatus.Active,
            CreatedUtc = now,
            UpdatedUtc = now,
            Participants = input.Participants.Select(x => new ReservationParticipant
            {
                Name = x.Name.Trim(),
                Email = x.Email?.Trim()
            }).ToList()
        };
        database.Reservations.Add(reservation);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReservationInfo(reservation.Id, room.Id, organizerId, reservation.Title,
            new DateTimeOffset(startUtc), new DateTimeOffset(endUtc), reservation.Status.ToString(),
            reservation.Participants.Select(x => new ParticipantInfo(x.Id, x.Name, x.Email)).ToList());
    }

    private static void Validate(ReservationInput input)
    {
        if (input.RoomId < 1 || string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 ||
            input.Participants is null || input.Participants.Count > 1000 ||
            input.Participants.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 150 ||
                x.Email is { Length: > 255 }))
        {
            throw new ServiceException(400, "INVALID_RESERVATION", "Rezervasyon başlığı veya katılımcılar geçersiz.");
        }

        var startUtc = input.StartsAt.UtcDateTime;
        var endUtc = input.EndsAt.UtcDateTime;
        var duration = endUtc - startUtc;
        if (startUtc <= DateTime.UtcNow)
        {
            throw new ServiceException(400, "PAST_RESERVATION", "Geçmiş zamana rezervasyon yapılamaz.");
        }

        if (duration < TimeSpan.FromMinutes(15) || duration > TimeSpan.FromHours(4))
        {
            throw new ServiceException(400, "INVALID_DURATION", "Rezervasyon süresi 15 dakika ile 4 saat arasında olmalıdır.");
        }

        var localStart = TimeZoneInfo.ConvertTime(input.StartsAt, TurkeyTimeZone);
        var localEnd = TimeZoneInfo.ConvertTime(input.EndsAt, TurkeyTimeZone);
        if (localStart.Date != localEnd.Date || localStart.TimeOfDay < TimeSpan.FromHours(8) ||
            localEnd.TimeOfDay > TimeSpan.FromHours(20))
        {
            throw new ServiceException(400, "OUTSIDE_WORK_HOURS", "Rezervasyon Türkiye saatiyle 08:00-20:00 arasında olmalıdır.");
        }
    }
}
