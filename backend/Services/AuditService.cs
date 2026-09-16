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

    private static string? Serialize(object? value)
        => value == null ? null : JsonSerializer.Serialize(value);
}
