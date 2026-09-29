using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class PromoCodeService
{
    private static readonly HashSet<string> AllowedDiscountTypes = new()
    {
        "percentage",
        "fixed"
    };

    private readonly AppDbContext _context;

    public PromoCodeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PromoCodeDTO>> GetAllAsync()
    {
        return await _context.PromoCodes
            .OrderBy(promoCode => promoCode.Code)
            .Select(promoCode => ToResponse(promoCode))
            .ToListAsync();
    }

    public async Task<PromoCodeDTO?> GetByIdAsync(int id)
    {
        var promoCode = await _context.PromoCodes.FirstOrDefaultAsync(promoCode => promoCode.Id == id);

        return promoCode == null ? null : ToResponse(promoCode);
    }

    public async Task<ServiceResult<PromoCodeDTO>> ValidateAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ServiceResult<PromoCodeDTO>.Fail("Codigo promocional e obrigatorio.");
        }

        var promoCode = await FindByCodeAsync(code);

        if (promoCode == null || !IsValidForUse(promoCode))
        {
            return ServiceResult<PromoCodeDTO>.Fail("Codigo invalido, expirado ou ja utilizado.");
        }

        return ServiceResult<PromoCodeDTO>.Ok(ToResponse(promoCode));
    }

    public async Task<ServiceResult<PromoCodeDTO>> CreateAsync(PromoCodeRequestDTO request)
    {
        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return ServiceResult<PromoCodeDTO>.Fail(validation);
        }

        var normalizedCode = NormalizeCode(request.Code);
        var duplicated = await _context.PromoCodes.AnyAsync(promoCode => promoCode.Code == normalizedCode);

        if (duplicated)
        {
            return ServiceResult<PromoCodeDTO>.Fail("Codigo promocional ja cadastrado.");
        }

        var promoCode = new PromoCode();
        ApplyRequest(promoCode, request);

        _context.PromoCodes.Add(promoCode);
        await _context.SaveChangesAsync();

        return ServiceResult<PromoCodeDTO>.Ok(ToResponse(promoCode));
    }

    public async Task<ServiceResult<PromoCodeDTO>> UpdateAsync(int id, PromoCodeRequestDTO request)
    {
        var validation = ValidateRequest(request);
        if (validation != null)
        {
            return ServiceResult<PromoCodeDTO>.Fail(validation);
        }

        var promoCode = await _context.PromoCodes.FirstOrDefaultAsync(promoCode => promoCode.Id == id);

        if (promoCode == null)
        {
            return ServiceResult<PromoCodeDTO>.Fail("Codigo promocional nao encontrado.");
        }

        var normalizedCode = NormalizeCode(request.Code);
        var duplicated = await _context.PromoCodes
            .AnyAsync(otherPromoCode => otherPromoCode.Id != id && otherPromoCode.Code == normalizedCode);

        if (duplicated)
        {
            return ServiceResult<PromoCodeDTO>.Fail("Codigo promocional ja cadastrado.");
        }

        ApplyRequest(promoCode, request);
        await _context.SaveChangesAsync();

        return ServiceResult<PromoCodeDTO>.Ok(ToResponse(promoCode));
    }
    public async Task<bool> DeleteAsync(int id)
    {
        var promoCode = await _context.PromoCodes.FirstOrDefaultAsync(promoCode => promoCode.Id == id);

        if (promoCode == null)
        {
            return false;
        }

        _context.PromoCodes.Remove(promoCode);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<ServiceResult<decimal>> ApplyDiscountAsync(string? code, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return ServiceResult<decimal>.Ok(0m);
        }

        var promoCode = await FindByCodeAsync(code);

        if (promoCode == null || !IsValidForUse(promoCode))
        {
            return ServiceResult<decimal>.Fail("Codigo promocional invalido.");
        }

        var discount = promoCode.DiscountType == "percentage"
            ? subtotal * (promoCode.Discount / 100)
            : promoCode.Discount;

        discount = Math.Min(discount, subtotal);
        promoCode.UsageCount += 1;

        return ServiceResult<decimal>.Ok(discount);
    }

    private Task<PromoCode?> FindByCodeAsync(string code)
    {
        var normalizedCode = NormalizeCode(code);

        return _context.PromoCodes.FirstOrDefaultAsync(promoCode => promoCode.Code == normalizedCode);
    }

    private static PromoCodeDTO ToResponse(PromoCode promoCode)
    {
        return new PromoCodeDTO
        {
            Id = promoCode.Id,
            Code = promoCode.Code,
            Discount = promoCode.Discount,
            DiscountType = promoCode.DiscountType,
            StartDate = promoCode.StartDate,
            EndDate = promoCode.EndDate,
            IsActive = promoCode.IsActive,
            UsageLimit = promoCode.UsageLimit,
            UsageCount = promoCode.UsageCount
        };
    }

    private static void ApplyRequest(PromoCode promoCode, PromoCodeRequestDTO request)
    {
        promoCode.Code = NormalizeCode(request.Code);
        promoCode.Discount = request.Discount;
        promoCode.DiscountType = request.DiscountType.Trim();
        promoCode.StartDate = request.StartDate.Trim();
        promoCode.EndDate = request.EndDate.Trim();
        promoCode.IsActive = request.IsActive;
        promoCode.UsageLimit = request.UsageLimit;
        promoCode.UsageCount = request.UsageCount;
    }

    private static string? ValidateRequest(PromoCodeRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Codigo promocional e obrigatorio.";
        }

        if (request.Discount <= 0)
        {
            return "Desconto deve ser maior que zero.";
        }

        if (!AllowedDiscountTypes.Contains(request.DiscountType))
        {
            return "DiscountType deve ser 'percentage' ou 'fixed'.";
        }

        if (request.DiscountType == "percentage" && request.Discount > 100)
        {
            return "Desconto percentual nao pode ser maior que 100.";
        }

        if (string.IsNullOrWhiteSpace(request.StartDate) || string.IsNullOrWhiteSpace(request.EndDate))
        {
            return "Datas de inicio e termino sao obrigatorias.";
        }

        if (!DateOnly.TryParse(request.StartDate, out var startDate) ||
            !DateOnly.TryParse(request.EndDate, out var endDate))
        {
            return "Datas devem estar em formato valido.";
        }

        if (endDate < startDate)
        {
            return "Data de termino nao pode ser anterior a data de inicio.";
        }

        if (request.UsageLimit.HasValue && request.UsageLimit.Value < 0)
        {
            return "Limite de uso nao pode ser negativo.";
        }

        if (request.UsageCount < 0)
        {
            return "Quantidade de usos nao pode ser negativa.";
        }

        return null;
    }

    private static bool IsValidForUse(PromoCode promoCode)
    {
        if (!promoCode.IsActive)
        {
            return false;
        }

        if (promoCode.UsageLimit.HasValue && promoCode.UsageCount >= promoCode.UsageLimit.Value)
        {
            return false;
        }

        if (!DateOnly.TryParse(promoCode.StartDate, out var startDate) ||
            !DateOnly.TryParse(promoCode.EndDate, out var endDate))
        {
            return false;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return today >= startDate && today <= endDate;
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }
}
