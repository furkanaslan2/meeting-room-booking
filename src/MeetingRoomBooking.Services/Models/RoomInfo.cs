namespace MeetingRoomBooking.Services.Models;

public sealed record EquipmentInfo(int Id, string Name);

public sealed record RoomInfo(
    int Id, int OfficeId, string Name, int Capacity, int Floor, bool IsActive,
    IReadOnlyList<EquipmentInfo> Equipment);

public sealed record RoomInput(
    string Name, int Capacity, int Floor, bool IsActive, IReadOnlyList<int> EquipmentIds);
