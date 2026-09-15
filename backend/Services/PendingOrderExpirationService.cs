using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EquipamentosMedicosApi.Services;

public sealed class PendingOrderExpirationService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PendingOrderExpirationService> _logger;

    public PendingOrderExpirationService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PendingOrderExpirationService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExpireOrdersAsync(stoppingToken);

        using var timer = new PeriodicTimer(CheckInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ExpireOrdersAsync(stoppingToken);
    }

    private async Task ExpireOrdersAsync(CancellationToken cancellationToken)
    {
        var expirationMinutes = _configuration.GetValue<int?>(
            "Payments:PendingOrderExpirationMinutes") ?? 30;

        if (expirationMinutes <= 0)
        {
            _logger.LogWarning(
                "Payments:PendingOrderExpirationMinutes deve ser maior que zero.");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<OrderService>();
        var expiredCount = await orderService.ExpirePendingOrdersAsync(
            TimeSpan.FromMinutes(expirationMinutes));

        if (expiredCount > 0)
        {
            _logger.LogInformation(
                "{ExpiredCount} pedido(s) pendente(s) expirado(s) e reservas liberadas.",
                expiredCount);
        }
    }
}
