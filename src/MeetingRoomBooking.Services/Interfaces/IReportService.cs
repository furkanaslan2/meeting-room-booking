using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IReportService
{
    Task<RoomUsageReportInfo> GetRoomUsageAsync(int actorId, int? officeId,
        int page, int pageSize, CancellationToken cancellationToken);
}
