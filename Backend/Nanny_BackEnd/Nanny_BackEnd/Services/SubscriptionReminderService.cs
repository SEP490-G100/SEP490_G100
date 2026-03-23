namespace Nanny_BackEnd.Services;

public class SubscriptionReminderService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(12);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionReminderService> _logger;

    public SubscriptionReminderService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

                var createdCount = await notificationService.createSubscriptionExpiryReminders();
                if (createdCount > 0)
                {
                    _logger.LogInformation(
                        "SubscriptionReminderService: da tao {Count} thong bao nhac gia han luc {Time}",
                        createdCount,
                        DateTime.UtcNow);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubscriptionReminderService: loi khi tao thong bao nhac gia han");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
