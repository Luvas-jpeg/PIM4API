using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public sealed class PromoCodeServiceTests
{
    [Fact]
    public async Task AppliesPercentageDiscountAndIncrementsUsage()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.PromoCodes.Add(new PromoCode
        {
            Code = "ACADEMICO10",
            Discount = 10,
            DiscountType = "percentage",
            StartDate = "2026-01-01",
            EndDate = "2026-12-31",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var result = await new PromoCodeService(context).ApplyDiscountAsync(" academico10 ", 200m);

        Assert.True(result.Success);
        Assert.Equal(20m, result.Data);
        Assert.Equal(1, (await context.PromoCodes.SingleAsync(code => code.Code == "ACADEMICO10")).UsageCount);
    }
}
