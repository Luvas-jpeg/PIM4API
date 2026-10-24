using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipamentosMedicosApi.Services;

public sealed class ParticipationService(AppDbContext context, TimeProvider clock)
{
    public static ParticipationDTO Describe(Enrollment enrollment, DateTime now)
    {
        var group = enrollment.Class;
        if (group == null) return new();
        var opensAt = group.DataRealizacao.AddDays(-7);
        if (enrollment.Status != "active" || group.Status is "cancelled" or "completed" or "finished")
            return new() { Status = "unavailable", OpensAt = opensAt };
        if (enrollment.ParticipationConfirmedAt.HasValue &&
            enrollment.ParticipationStartDate == group.DataRealizacao)
            return new() { Status = "confirmed", OpensAt = opensAt,
                ConfirmedAt = enrollment.ParticipationConfirmedAt };
        var canConfirm = now >= opensAt && now < group.DataRealizacao && group.Status == "scheduled";
        return new() { Status = canConfirm ? "pending" : now < opensAt ? "waiting" : "closed",
            OpensAt = opensAt, CanConfirm = canConfirm };
    }

    public async Task<ParticipationDTO?> ConfirmAsync(int enrollmentId, int userId)
    {
        var enrollment = await context.Enrollments.Include(e => e.Class)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId && e.Student != null && e.Student.UserId == userId);
        if (enrollment == null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var state = Describe(enrollment, now);
        if (state.Status == "confirmed") return state;
        if (!state.CanConfirm)
            throw new InvalidOperationException("A confirmacao fica disponivel nos sete dias anteriores ao inicio, apenas para matriculas e turmas ativas.");
        var classId = enrollment.ClassId;
        var start = enrollment.Class!.DataRealizacao;
        // Keep confirmation tied to the class/date checked above, even if an admin
        // transfers the enrollment or reschedules concurrently.
        var eligibleIds = context.Enrollments.Where(e => e.Id == enrollmentId &&
            e.Student != null && e.Student.UserId == userId &&
            e.Status == "active" && e.ClassId == classId && e.Class != null &&
            e.Class.Status == "scheduled" && e.Class.DataRealizacao == start &&
            (e.ParticipationConfirmedAt == null || e.ParticipationStartDate != start))
            .Select(e => e.Id);
        await context.Enrollments.Where(e => eligibleIds.Contains(e.Id))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.ParticipationConfirmedAt, now)
                .SetProperty(e => e.ParticipationStartDate, start));
        await context.Entry(enrollment).ReloadAsync();
        var current = await context.Enrollments.AsNoTracking().Include(e => e.Class)
            .FirstAsync(e => e.Id == enrollmentId);
        var result = Describe(current, now);
        if (result.Status != "confirmed")
            throw new InvalidOperationException("A turma foi alterada. Atualize a matricula antes de confirmar.");
        return result;
    }
}
