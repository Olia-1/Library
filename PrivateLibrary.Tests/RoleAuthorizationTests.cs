using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using PrivateLibrary.Controllers;
using Xunit;

namespace PrivateLibrary.Tests;

public class RoleAuthorizationTests
{
    [Fact(DisplayName = "AdminController - перевірка ролі Admin")]
    public void AdminController_Requires_Admin_Role()
    {
        var authorization = typeof(AdminController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
        Assert.Equal("Admin", authorization!.Roles);
    }

    [Fact(DisplayName = "LibrarianController - перевірка ролі Librarian")]
    public void LibrarianController_Requires_Librarian_Role()
    {
        var authorization = typeof(LibrarianController)
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorization);
        Assert.Equal("Librarian", authorization!.Roles);
    }
}