using Microsoft.EntityFrameworkCore;
using System.Globalization;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.DTOs;
using EquipamentosMedicosApi.Models;

namespace EquipamentosMedicosApi.Services;

public class ProductService
{
    private readonly AppDbContext _context;

    public ProductService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProductResponseDTO>> GetAllAsync(string? tipo)
    {
        var query = _context.Products.Where(product => product.TipoProduto == "course");

        if (!string.IsNullOrWhiteSpace(tipo) && tipo != "course")
        {
            return [];
        }

        return await query
            .Select(p => ToResponse(p))
            .ToListAsync();
    }

    public async Task<ProductResponseDTO?> GetByIdAsync(int id)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.TipoProduto == "course");

        if (product == null)
        {
            return null;
        }

        return ToResponse(product);
    }

    public async Task<ProductResponseDTO> CreateAsync(ProductRequestDTO request)
    {
        var product = new Product();

        ApplyRequest(product, request);

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        if (product.TipoProduto == "course")
        {
            _context.CourseClasses.Add(CreateCourseClass(product));
            _context.Courses.Add(CreateCourse(product));
            await _context.SaveChangesAsync();
        }

        return ToResponse(product);
    }

    public async Task<ProductResponseDTO?> UpdateAsync(int id, ProductRequestDTO request)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return null;
        }

        ApplyRequest(product, request);

        if (product.TipoProduto == "course")
        {
            var course = await _context.Courses
                .FirstOrDefaultAsync(course => course.LegacyProductId == product.Id);
            if (course == null)
            {
                _context.Courses.Add(CreateCourse(product));
            }
            else
            {
                course.Nome = product.Nome;
                course.Description = product.Description;
                course.Preco = product.Preco;
                course.Image = product.Image;
                course.Category = product.Category;
            }

            var courseClass = await _context.CourseClasses
                .FirstOrDefaultAsync(courseClass => courseClass.ProdutoId == product.Id);

            if (courseClass == null)
            {
                _context.CourseClasses.Add(CreateCourseClass(product));
            }
            else
            {
                courseClass.DataRealizacao = ParseCourseDate(product.Date);
                courseClass.Local = product.Location;
                courseClass.Instructor = product.Instructor;
                courseClass.AvailableSeats = product.Estoque ?? 0;
                courseClass.VafasDisponiveis = courseClass.AvailableSeats;
            }
        }

        await _context.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return false;
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();

        return true;
    }

    public string? ValidateRequest(ProductRequestDTO request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
        {
            return "Nome é obrigatório.";
        }

        if (request.Preco < 0)
        {
            return "Preço não pode ser negativo.";
        }

        if (request.TipoProduto != "equipment" && request.TipoProduto != "course")
        {
            return "TipoProduto deve ser 'equipment' ou 'course'.";
        }

        if (request.Estoque < 0)
        {
            return "Estoque não pode ser negativo.";
        }

        return null;
    }

    private static ProductResponseDTO ToResponse(Product product)
    {
        return new ProductResponseDTO
        {
            Id = product.Id,
            Nome = product.Nome,
            Preco = product.Preco,
            TipoProduto = product.TipoProduto,
            Estoque = product.Estoque ?? 0,
            Description = product.Description,
            Image = product.Image,
            Category = product.Category,
            Date = product.Date,
            Location = product.Location,
            Instructor = product.Instructor
        };
    }

    private static void ApplyRequest(Product product, ProductRequestDTO request)
    {
        product.Nome = request.Nome.Trim();
        product.Preco = request.Preco;
        product.TipoProduto = request.TipoProduto.Trim();
        product.Estoque = request.Estoque;
        product.Description = request.Description.Trim();
        product.Image = request.Image.Trim();
        product.Category = request.Category.Trim();
        product.Date = request.Date.Trim();
        product.Location = request.Location.Trim();
        product.Instructor = request.Instructor.Trim();
    }

    private static CourseClass CreateCourseClass(Product product)
    {
        return new CourseClass
        {
            ProdutoId = product.Id,
            DataRealizacao = ParseCourseDate(product.Date),
            Local = product.Location,
            Instructor = product.Instructor,
            VafasDisponiveis = product.Estoque ?? 0,
            Capacity = product.Estoque ?? 0,
            AvailableSeats = product.Estoque ?? 0
        };
    }

    private static Course CreateCourse(Product product)
    {
        return new Course
        {
            Nome = product.Nome,
            Description = product.Description,
            Preco = product.Preco,
            Image = product.Image,
            Category = product.Category,
            LegacyProductId = product.Id,
            IsActive = true
        };
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