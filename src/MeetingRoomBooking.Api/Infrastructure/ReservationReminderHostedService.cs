using MeetingRoomBooking.Services.Interfaces;

namespace MeetingRoomBooking.Api.Infrastructure;

public sealed class ReservationReminderHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationReminderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<IReservationReminderProcessor>();
                var reminders = await processor.ClaimDueAsync(stoppingToken);
                foreach (var reminder in reminders)
                {
                    logger.LogInformation(
                        "REZERVASYON HATIRLATMASI: rezervasyon {ReservationId}, oda {RoomId}, başlangıç {StartsAtUtc:O} (UTC); yaklaşık 15 dakika kaldı.",
                        reminder.ReservationId, reminder.RoomId, reminder.StartsAtUtc);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Rezervasyon hatırlatmaları kontrol edilirken hata oluştu.");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        } while (!stoppingToken.IsCancellationRequested);
    }
}
