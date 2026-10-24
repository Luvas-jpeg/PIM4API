using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EquipamentosMedicosApi.Services;

// Caller holds MutationTransaction and commits all related changes together.
public sealed class EnrollmentStateService(AppDbContext context)
{
    public async Task<string?> ChangeAsync(Enrollment enrollment, string status)
    {
        if (enrollment.Status == status) return null;
        var wasReserved = enrollment.Status != "cancelled";
        var willReserve = status != "cancelled";
        if (enrollment.ClassId.HasValue)
        {
            var group = enrollment.Class ?? await context.CourseClasses.FindAsync(enrollment.ClassId.Value);
            if (group == null) return "Turma nao encontrada.";
            if (!wasReserved && willReserve)
            {
                if (group.Status != "scheduled" || group.DataRealizacao <= DateTime.UtcNow || group.AvailableSeats <= 0)
                    return "A turma nao possui inscricoes abertas ou vagas disponiveis.";
                if (await context.Orders.AnyAsync(o => o.Id == enrollment.OrderId &&
                    (o.PaymentStatus == "refunded" || o.PaymentStatus == "cancelled" || o.PaymentStatus == "refused")))
                    return "Nao e possivel reativar matricula de pedido encerrado ou reembolsado.";
                group.AvailableSeats--;
            }
            else if (wasReserved && !willReserve)
                group.AvailableSeats = Math.Min(group.Capacity, group.AvailableSeats + 1);
        }
        if (!wasReserved || !willReserve)
        {
            enrollment.ParticipationConfirmedAt = null;
            enrollment.ParticipationStartDate = null;
        }
        enrollment.Status = status;
        return null;
    }
}
