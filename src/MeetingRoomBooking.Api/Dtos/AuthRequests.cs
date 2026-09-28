using System.ComponentModel.DataAnnotations;

namespace MeetingRoomBooking.Api.Dtos;

public sealed class RegisterRequest
{
    [Required, StringLength(150, MinimumLength = 2)]
    public string FullName { get; init; } = string.Empty;

    [Required, EmailAddress, MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required, MinLength(12), MaxLength(256)]
    public string Password { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? OfficeId { get; init; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

public sealed class ChangeRoleRequest
{
    [Required]
    public string Role { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? OfficeId { get; init; }
}
