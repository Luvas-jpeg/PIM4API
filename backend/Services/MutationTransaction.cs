using EquipamentosMedicosApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EquipamentosMedicosApi.Services;

public static class MutationTransaction
{
    // Cross-process lock, released by commit/rollback. All seat/order mutations
    // acquire it BEFORE reading state. Reads remain concurrent. Keep transactions
    // short and never perform gateway/network calls while holding this lock.
    public static async Task<IDbContextTransaction> BeginAsync(AppDbContext context)
    {
        var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            if (context.Database.IsNpgsql())
                await context.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(1346981172, 1)");
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
}
