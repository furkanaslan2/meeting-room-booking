using MeetingRoomBooking.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Data;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(BookingDbContext database, string password, CancellationToken cancellationToken)
    {
        if (await database.Offices.AnyAsync(cancellationToken) ||
            await database.Rooms.AnyAsync(cancellationToken) ||
            await database.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        var istanbul = new Office { Name = "Istanbul Office", City = "İstanbul" };
        var ankara = new Office { Name = "Ankara Office", City = "Ankara" };
        var projector = new Equipment { Name = "Projector" };
        var television = new Equipment { Name = "TV" };
        var whiteboard = new Equipment { Name = "Whiteboard" };

        var atlas = new Room { Office = istanbul, Name = "Atlas", Capacity = 4, Floor = 1, IsActive = true };
        var marmara = new Room { Office = istanbul, Name = "Marmara", Capacity = 6, Floor = 1, IsActive = true };
        var bogaz = new Room { Office = istanbul, Name = "Boğaz", Capacity = 8, Floor = 2, IsActive = true };
        var galata = new Room { Office = istanbul, Name = "Galata", Capacity = 12, Floor = 3, IsActive = true };
        var kizilay = new Room { Office = ankara, Name = "Kızılay", Capacity = 4, Floor = 1, IsActive = true };
        var anadolu = new Room { Office = ankara, Name = "Anadolu", Capacity = 6, Floor = 2, IsActive = true };
        var baskent = new Room { Office = ankara, Name = "Başkent", Capacity = 8, Floor = 2, IsActive = true };
        var kale = new Room { Office = ankara, Name = "Kale", Capacity = 12, Floor = 3, IsActive = true };

        var admin = new User { FullName = "Demo Admin", Email = "admin@meeting.test", Role = UserRole.Admin };
        var manager = new User
        {
            FullName = "Demo Office Manager",
            Email = "manager@meeting.test",
            Role = UserRole.OfficeManager,
            Office = istanbul
        };
        var employee = new User
        {
            FullName = "Demo Employee",
            Email = "employee@meeting.test",
            Role = UserRole.Employee,
            Office = istanbul
        };

        var hasher = new PasswordHasher<User>();
        admin.PasswordHash = hasher.HashPassword(admin, password);
        manager.PasswordHash = hasher.HashPassword(manager, password);
        employee.PasswordHash = hasher.HashPassword(employee, password);

        database.Offices.AddRange(istanbul, ankara);
        database.Equipment.AddRange(projector, television, whiteboard);
        database.Rooms.AddRange(atlas, marmara, bogaz, galata, kizilay, anadolu, baskent, kale);
        database.Users.AddRange(admin, manager, employee);
        await database.SaveChangesAsync(cancellationToken);

        database.RoomEquipment.AddRange(
            new RoomEquipment { RoomId = atlas.Id, EquipmentId = projector.Id },
            new RoomEquipment { RoomId = atlas.Id, EquipmentId = whiteboard.Id },
            new RoomEquipment { RoomId = marmara.Id, EquipmentId = television.Id },
            new RoomEquipment { RoomId = bogaz.Id, EquipmentId = projector.Id },
            new RoomEquipment { RoomId = bogaz.Id, EquipmentId = television.Id },
            new RoomEquipment { RoomId = galata.Id, EquipmentId = projector.Id },
            new RoomEquipment { RoomId = galata.Id, EquipmentId = television.Id },
            new RoomEquipment { RoomId = galata.Id, EquipmentId = whiteboard.Id },
            new RoomEquipment { RoomId = kizilay.Id, EquipmentId = television.Id },
            new RoomEquipment { RoomId = anadolu.Id, EquipmentId = whiteboard.Id },
            new RoomEquipment { RoomId = baskent.Id, EquipmentId = projector.Id },
            new RoomEquipment { RoomId = kale.Id, EquipmentId = television.Id },
            new RoomEquipment { RoomId = kale.Id, EquipmentId = whiteboard.Id });

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone));
        var tomorrow = localToday.AddDays(1);
        var nextDay = localToday.AddDays(2);
        var now = DateTime.UtcNow;

        database.Reservations.AddRange(
            new Reservation
            {
                RoomId = atlas.Id,
                OrganizerUserId = employee.Id,
                Title = "Project planning",
                StartUtc = ToUtc(tomorrow, 10, timeZone),
                EndUtc = ToUtc(tomorrow, 11, timeZone),
                CreatedUtc = now,
                UpdatedUtc = now,
                Participants = new List<ReservationParticipant>
                {
                    new() { Name = "Demo Office Manager", Email = manager.Email }
                }
            },
            new Reservation
            {
                RoomId = baskent.Id,
                OrganizerUserId = admin.Id,
                Title = "Quarterly review",
                StartUtc = ToUtc(nextDay, 14, timeZone),
                EndUtc = ToUtc(nextDay, 15, timeZone),
                CreatedUtc = now,
                UpdatedUtc = now,
                Participants = new List<ReservationParticipant>
                {
                    new() { Name = "Demo Employee", Email = employee.Email }
                }
            });

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static DateTime ToUtc(DateOnly date, int hour, TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(new TimeOnly(hour, 0)), timeZone);
    }
}
