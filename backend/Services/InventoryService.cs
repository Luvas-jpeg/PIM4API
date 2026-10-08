using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class InventoryService(AppDbContext context)
{
    public const int MaxCourseQuantityPerOrder = 5;

    public async Task<ServiceResult<Dictionary<int, Course>>> ValidateAndReserveAsync(List<CreateOrderItemDTO> items)
    {
        if (items.Count == 0)
            return ServiceResult<Dictionary<int, Course>>.Fail("Pedido deve possuir ao menos um item.");

        foreach (var group in items.GroupBy(item => item.CourseId))
        {
            if (group.Any(item => item.Quantidade <= 0))
                return ServiceResult<Dictionary<int, Course>>.Fail("Quantidade deve ser maior que zero.");
            if (group.Sum(item => (long)item.Quantidade) > MaxCourseQuantityPerOrder)
                return ServiceResult<Dictionary<int, Course>>.Fail(
                    $"A quantidade maxima por pedido para cursos e {MaxCourseQuantityPerOrder} inscricoes.");
            if (group.Select(item => item.TurmaId).Distinct().Count() > 1)
                return ServiceResult<Dictionary<int, Course>>.Fail("Selecione apenas uma turma por curso no pedido.");
        }

        var courseIds = items.Select(item => item.CourseId).Distinct().ToList();
        var courses = await context.Courses
            .Where(course => courseIds.Contains(course.Id))
            .ToDictionaryAsync(course => course.Id);

        foreach (var item in items)
        {
            if (!courses.TryGetValue(item.CourseId, out var course))
                return ServiceResult<Dictionary<int, Course>>.Fail($"Curso #{item.CourseId} nao encontrado.");
            if (!course.IsActive)
                return ServiceResult<Dictionary<int, Course>>.Fail("O curso informado esta arquivado.");

            if (course.DeliveryMode == "ead")
            {
                if (item.TurmaId.HasValue)
                    return ServiceResult<Dictionary<int, Course>>.Fail(
                        $"O curso EAD '{course.Nome}' nao deve possuir turma.");
                continue;
            }
            if (!item.TurmaId.HasValue)
                return ServiceResult<Dictionary<int, Course>>.Fail(
                    $"O curso '{course.Nome}' exige a selecao de uma turma.");

            var courseClass = await context.CourseClasses.AsNoTracking()
                .FirstOrDefaultAsync(candidate => candidate.Id == item.TurmaId.Value);
            if (courseClass == null || courseClass.CourseId != course.Id)
                return ServiceResult<Dictionary<int, Course>>.Fail(
                    $"Turma #{item.TurmaId.Value} nao pertence ao curso informado.");
            if (courseClass.Status != "scheduled")
                return ServiceResult<Dictionary<int, Course>>.Fail("A turma selecionada nao esta com inscricoes abertas.");
            var now = DateTime.UtcNow;
            if (courseClass.DataRealizacao < now)
                return ServiceResult<Dictionary<int, Course>>.Fail("Nao e possivel comprar uma turma que ja iniciou.");

            var reserved = await context.CourseClasses
                .Where(candidate => candidate.Id == item.TurmaId.Value &&
                    candidate.CourseId == course.Id && candidate.Status == "scheduled" &&
                    candidate.DataRealizacao >= now && candidate.AvailableSeats >= item.Quantidade)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(candidate => candidate.AvailableSeats,
                        candidate => candidate.AvailableSeats - item.Quantidade)
                    .SetProperty(candidate => candidate.VafasDisponiveis,
                        candidate => candidate.VafasDisponiveis - item.Quantidade));
            if (reserved == 0)
                return ServiceResult<Dictionary<int, Course>>.Fail(
                    $"Vagas insuficientes na turma selecionada para {course.Nome}.");
        }
        return ServiceResult<Dictionary<int, Course>>.Ok(courses);
    }
}