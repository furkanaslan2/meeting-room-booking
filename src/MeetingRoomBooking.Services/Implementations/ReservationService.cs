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
    public async Task<ReservationInfo> CreateAsync(int organizerId, ReservationInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var startUtc = input.StartsAt.UtcDateTime;
        var endUtc = input.EndsAt.UtcDateTime;

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var room = await roomLocks.LockAsync(input.RoomId, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        await EnsureAvailableAsync(room, input, null, cancellationToken);

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

        return ToInfo(reservation);
    }

    public async Task<PagedResult<ReservationInfo>> ListAsync(int actorId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorId, cancellationToken);
        IQueryable<Reservation> query = database.Reservations.AsNoTracking();
        if (actor.Role == UserRole.OfficeManager)
        {
            query = query.Where(x => x.Room.OfficeId == actor.OfficeId);
        }
        else if (actor.Role != UserRole.Admin)
        {
            query = query.Where(x => x.OrganizerUserId == actorId);
        }

        return await PageAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ReservationInfo>> MineAsync(int actorId, int page, int pageSize, CancellationToken cancellationToken)
    {
        await GetActorAsync(actorId, cancellationToken);
        return await PageAsync(database.Reservations.AsNoTracking().Where(x => x.OrganizerUserId == actorId),
            page, pageSize, cancellationToken);
    }

    public async Task<ReservationInfo> GetAsync(int actorId, int id, CancellationToken cancellationToken)
    {
        var actor = await GetActorAsync(actorId, cancellationToken);
        var reservation = await database.Reservations.AsNoTracking()
            .Include(x => x.Room).Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "RESERVATION_NOT_FOUND", "Rezervasyon bulunamadı.");
        EnsureCanManage(actor, reservation);
        return ToInfo(reservation);
    }

    public async Task<ReservationInfo> UpdateAsync(int actorId, int id, ReservationInput input, CancellationToken cancellationToken)
    {
        Validate(input);
        var originalRoomId = await database.Reservations.AsNoTracking()
            .Where(x => x.Id == id).Select(x => (int?)x.RoomId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ServiceException(404, "RESERVATION_NOT_FOUND", "Rezervasyon bulunamadı.");

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        LockedRoom? targetRoom = null;
        foreach (var roomId in new[] { originalRoomId, input.RoomId }.Distinct().OrderBy(x => x))
        {
            var locked = await roomLocks.LockAsync(roomId, cancellationToken)
                ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
            if (roomId == input.RoomId) targetRoom = locked;
        }

        var reservation = await database.Reservations.Include(x => x.Room).Include(x => x.Participants)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "RESERVATION_NOT_FOUND", "Rezervasyon bulunamadı.");
        if (reservation.RoomId != originalRoomId)
        {
            throw new ServiceException(409, "RESERVATION_CHANGED", "Rezervasyon aynı anda değişti; yeniden deneyin.");
        }

        var actor = await GetActorAsync(actorId, cancellationToken);
        EnsureCanManage(actor, reservation);
        if (reservation.Status != ReservationStatus.Active)
        {
            throw new ServiceException(409, "RESERVATION_CANCELLED", "İptal edilmiş rezervasyon düzenlenemez.");
        }
        if (actor.Role == UserRole.OfficeManager && targetRoom!.OfficeId != actor.OfficeId)
        {
            throw new ServiceException(403, "FORBIDDEN", "Başka ofisin odasına taşıma yetkiniz yok.");
        }

        await EnsureAvailableAsync(targetRoom!, input, id, cancellationToken);
        reservation.RoomId = input.RoomId;
        reservation.Title = input.Title.Trim();
        reservation.StartUtc = input.StartsAt.UtcDateTime;
        reservation.EndUtc = input.EndsAt.UtcDateTime;
        reservation.UpdatedUtc = DateTime.UtcNow;
        database.ReservationParticipants.RemoveRange(reservation.Participants);
        reservation.Participants = input.Participants.Select(x => new ReservationParticipant
        {
            Name = x.Name.Trim(), Email = x.Email?.Trim()
        }).ToList();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToInfo(reservation);
    }

    public async Task CancelAsync(int actorId, int id, CancellationToken cancellationToken)
    {
        var originalRoomId = await database.Reservations.AsNoTracking()
            .Where(x => x.Id == id).Select(x => (int?)x.RoomId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ServiceException(404, "RESERVATION_NOT_FOUND", "Rezervasyon bulunamadı.");
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        _ = await roomLocks.LockAsync(originalRoomId, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        var reservation = await database.Reservations.Include(x => x.Room)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "RESERVATION_NOT_FOUND", "Rezervasyon bulunamadı.");
        if (reservation.RoomId != originalRoomId)
        {
            throw new ServiceException(409, "RESERVATION_CHANGED", "Rezervasyon aynı anda değişti; yeniden deneyin.");
        }

        var actor = await GetActorAsync(actorId, cancellationToken);
        EnsureCanManage(actor, reservation);
        if (reservation.Status == ReservationStatus.Active)
        {
            reservation.Status = ReservationStatus.Cancelled;
            reservation.UpdatedUtc = DateTime.UtcNow;
            await database.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<PagedResult<ReservationInfo>> PageAsync(IQueryable<Reservation> query,
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.StartUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.Participants).ToListAsync(cancellationToken);
        return new PagedResult<ReservationInfo>(items.Select(ToInfo).ToList(), page, pageSize, total);
    }

    private async Task<User> GetActorAsync(int actorId, CancellationToken cancellationToken) =>
        await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken)
        ?? throw new ServiceException(401, "UNAUTHORIZED", "Kullanıcı bulunamadı.");

    private static void EnsureCanManage(User actor, Reservation reservation)
    {
        if (actor.Role == UserRole.Admin ||
            actor.Role == UserRole.OfficeManager && actor.OfficeId == reservation.Room.OfficeId ||
            actor.Role == UserRole.Employee && reservation.OrganizerUserId == actor.Id)
        {
            return;
        }

        throw new ServiceException(403, "FORBIDDEN", "Bu rezervasyon için yetkiniz yok.");
    }

    private async Task EnsureAvailableAsync(LockedRoom room, ReservationInput input, int? excludingId,
        CancellationToken cancellationToken)
    {
        if (!room.IsActive)
        {
            throw new ServiceException(409, "ROOM_INACTIVE", "Pasif odaya rezervasyon yapılamaz.");
        }

        if (input.StartsAt.UtcDateTime <= DateTime.UtcNow)
        {
            throw new ServiceException(400, "PAST_RESERVATION", "Geçmiş zamana rezervasyon yapılamaz.");
        }

        if (input.Participants.Count + 1 > room.Capacity)
        {
            throw new ServiceException(400, "CAPACITY_EXCEEDED", "Katılımcılar ve düzenleyen kişi oda kapasitesini aşıyor.");
        }

        var startUtc = input.StartsAt.UtcDateTime;
        var endUtc = input.EndsAt.UtcDateTime;
        var overlapsQuery = database.Reservations.AsNoTracking().Where(x =>
            x.RoomId == room.Id && x.Status == ReservationStatus.Active &&
            x.StartUtc < endUtc && startUtc < x.EndUtc);
        if (excludingId.HasValue)
        {
            overlapsQuery = overlapsQuery.Where(x => x.Id != excludingId.Value);
        }

        var overlaps = await overlapsQuery.AnyAsync(cancellationToken);
        if (overlaps)
        {
            throw new ServiceException(409, "ROOM_CONFLICT", "Bu saatte oda dolu.");
        }
    }

    private static ReservationInfo ToInfo(Reservation reservation) => new(
        reservation.Id, reservation.RoomId, reservation.OrganizerUserId, reservation.Title,
        new DateTimeOffset(DateTime.SpecifyKind(reservation.StartUtc, DateTimeKind.Utc)),
        new DateTimeOffset(DateTime.SpecifyKind(reservation.EndUtc, DateTimeKind.Utc)),
        reservation.Status.ToString(), reservation.Participants.OrderBy(x => x.Id)
            .Select(x => new ParticipantInfo(x.Id, x.Name, x.Email)).ToList());

    private static void Validate(ReservationInput input)
    {
        if (input.RoomId < 1 || string.IsNullOrWhiteSpace(input.Title) || input.Title.Trim().Length > 200 ||
            input.Participants is null || input.Participants.Count > 1000 ||
            input.Participants.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 150 ||
                x.Email is { Length: > 255 }))
        {
            throw new ServiceException(400, "INVALID_RESERVATION", "Rezervasyon başlığı veya katılımcılar geçersiz.");
        }

        ReservationTimeRules.Validate(input.StartsAt, input.EndsAt);
    }
}
