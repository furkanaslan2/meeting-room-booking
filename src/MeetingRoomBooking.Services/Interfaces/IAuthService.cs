using MeetingRoomBooking.Services.Models;

namespace MeetingRoomBooking.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(string fullName, string email, string password, int? officeId, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task LogoutAsync(string jwtId, CancellationToken cancellationToken);
    Task<UserInfo> GetUserAsync(int userId, CancellationToken cancellationToken);
    Task<UserInfo> ChangeRoleAsync(int userId, string role, int? officeId, CancellationToken cancellationToken);
}
