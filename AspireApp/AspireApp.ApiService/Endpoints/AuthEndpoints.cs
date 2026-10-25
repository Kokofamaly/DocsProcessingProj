using System.Security.Claims;
using CommonData;
using DocsProcessingProj.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocsProcessingProj.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/register", Register).AllowAnonymous();
        app.MapPost("/login", Login).AllowAnonymous();
        app.MapPost("/logout", Logout).AllowAnonymous();
        app.MapGet("/me", Me);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest req, HttpContext http, AppDbContext db, IPasswordHasher<User> hasher)
    {
        var login = req.login?.Trim();

        if (string.IsNullOrEmpty(login) || login.Length < 3)
            return Results.BadRequest("Login must be at least 3 characters.");
        if (string.IsNullOrEmpty(req.password) || req.password.Length < 6)
            return Results.BadRequest("Password must be at least 6 characters.");

        if (await db.Users.AnyAsync(u => u.Login == login))
            return Results.Conflict("Login is taken");

        var user = new User { Login = login };
        user.HashedPassword = hasher.HashPassword(user, req.password);

        await db.Users.AddAsync(user);
        await db.SaveChangesAsync();

        await SignInUserAsync(http, user);
        return Results.Created();
    }

    private static async Task<IResult> Login(
        LoginRequest req, HttpContext http, AppDbContext db, IPasswordHasher<User> hasher)
    {
        var login = req.login?.Trim();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Login == login);

        if (user is null || string.IsNullOrEmpty(req.password))
            return Results.Unauthorized();

        var passwordVerification = hasher.VerifyHashedPassword(user, user.HashedPassword, req.password);
        if (passwordVerification == PasswordVerificationResult.Failed)
            return Results.Unauthorized();

        await SignInUserAsync(http, user);
        return Results.Ok();
    }

    private static async Task<IResult> Logout(HttpContext http)
    {
        await http.SignOutAsync();
        return Results.Ok();
    }

    private static IResult Me(ClaimsPrincipal user) =>
        Results.Ok(new { login = user.Identity!.Name, role = user.FindFirstValue(ClaimTypes.Role) });

    private static Task SignInUserAsync(HttpContext http, User user)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Login),
                new Claim(ClaimTypes.Role, user.Role)
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);

        return http.SignInAsync(new ClaimsPrincipal(identity));
    }
}