using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class OfficeService(BookingDbContext database) : IOfficeService
{
    public async Task<PagedResult<OfficeInfo>> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = database.Offices.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var offices = await query.OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new OfficeInfo(x.Id, x.Name, x.City))
            .ToListAsync(cancellationToken);
        return new PagedResult<OfficeInfo>(offices, page, pageSize, total);
    }

    public async Task<OfficeInfo> GetAsync(int id, CancellationToken cancellationToken)
    {
        return await database.Offices.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new OfficeInfo(x.Id, x.Name, x.City))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ServiceException(404, "OFFICE_NOT_FOUND", "Ofis bulunamadı.");
    }

    public async Task<OfficeInfo> CreateAsync(string name, string city, CancellationToken cancellationToken)
    {
        Validate(name, city);
        var officeName = name.Trim();
        if (await database.Offices.AnyAsync(x => x.Name == officeName, cancellationToken))
        {
            throw new ServiceException(409, "OFFICE_EXISTS", "Bu isimde bir ofis zaten var.");
        }

        var office = new Office { Name = officeName, City = city.Trim() };
        database.Offices.Add(office);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Offices.AsNoTracking().AnyAsync(x => x.Name == officeName, cancellationToken))
            {
                throw new ServiceException(409, "OFFICE_EXISTS", "Bu isimde bir ofis zaten var.");
            }

            throw;
        }

        return ToInfo(office);
    }

    public async Task<OfficeInfo> UpdateAsync(int id, string name, string city, CancellationToken cancellationToken)
    {
        Validate(name, city);
        var office = await database.Offices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "OFFICE_NOT_FOUND", "Ofis bulunamadı.");
        var officeName = name.Trim();
        if (await database.Offices.AnyAsync(x => x.Id != id && x.Name == officeName, cancellationToken))
        {
            throw new ServiceException(409, "OFFICE_EXISTS", "Bu isimde bir ofis zaten var.");
        }

        office.Name = officeName;
        office.City = city.Trim();
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Offices.AsNoTracking().AnyAsync(x => x.Id != id && x.Name == officeName, cancellationToken))
            {
                throw new ServiceException(409, "OFFICE_EXISTS", "Bu isimde bir ofis zaten var.");
            }

            throw;
        }

        return ToInfo(office);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var office = await database.Offices.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceException(404, "OFFICE_NOT_FOUND", "Ofis bulunamadı.");
        if (await database.Rooms.AnyAsync(x => x.OfficeId == id, cancellationToken) ||
            await database.Users.AnyAsync(x => x.OfficeId == id, cancellationToken))
        {
            throw new ServiceException(409, "OFFICE_IN_USE", "Bu ofise bağlı oda veya kullanıcı var; silinemez.");
        }

        database.Offices.Remove(office);
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ServiceException(409, "OFFICE_IN_USE", "Bu ofise bağlı kayıtlar var; silinemez.");
        }
    }

    private static OfficeInfo ToInfo(Office office) => new(office.Id, office.Name, office.City);

    private static void Validate(string name, string city)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(city) ||
            name.Trim().Length is < 2 or > 120 || city.Trim().Length is < 2 or > 120)
        {
            throw new ServiceException(400, "INVALID_OFFICE", "Ofis adı ve şehir 2-120 karakter olmalıdır.");
        }
    }
}
