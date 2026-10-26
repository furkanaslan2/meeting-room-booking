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

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var room = await roomLocks.LockAsync(input.RoomId, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        await EnsureAvailableAsync(room, input, null, cancellationToken);

        var reservation = NewReservation(organizerId, input, DateTime.UtcNow);
        database.Reservations.Add(reservation);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToInfo(reservation);
    }

    public async Task<IReadOnlyList<ReservationInfo>> CreateRecurringAsync(int organizerId, ReservationInput input,
        int weekCount, CancellationToken cancellationToken)
    {
        Validate(input);
        if (weekCount is < 2 or > 52)
        {
            throw new ServiceException(400, "INVALID_RECURRENCE_COUNT", "Tekrar sayısı 2 ile 52 hafta arasında olmalıdır.");
        }

        var occurrences = new List<ReservationInput>(weekCount);
        for (var index = 0; index < weekCount; index++)
        {
            DateTimeOffset startsAt;
            DateTimeOffset endsAt;
            try
            {
                startsAt = input.StartsAt.AddDays(index * 7);
                endsAt = input.EndsAt.AddDays(index * 7);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new ServiceException(400, "INVALID_RECURRENCE", "Tekrar tarihleri geçerli aralık dışında.");
            }

            var occurrence = input with { StartsAt = startsAt, EndsAt = endsAt };
            ReservationTimeRules.Validate(occurrence.StartsAt, occurrence.EndsAt);
            occurrences.Add(occurrence);
        }

        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var room = await roomLocks.LockAsync(input.RoomId, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        for (var index = 0; index < occurrences.Count; index++)
        {
            try
            {
                await EnsureAvailableAsync(room, occurrences[index], null, cancellationToken);
            }
            catch (ServiceException exception) when (exception.Code == "ROOM_CONFLICT")
            {
                var localStart = TimeZoneInfo.ConvertTime(occurrences[index].StartsAt, TurkeyTimeZone);
                throw new ServiceException(409, "ROOM_CONFLICT",
                    $"{index + 1}. tekrar ({localStart:dd.MM.yyyy HH:mm}, Türkiye saati) bu odada dolu. Hiçbir tekrar kaydedilmedi.");
            }
        }

        var now = DateTime.UtcNow;
        var reservations = occurrences.Select(occurrence => NewReservation(organizerId, occurrence, now)).ToList();
        database.Reservations.AddRange(reservations);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return reservations.Select(ToInfo).ToList();
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

    private static Reservation NewReservation(int organizerId, ReservationInput input, DateTime now) => new()
    {
        RoomId = input.RoomId,
        OrganizerUserId = organizerId,
        Title = input.Title.Trim(),
        StartUtc = input.StartsAt.UtcDateTime,
        EndUtc = input.EndsAt.UtcDateTime,
        Status = ReservationStatus.Active,
        CreatedUtc = now,
        UpdatedUtc = now,
        Participants = input.Participants.Select(participant => new ReservationParticipant
        {
            Name = participant.Name.Trim(),
            Email = participant.Email?.Trim()
        }).ToList()
    };

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
