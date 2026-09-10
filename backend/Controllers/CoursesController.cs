using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Services;

namespace EquipamentosMedicosApi.Controllers;

[Route("api/courses")]
[ApiController]
public class CoursesController : ControllerBase
{
    private readonly CourseService _courseService;

    public CoursesController(CourseService courseService)
    {
        _courseService = courseService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        => Ok(await _courseService.GetAllAsync(includeInactive));

    [HttpGet("catalog")]
    public async Task<IActionResult> GetCatalog([FromQuery] CourseCatalogQueryDTO request)
        => Ok(await _courseService.GetCatalogAsync(request));

    [HttpGet("catalog/options")]
    public async Task<IActionResult> GetCatalogOptions()
        => Ok(await _courseService.GetCatalogOptionsAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, [FromQuery] bool includeInactive = false)
    {
        var course = await _courseService.GetByIdAsync(id, includeInactive);
        return course == null ? NotFound(new { message = "Curso nao encontrado." }) : Ok(course);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CourseRequestDTO request)
    {
        var result = await _courseService.CreateAsync(request);
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, CourseRequestDTO request)
    {
        var result = await _courseService.UpdateAsync(id, request);
        if (!result.Success)
            return result.Error?.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{courseId:int}/classes")]
    public async Task<IActionResult> GetClasses(int courseId, [FromQuery] bool includeInactive = false)
        => Ok(await _courseService.GetClassesAsync(courseId, includeInactive));

    [HttpGet("{courseId:int}/classes/{classId:int}")]
    public async Task<IActionResult> GetClass(int courseId, int classId, [FromQuery] bool includeInactive = false)
    {
        var courseClass = await _courseService.GetClassAsync(courseId, classId, includeInactive);
        return courseClass == null
            ? NotFound(new { message = "Turma nao encontrada." })
            : Ok(courseClass);
    }

    [HttpGet("{courseId:int}/classes/{classId:int}/students")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetClassStudents(int courseId, int classId)
    {
        var students = await _courseService.GetClassStudentsAsync(courseId, classId);
        return students == null
            ? NotFound(new { message = "Turma nao encontrada." })
            : Ok(students);
    }

    [HttpPost("{courseId:int}/classes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClass(int courseId, CourseClassRequestDTO request)
    {
        var result = await _courseService.CreateClassAsync(courseId, request);
        if (!result.Success)
            return result.Error?.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        return CreatedAtAction(nameof(GetClasses), new { courseId }, result.Data);
    }

    [HttpPut("{courseId:int}/classes/{classId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateClass(int courseId, int classId, CourseClassRequestDTO request)
    {
        var result = await _courseService.UpdateClassAsync(courseId, classId, request);
        if (!result.Success)
            return result.Error?.Contains("nao encontrada", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        return Ok(result.Data);
    }
}
