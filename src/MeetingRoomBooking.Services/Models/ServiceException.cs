namespace MeetingRoomBooking.Services.Models;

public sealed class ServiceException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}
