using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
        var result = await _courseService.CreateAsync(request, GetUserId());
        return result.Success
            ? CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, CourseRequestDTO request)
    {
        var result = await _courseService.UpdateAsync(id, request, GetUserId());
        if (!result.Success)
            return result.Error?.Contains("nao encontrado", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        return Ok(result.Data);
    }

    [HttpPost("{id:int}/archive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Archive(int id)
        => await SetActive(id, false);

    [HttpPost("{id:int}/restore")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Restore(int id)
        => await SetActive(id, true);

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

    [HttpPatch("{courseId:int}/classes/{classId:int}/students/{studentId:int}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateEnrollmentStatus(
        int courseId,
        int classId,
        int studentId,
        EnrollmentStatusRequestDTO request)
    {
        var result = await _courseService.UpdateEnrollmentStatusAsync(
            courseId,
            classId,
            studentId,
            request.Status,
            GetUserId());

        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("{courseId:int}/classes/{classId:int}/students/{studentId:int}/transfer")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> TransferEnrollment(
        int courseId,
        int classId,
        int studentId,
        TransferEnrollmentRequestDTO request)
    {
        var result = await _courseService.TransferEnrollmentAsync(
            courseId,
            classId,
            studentId,
            request.TargetClassId,
            GetUserId());

        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("{courseId:int}/classes")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateClass(int courseId, CourseClassRequestDTO request)
    {
        var result = await _courseService.CreateClassAsync(courseId, request, GetUserId());
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
        var result = await _courseService.UpdateClassAsync(courseId, classId, request, GetUserId());
        if (!result.Success)
            return result.Error?.Contains("nao encontrada", StringComparison.OrdinalIgnoreCase) == true
                ? NotFound(new { message = result.Error })
                : BadRequest(new { message = result.Error });
        return Ok(result.Data);
    }

    [HttpGet("{courseId:int}/modules")]
    public async Task<IActionResult> GetModules(int courseId)
    {
        var modules = await _courseService.GetModulesAsync(courseId);
        return modules == null
            ? NotFound(new { message = "Curso nao encontrado." })
            : Ok(modules);
    }

    [HttpPost("{courseId:int}/modules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateModule(int courseId, CourseModuleRequestDTO request)
    {
        var result = await _courseService.CreateModuleAsync(courseId, request, GetUserId());
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{courseId:int}/modules/{moduleId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateModule(
        int courseId,
        int moduleId,
        CourseModuleRequestDTO request)
    {
        var result = await _courseService.UpdateModuleAsync(courseId, moduleId, request, GetUserId());
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPost("{courseId:int}/modules/{moduleId:int}/lessons")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLesson(
        int courseId,
        int moduleId,
        CourseLessonRequestDTO request)
    {
        var result = await _courseService.CreateLessonAsync(courseId, moduleId, request, GetUserId());
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{courseId:int}/modules/{moduleId:int}/lessons/{lessonId:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateLesson(
        int courseId,
        int moduleId,
        int lessonId,
        CourseLessonRequestDTO request)
    {
        var result = await _courseService.UpdateLessonAsync(
            courseId,
            moduleId,
            lessonId,
            request,
            GetUserId());
        return result.Success
            ? Ok(result.Data)
            : BadRequest(new { message = result.Error });
    }

    private async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var result = await _courseService.SetActiveAsync(id, isActive, GetUserId());
        if (!result.Success)
            return NotFound(new { message = result.Error });

        return Ok(result.Data);
    }

    private int? GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("sub");
        return claim != null && int.TryParse(claim.Value, out var userId) ? userId : null;
    }
}
