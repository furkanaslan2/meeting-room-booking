using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IRoomService
{
    Task<PagedResult<RoomInfo>> ListAsync(int page, int pageSize, int? officeId, int? minCapacity,
        int? equipmentId, bool? isActive, CancellationToken cancellationToken);
    Task<RoomInfo> GetAsync(int id, CancellationToken cancellationToken);
    Task<RoomInfo> CreateAsync(int actorId, int officeId, RoomInput input, CancellationToken cancellationToken);
    Task<RoomInfo> UpdateAsync(int actorId, int id, RoomInput input, CancellationToken cancellationToken);
    Task DeleteAsync(int actorId, int id, CancellationToken cancellationToken);
    Task<PagedResult<EquipmentInfo>> ListEquipmentAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<EquipmentInfo> CreateEquipmentAsync(string name, CancellationToken cancellationToken);
}
