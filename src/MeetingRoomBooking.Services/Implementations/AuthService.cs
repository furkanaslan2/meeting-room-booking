using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MeetingRoomBooking.Data;
using MeetingRoomBooking.Data.Entities;
using MeetingRoomBooking.Services.Interfaces;
using MeetingRoomBooking.Services.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace MeetingRoomBooking.Services.Implementations;

public sealed class AuthService(BookingDbContext database, IPasswordHasher<User> passwordHasher, JwtSettings jwtSettings) : IAuthService
{
    public async Task<AuthResult> RegisterAsync(string fullName, string email, string password, int? officeId, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (await database.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken))
        {
            throw new ServiceException(409, "EMAIL_EXISTS", "Bu e-posta adresi zaten kayıtlı.");
        }

        if (officeId.HasValue && !await database.Offices.AnyAsync(office => office.Id == officeId.Value, cancellationToken))
        {
            throw new ServiceException(400, "INVALID_OFFICE", "Seçilen ofis bulunamadı.");
        }

        var user = new User
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            OfficeId = officeId,
            Role = UserRole.Employee
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        database.Users.Add(user);

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (await database.Users.AsNoTracking().AnyAsync(x => x.Email == normalizedEmail, cancellationToken))
            {
                throw new ServiceException(409, "EMAIL_EXISTS", "Bu e-posta adresi zaten kayıtlı.");
            }

            throw;
        }

        return CreateAuthResult(user);
    }

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await database.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            throw new ServiceException(401, "INVALID_CREDENTIALS", "E-posta veya şifre hatalı.");
        }

        return CreateAuthResult(user);
    }

    public async Task LogoutAsync(string jwtId, CancellationToken cancellationToken)
    {
        if (!await database.RevokedTokens.AnyAsync(x => x.JwtId == jwtId, cancellationToken))
        {
            database.RevokedTokens.Add(new RevokedToken { JwtId = jwtId, ExpiresUtc = DateTime.UtcNow.AddHours(1) });
            await database.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserInfo> GetUserAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await database.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ServiceException(404, "USER_NOT_FOUND", "Kullanıcı bulunamadı.");
        return ToUserInfo(user);
    }

    public async Task<UserInfo> ChangeRoleAsync(int userId, string role, int? officeId, CancellationToken cancellationToken)
    {
        if (!Enum.GetNames<UserRole>().Contains(role, StringComparer.Ordinal))
        {
            throw new ServiceException(400, "INVALID_ROLE", "Geçersiz rol.");
        }
        var newRole = Enum.Parse<UserRole>(role);

        var user = await database.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new ServiceException(404, "USER_NOT_FOUND", "Kullanıcı bulunamadı.");

        if (officeId.HasValue)
        {
            if (!await database.Offices.AnyAsync(x => x.Id == officeId.Value, cancellationToken))
            {
                throw new ServiceException(400, "INVALID_OFFICE", "Seçilen ofis bulunamadı.");
            }

            user.OfficeId = officeId;
        }

        if (newRole == UserRole.OfficeManager && user.OfficeId is null)
        {
            throw new ServiceException(400, "OFFICE_REQUIRED", "Ofis yöneticisi için bir ofis seçilmelidir.");
        }

        user.Role = newRole;
        await database.SaveChangesAsync(cancellationToken);
        return ToUserInfo(user);
    }

    private AuthResult CreateAuthResult(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(1);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role.ToString())
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(jwtSettings.Issuer, jwtSettings.Audience, claims,
            notBefore: now, expires: expires, signingCredentials: credentials);
        return new AuthResult(new JwtSecurityTokenHandler().WriteToken(jwt), expires, ToUserInfo(user));
    }

    private static UserInfo ToUserInfo(User user) =>
        new(user.Id, user.FullName, user.Email, user.Role.ToString(), user.OfficeId);
}
