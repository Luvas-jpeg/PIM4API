using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class InventoryService
{
    public const int MaxCourseQuantityPerOrder = 5;

    private readonly AppDbContext _context;

    public InventoryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ServiceResult<Dictionary<int, Product>>> ValidateAndReserveAsync(List<CreateOrderItemDTO> items)
    {
        if (items.Count == 0)
        {
            return ServiceResult<Dictionary<int, Product>>.Fail("Pedido deve possuir ao menos um item.");
        }

        var productIds = items
            .Select(item => item.ProdutoId)
            .Distinct()
            .ToList();

        var products = await _context.Products
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        foreach (var item in items)
        {
            if (!products.TryGetValue(item.ProdutoId, out var product))
            {
                return ServiceResult<Dictionary<int, Product>>.Fail($"Produto #{item.ProdutoId} nao encontrado.");
            }

            if (item.Quantidade <= 0)
            {
                return ServiceResult<Dictionary<int, Product>>.Fail("Quantidade deve ser maior que zero.");
            }

            if (item.Quantidade > MaxCourseQuantityPerOrder &&
                product.TipoProduto == "course")
            {
                return ServiceResult<Dictionary<int, Product>>.Fail(
                    $"A quantidade maxima por pedido para cursos e {MaxCourseQuantityPerOrder} inscricoes.");
            }

            if (product.TipoProduto != "course")
            {
                return ServiceResult<Dictionary<int, Product>>.Fail(
                    "A loja aceita somente cursos.");
            }

            var isMigratedCourse = product.TipoProduto == "course"
                && await _context.Courses.AnyAsync(course => course.LegacyProductId == product.Id);

            if (isMigratedCourse && !item.TurmaId.HasValue)
            {
                return ServiceResult<Dictionary<int, Product>>.Fail(
                    $"O curso '{product.Nome}' exige a selecao de uma turma.");
            }

            if (item.TurmaId.HasValue)
            {
                if (product.TipoProduto != "course")
                {
                    return ServiceResult<Dictionary<int, Product>>.Fail(
                        $"O produto '{product.Nome}' nao possui turmas.");
                }

                var courseClass = await _context.CourseClasses
                    .Include(courseClass => courseClass.Course)
                    .FirstOrDefaultAsync(courseClass => courseClass.Id == item.TurmaId.Value);

                if (courseClass == null ||
                    (courseClass.CourseId.HasValue &&
                     courseClass.Course?.LegacyProductId != product.Id) ||
                    (!courseClass.CourseId.HasValue && courseClass.ProdutoId != product.Id))
                {
                    return ServiceResult<Dictionary<int, Product>>.Fail(
                        $"Turma #{item.TurmaId.Value} nao pertence ao curso informado.");
                }

                if (courseClass.Status != "scheduled")
                {
                    return ServiceResult<Dictionary<int, Product>>.Fail(
                        "A turma selecionada nao esta com inscricoes abertas.");
                }

                if (courseClass.DataRealizacao < DateTime.UtcNow)
                {
                    return ServiceResult<Dictionary<int, Product>>.Fail(
                        "Nao e possivel comprar uma turma que ja iniciou.");
                }

                var reservedSeats = await _context.CourseClasses
                    .Where(candidate =>
                        candidate.Id == item.TurmaId.Value &&
                        candidate.Status != "cancelled" &&
                        candidate.AvailableSeats >= item.Quantidade)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(
                            candidate => candidate.AvailableSeats,
                            candidate => candidate.AvailableSeats - item.Quantidade)
                        .SetProperty(
                            candidate => candidate.VafasDisponiveis,
                            candidate => candidate.VafasDisponiveis - item.Quantidade));

                if (reservedSeats == 0)
                {
                    return ServiceResult<Dictionary<int, Product>>.Fail(
                        $"Vagas insuficientes na turma selecionada para {product.Nome}.");
                }

                continue;
            }

            var currentStock = product.Estoque ?? 0;

            if (currentStock < item.Quantidade)
            {
                return ServiceResult<Dictionary<int, Product>>.Fail($"Estoque/vagas insuficientes para {product.Nome}.");
            }

            product.Estoque = currentStock - item.Quantidade;
        }

        return ServiceResult<Dictionary<int, Product>>.Ok(products);
    }
}