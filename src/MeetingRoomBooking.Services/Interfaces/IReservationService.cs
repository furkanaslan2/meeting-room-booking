using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IReservationService
{
    Task<ReservationInfo> CreateAsync(int organizerId, ReservationInput input, CancellationToken cancellationToken);
    Task<PagedResult<ReservationInfo>> ListAsync(int actorId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<ReservationInfo>> MineAsync(int actorId, int page, int pageSize, CancellationToken cancellationToken);
    Task<ReservationInfo> GetAsync(int actorId, int id, CancellationToken cancellationToken);
    Task<ReservationInfo> UpdateAsync(int actorId, int id, ReservationInput input, CancellationToken cancellationToken);
    Task CancelAsync(int actorId, int id, CancellationToken cancellationToken);
}
