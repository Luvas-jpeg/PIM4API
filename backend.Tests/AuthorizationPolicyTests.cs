using System.Reflection;
using EquipamentosMedicosApi.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace backend.Tests;

public sealed class AuthorizationPolicyTests
{
    [Theory]
    [InlineData(typeof(CoursesController), nameof(CoursesController.GetAll))]
    [InlineData(typeof(CoursesController), nameof(CoursesController.GetModules))]
    [InlineData(typeof(CoursesController), nameof(CoursesController.GetAssessments))]
    [InlineData(typeof(OrdersController), nameof(OrdersController.GetAllOrders))]
    [InlineData(typeof(OrdersController), nameof(OrdersController.UpdateStatus))]
    [InlineData(typeof(OrdersController), nameof(OrdersController.Refund))]
    [InlineData(typeof(PaymentsController), nameof(PaymentsController.GetWebhookEvents))]
    [InlineData(typeof(PromoCodesController), nameof(PromoCodesController.GetAll))]
    [InlineData(typeof(PromoCodesController), nameof(PromoCodesController.Create))]
    public void AdministrativeActionsRequireAdminRole(Type controllerType, string actionName)
    {
        var method = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        Assert.True(RequiresAdmin(controllerType, method!),
            $"{controllerType.Name}.{actionName} deve exigir a role Admin.");
    }

    [Theory]
    [InlineData(typeof(AuditController))]
    [InlineData(typeof(StudentsController))]
    public void AdministrativeControllersRequireAdminRole(Type controllerType)
    {
        Assert.True(HasAdminRole(controllerType.GetCustomAttributes<AuthorizeAttribute>()),
            $"{controllerType.Name} deve exigir a role Admin.");
    }

    private static bool RequiresAdmin(Type controllerType, MethodInfo method)
    {
        return HasAdminRole(controllerType.GetCustomAttributes<AuthorizeAttribute>()) ||
               HasAdminRole(method.GetCustomAttributes<AuthorizeAttribute>());
    }

    private static bool HasAdminRole(IEnumerable<AuthorizeAttribute> attributes)
    {
        return attributes
            .SelectMany(attribute => (attribute.Roles ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Contains("Admin", StringComparer.Ordinal);
    }
}