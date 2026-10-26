using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IReservationReminderProcessor
{
    Task<IReadOnlyList<ReservationReminderInfo>> ClaimDueAsync(CancellationToken cancellationToken);
}
