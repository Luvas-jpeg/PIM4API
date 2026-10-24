using System.Text.Json;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class AuditService
{
    private readonly AppDbContext _context;

    public AuditService(AppDbContext context)
    {
        _context = context;
    }

    public void Add(
        int? userId,
        string action,
        string entityType,
        object entityId,
        object? previousValue = null,
        object? newValue = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString() ?? string.Empty,
            PreviousValue = Serialize(previousValue),
            NewValue = Serialize(newValue)
        });
    }

    public async Task AddCreatedAsync(int? userId, string entityType, Func<object> entityId, object newValue)
    {
        var ownsTransaction = _context.Database.CurrentTransaction == null;
        await using var transaction = ownsTransaction ? await MutationTransaction.BeginAsync(_context) : null;
        await _context.SaveChangesAsync();
        Add(userId, "created", entityType, entityId(), null, newValue);
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }

    private static string? Serialize(object? value)
        => value == null ? null : JsonSerializer.Serialize(value);
}
