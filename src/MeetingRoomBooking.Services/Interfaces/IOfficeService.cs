using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IOfficeService
{
    Task<PagedResult<OfficeInfo>> ListAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<OfficeInfo> GetAsync(int id, CancellationToken cancellationToken);
    Task<OfficeInfo> CreateAsync(string name, string city, CancellationToken cancellationToken);
    Task<OfficeInfo> UpdateAsync(int id, string name, string city, CancellationToken cancellationToken);
    Task DeleteAsync(int id, CancellationToken cancellationToken);
}
