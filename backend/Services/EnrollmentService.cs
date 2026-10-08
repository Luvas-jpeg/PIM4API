using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class EnrollmentService(AppDbContext context)
{
    public async Task CreateEnrollmentsForCourseAsync(
        User user, Course course, Order order, int quantity, int? classId = null)
    {
        if (course.DeliveryMode == "ead" && classId.HasValue)
            throw new InvalidOperationException("Cursos EAD nao possuem turmas presenciais.");
        if (course.DeliveryMode != "ead")
        {
            if (!classId.HasValue || !await context.CourseClasses.AnyAsync(
                item => item.Id == classId.Value && item.CourseId == course.Id))
                throw new InvalidOperationException("A turma nao pertence ao curso informado.");
        }

        var courseKey = course.Id.ToString();
        var student = await context.Students.FirstOrDefaultAsync(
            item => item.UserId == user.ID && item.CourseId == courseKey);
        if (student == null)
        {
            student = new Student
            {
                UserId = user.ID, Name = user.Nome, Email = user.Email, Phone = user.Phone,
                CourseId = courseKey, CourseName = course.Nome,
                EnrollmentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"), Status = "active"
            };
            context.Students.Add(student);
            await context.SaveChangesAsync();
        }

        var existing = await context.Enrollments.CountAsync(item =>
            item.OrderId == order.Id && item.StudentId == student.Id &&
            item.CourseId == course.Id && item.ClassId == classId);
        for (var index = existing; index < quantity; index++)
            context.Enrollments.Add(new Enrollment
            {
                CourseId = course.Id, ClassId = classId, StudentId = student.Id,
                OrderId = order.Id, Status = "active"
            });
    }
}