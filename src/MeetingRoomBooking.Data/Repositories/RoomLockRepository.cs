using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MeetingRoomBooking.Data.Repositories;

public sealed record LockedRoom(int Id, int OfficeId, int Capacity, bool IsActive);

public sealed class RoomLockRepository(BookingDbContext database)
{
    public async Task<LockedRoom?> LockAsync(int roomId, CancellationToken cancellationToken)
    {
        var transaction = database.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A database transaction is required before locking a room.");
        var connection = database.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            throw new InvalidOperationException("The transaction connection is not open.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT `Id`, `OfficeId`, `Capacity`, `IsActive` FROM `Rooms` WHERE `Id` = @roomId FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@roomId";
        parameter.Value = roomId;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new LockedRoom(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetBoolean(3));
    }
}
