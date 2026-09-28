using MeetingRoomBooking.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MeetingRoomBooking.Data;

public class BookingDbContext(DbContextOptions<BookingDbContext> options) : DbContext(options)
{
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<RoomEquipment> RoomEquipment => Set<RoomEquipment>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationParticipant> ReservationParticipants => Set<ReservationParticipant>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Office>(entity =>
        {
            entity.ToTable("Offices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.City).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", table => table.HasCheckConstraint(
                "CK_Users_ManagerOffice", "`Role` <> 'OfficeManager' OR `OfficeId` IS NOT NULL"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(255).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(25).IsRequired();
            entity.HasOne(x => x.Office).WithMany(x => x.Users)
                .HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.ToTable("Rooms", table => table.HasCheckConstraint(
                "CK_Rooms_Capacity", "`Capacity` > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.OfficeId, x.Name }).IsUnique();
            entity.HasOne(x => x.Office).WithMany(x => x.Rooms)
                .HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.ToTable("Equipment");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<RoomEquipment>(entity =>
        {
            entity.ToTable("RoomEquipment");
            entity.HasKey(x => new { x.RoomId, x.EquipmentId });
            entity.HasOne(x => x.Room).WithMany(x => x.RoomEquipment)
                .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Equipment).WithMany(x => x.RoomEquipment)
                .HasForeignKey(x => x.EquipmentId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.ToTable("Reservations", table => table.HasCheckConstraint(
                "CK_Reservations_TimeOrder", "`EndUtc` > `StartUtc`"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.StartUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.EndUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.CreatedUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.UpdatedUtc).HasColumnType("datetime(6)");
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            entity.HasIndex(x => new { x.RoomId, x.Status, x.StartUtc, x.EndUtc });
            entity.HasOne(x => x.Room).WithMany(x => x.Reservations)
                .HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Organizer).WithMany(x => x.OrganizedReservations)
                .HasForeignKey(x => x.OrganizerUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReservationParticipant>(entity =>
        {
            entity.ToTable("ReservationParticipants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.HasOne(x => x.Reservation).WithMany(x => x.Participants)
                .HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RevokedToken>(entity =>
        {
            entity.ToTable("RevokedTokens");
            entity.HasKey(x => x.JwtId);
            entity.Property(x => x.JwtId).HasMaxLength(64);
            entity.Property(x => x.ExpiresUtc).HasColumnType("datetime(6)");
            entity.HasIndex(x => x.ExpiresUtc);
        });
    }
}
