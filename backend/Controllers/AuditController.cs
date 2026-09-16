using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;

namespace EquipamentosMedicosApi.Controllers;

[Route("api/audit")]
[ApiController]
[Authorize(Roles = "Admin")]
public class AuditController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuditController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int limit = 100)
    {
        var take = Math.Clamp(limit, 1, 500);
        var logs = await _context.AuditLogs
            .AsNoTracking()
            .Include(log => log.User)
            .OrderByDescending(log => log.CreatedAt)
            .Take(take)
            .Select(log => new
            {
                log.Id,
                log.UserId,
                UserName = log.User == null ? null : log.User.Nome,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.PreviousValue,
                log.NewValue,
                log.CreatedAt
            })
            .ToListAsync();

        return Ok(logs);
    }
}
