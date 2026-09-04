using System.Globalization;
using Microsoft.EntityFrameworkCore;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class EnrollmentService
{
    private readonly AppDbContext _context;

    public EnrollmentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task CreateEnrollmentsForCourseAsync(
        User user,
        Product product,
        Order order,
        int quantity,
        int? classId = null)
    {
        if (product.TipoProduto != "course")
        {
            return;
        }

        var student = await _context.Students.FirstOrDefaultAsync(s =>
            s.Email == user.Email &&
            s.CourseId == product.Id.ToString());

        if (student == null)
        {
            student = new Student
            {
                Name = user.Nome,
                Email = user.Email,
                Phone = user.Phone,
                CourseId = product.Id.ToString(),
                CourseName = product.Nome,
                EnrollmentDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Status = "active"
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();
        }

        var courseClass = classId.HasValue
            ? await _context.CourseClasses.FirstOrDefaultAsync(c =>
                c.Id == classId.Value &&
                ((c.CourseId.HasValue && c.Course!.LegacyProductId == product.Id) ||
                 (!c.CourseId.HasValue && c.ProdutoId == product.Id)))
            : await _context.CourseClasses.FirstOrDefaultAsync(c => c.ProdutoId == product.Id);

        if (courseClass == null)
        {
            courseClass = new CourseClass
            {
                ProdutoId = product.Id,
                DataRealizacao = ParseCourseDate(product.Date),
                Local = product.Location,
                Instructor = product.Instructor,
                VafasDisponiveis = product.Estoque ?? 0
            };

            courseClass.AvailableSeats = courseClass.VafasDisponiveis;

            _context.CourseClasses.Add(courseClass);
            await _context.SaveChangesAsync();
        }

        var existingEnrollments = await _context.Enrollments.CountAsync(enrollment =>
            enrollment.OrderId == order.Id &&
            enrollment.StudentId == student.Id &&
            enrollment.ClassId == courseClass.Id);

        var enrollmentsToCreate = Math.Max(0, quantity - existingEnrollments);
        // Explicit class selection reserves its seats while creating the order.
        // Legacy orders reserve Product stock and consume class seats on payment.
        if (!classId.HasValue && courseClass.VafasDisponiveis < enrollmentsToCreate)
        {
            throw new InvalidOperationException($"Nao ha vagas suficientes na turma do curso '{product.Nome}'.");
        }

        if (!classId.HasValue)
        {
            courseClass.VafasDisponiveis -= enrollmentsToCreate;
            courseClass.AvailableSeats = courseClass.VafasDisponiveis;
        }

        for (var i = 0; i < enrollmentsToCreate; i++)
        {
            _context.Enrollments.Add(new Enrollment
            {
                ClassId = courseClass.Id,
                StudentId = student.Id,
                OrderId = order.Id,
                Status = "active"
            });
        }
    }

    private static DateTime ParseCourseDate(string date)
    {
        if (DateTime.TryParse(
            date,
            CultureInfo.GetCultureInfo("pt-BR"),
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }
}