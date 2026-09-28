using MeetingRoomBooking.Data;
using Microsoft.Extensions.DependencyInjection;

namespace MeetingRoomBooking.Api.Infrastructure;

public sealed class DemoDataHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var password = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12 || password.StartsWith("REPLACE_WITH_"))
        {
            throw new InvalidOperationException("Set Seed:DemoPassword in local appsettings.json to at least 12 characters.");
        }

        using var scope = scopeFactory.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        await DemoDataSeeder.SeedAsync(database, password, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
