using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IReservationService
{
    Task<ReservationInfo> CreateAsync(int organizerId, ReservationInput input, CancellationToken cancellationToken);
}
