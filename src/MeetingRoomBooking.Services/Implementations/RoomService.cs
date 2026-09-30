using System.Data;
using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Data.Repositories;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class RoomService(BookingDbContext database, RoomLockRepository roomLocks) : IRoomService
{
    public async Task<PagedResult<RoomInfo>> ListAsync(int page, int pageSize, int? officeId, int? minCapacity,
        int? equipmentId, bool? isActive, CancellationToken cancellationToken)
    {
        IQueryable<Room> query = database.Rooms.AsNoTracking();
        if (officeId.HasValue) query = query.Where(x => x.OfficeId == officeId.Value);
        if (minCapacity.HasValue) query = query.Where(x => x.Capacity >= minCapacity.Value);
        if (equipmentId.HasValue) query = query.Where(x => x.RoomEquipment.Any(e => e.EquipmentId == equipmentId.Value));
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);
        var rooms = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.RoomEquipment).ThenInclude(x => x.Equipment)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new PagedResult<RoomInfo>(rooms.Select(ToInfo).ToList(), page, pageSize, total);
    }

    public async Task<PagedResult<RoomInfo>> SearchAvailableAsync(DateTimeOffset startsAt, DateTimeOffset endsAt,
        int page, int pageSize, int? officeId, int? minCapacity, int? equipmentId, CancellationToken cancellationToken)
    {
        var (startUtc, endUtc) = ReservationTimeRules.Validate(startsAt, endsAt);
        IQueryable<Room> query = database.Rooms.AsNoTracking().Where(room => room.IsActive &&
            !room.Reservations.Any(reservation => reservation.Status == ReservationStatus.Active &&
                reservation.StartUtc < endUtc && startUtc < reservation.EndUtc));
        if (officeId.HasValue) query = query.Where(room => room.OfficeId == officeId.Value);
        if (minCapacity.HasValue) query = query.Where(room => room.Capacity >= minCapacity.Value);
        if (equipmentId.HasValue)
        {
            query = query.Where(room => room.RoomEquipment.Any(item => item.EquipmentId == equipmentId.Value));
        }

        var total = await query.CountAsync(cancellationToken);
        var rooms = await query.OrderBy(room => room.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(room => room.RoomEquipment).ThenInclude(item => item.Equipment)
            .AsSplitQuery().ToListAsync(cancellationToken);
        return new PagedResult<RoomInfo>(rooms.Select(ToInfo).ToList(), page, pageSize, total);
    }

    public async Task<RoomInfo> GetAsync(int id, CancellationToken cancellationToken)
    {
        var room = await database.Rooms.AsNoTracking()
            .Include(x => x.RoomEquipment).ThenInclude(x => x.Equipment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        return ToInfo(room);
    }

    public async Task<RoomInfo> CreateAsync(int actorId, int officeId, RoomInput input, CancellationToken cancellationToken)
    {
        await EnsureCanManageAsync(actorId, officeId, cancellationToken);
        if (!await database.Offices.AnyAsync(x => x.Id == officeId, cancellationToken))
        {
            throw new ServiceException(404, "OFFICE_NOT_FOUND", "Ofis bulunamadı.");
        }

        var name = ValidateRoom(input);
        var equipmentIds = await ValidateEquipmentAsync(input.EquipmentIds, cancellationToken);
        if (await database.Rooms.AnyAsync(x => x.OfficeId == officeId && x.Name == name, cancellationToken))
        {
            throw new ServiceException(409, "ROOM_EXISTS", "Bu ofiste aynı isimli bir oda var.");
        }

        var room = new Room
        {
            OfficeId = officeId,
            Name = name,
            Capacity = input.Capacity,
            Floor = input.Floor,
            IsActive = input.IsActive,
            RoomEquipment = equipmentIds.Select(id => new RoomEquipment { EquipmentId = id }).ToList()
        };
        database.Rooms.Add(room);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Rooms.AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Name == name, cancellationToken))
            {
                throw new ServiceException(409, "ROOM_EXISTS", "Bu ofiste aynı isimli bir oda var.");
            }

            throw;
        }

        return await GetAsync(room.Id, cancellationToken);
    }

    public async Task<RoomInfo> UpdateAsync(int actorId, int id, RoomInput input, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (await roomLocks.LockAsync(id, cancellationToken) is null)
        {
            throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        }
        var room = await database.Rooms.Include(x => x.RoomEquipment)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        await EnsureCanManageAsync(actorId, room.OfficeId, cancellationToken);

        var name = ValidateRoom(input);
        var equipmentIds = await ValidateEquipmentAsync(input.EquipmentIds, cancellationToken);
        if (await database.Rooms.AnyAsync(x => x.OfficeId == room.OfficeId && x.Id != id && x.Name == name, cancellationToken))
        {
            throw new ServiceException(409, "ROOM_EXISTS", "Bu ofiste aynı isimli bir oda var.");
        }
        var now = DateTime.UtcNow;
        if (await database.Reservations.AnyAsync(x => x.RoomId == id && x.Status == ReservationStatus.Active &&
            x.EndUtc > now && x.Participants.Count + 1 > input.Capacity, cancellationToken))
        {
            throw new ServiceException(409, "CAPACITY_CONFLICT", "Gelecek rezervasyonların kişi sayısı yeni kapasiteyi aşıyor.");
        }

        room.Name = name;
        room.Capacity = input.Capacity;
        room.Floor = input.Floor;
        room.IsActive = input.IsActive;
        var selected = equipmentIds.ToHashSet();
        var existing = room.RoomEquipment.Select(x => x.EquipmentId).ToHashSet();
        database.RoomEquipment.RemoveRange(room.RoomEquipment.Where(x => !selected.Contains(x.EquipmentId)));
        foreach (var equipmentId in selected.Except(existing))
        {
            room.RoomEquipment.Add(new RoomEquipment { RoomId = id, EquipmentId = equipmentId });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Rooms.AsNoTracking().AnyAsync(x => x.OfficeId == room.OfficeId && x.Id != id && x.Name == name, cancellationToken))
            {
                throw new ServiceException(409, "ROOM_EXISTS", "Bu ofiste aynı isimli bir oda var.");
            }

            throw;
        }

        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int actorId, int id, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (await roomLocks.LockAsync(id, cancellationToken) is null)
        {
            throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        }
        var room = await database.Rooms.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "ROOM_NOT_FOUND", "Oda bulunamadı.");
        await EnsureCanManageAsync(actorId, room.OfficeId, cancellationToken);
        if (await database.Reservations.AnyAsync(x => x.RoomId == id, cancellationToken))
        {
            throw new ServiceException(409, "ROOM_IN_USE", "Rezervasyonu olan oda silinemez; pasif hale getirin.");
        }

        database.Rooms.Remove(room);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ServiceException(409, "ROOM_IN_USE", "Odaya bağlı kayıtlar var; pasif hale getirin.");
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<PagedResult<EquipmentInfo>> ListEquipmentAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = database.Equipment.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EquipmentInfo(x.Id, x.Name)).ToListAsync(cancellationToken);
        return new PagedResult<EquipmentInfo>(items, page, pageSize, total);
    }

    public async Task<EquipmentInfo> CreateEquipmentAsync(string name, CancellationToken cancellationToken)
    {
        var equipmentName = name?.Trim() ?? string.Empty;
        if (equipmentName.Length is < 2 or > 100)
        {
            throw new ServiceException(400, "INVALID_EQUIPMENT", "Ekipman adı 2-100 karakter olmalıdır.");
        }

        if (await database.Equipment.AnyAsync(x => x.Name == equipmentName, cancellationToken))
        {
            throw new ServiceException(409, "EQUIPMENT_EXISTS", "Bu ekipman zaten var.");
        }

        var equipment = new Equipment { Name = equipmentName };
        database.Equipment.Add(equipment);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Equipment.AsNoTracking().AnyAsync(x => x.Name == equipmentName, cancellationToken))
            {
                throw new ServiceException(409, "EQUIPMENT_EXISTS", "Bu ekipman zaten var.");
            }

            throw;
        }

        return new EquipmentInfo(equipment.Id, equipment.Name);
    }

    private async Task EnsureCanManageAsync(int actorId, int officeId, CancellationToken cancellationToken)
    {
        var actor = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, cancellationToken);
        if (actor?.Role == UserRole.Admin ||
            actor?.Role == UserRole.OfficeManager && actor.OfficeId == officeId)
        {
            return;
        }

        throw new ServiceException(403, "FORBIDDEN", "Yalnızca kendi ofisinizdeki odaları yönetebilirsiniz.");
    }

    private async Task<int[]> ValidateEquipmentAsync(IReadOnlyList<int> equipmentIds, CancellationToken cancellationToken)
    {
        if (equipmentIds is null || equipmentIds.Count > 100 || equipmentIds.Any(id => id < 1) ||
            equipmentIds.Count != equipmentIds.Distinct().Count())
        {
            throw new ServiceException(400, "INVALID_EQUIPMENT", "Ekipman ID listesi geçersiz veya tekrar içeriyor.");
        }

        var ids = equipmentIds.ToArray();
        if (ids.Length > 0 && await database.Equipment.CountAsync(x => ids.Contains(x.Id), cancellationToken) != ids.Length)
        {
            throw new ServiceException(400, "INVALID_EQUIPMENT", "Seçilen ekipmanlardan biri bulunamadı.");
        }

        return ids;
    }

    private static string ValidateRoom(RoomInput input)
    {
        var name = input.Name?.Trim() ?? string.Empty;
        if (name.Length is < 2 or > 120 || input.Capacity < 1)
        {
            throw new ServiceException(400, "INVALID_ROOM", "Oda adı 2-120 karakter ve kapasitesi en az 1 olmalıdır.");
        }

        return name;
    }

    private static RoomInfo ToInfo(Room room) => new(
        room.Id, room.OfficeId, room.Name, room.Capacity, room.Floor, room.IsActive,
        room.RoomEquipment.OrderBy(x => x.EquipmentId)
            .Select(x => new EquipmentInfo(x.EquipmentId, x.Equipment.Name)).ToList());
}
