using Nanny_BackEnd.Repositories;

namespace Nanny_BackEnd.Services;


public class OtpCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OtpCleanupService> _logger;

    public OtpCleanupService(IServiceScopeFactory scopeFactory, ILogger<OtpCleanupService> logger)
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

                var otpRepo = scope.ServiceProvider.GetRequiredService<OtpRepository>();

                await otpRepo.CleanupExpiredAsync();
                await otpRepo.SaveChangesAsync();

                _logger.LogInformation("OtpCleanupService: đã xóa các OTP hết hạn lúc {Time}", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OtpCleanupService: lỗi khi dọn dữ liệu hết hạn");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }
}
