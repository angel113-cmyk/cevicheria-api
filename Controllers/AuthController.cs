using System.ComponentModel.DataAnnotations;
using Cevicheria.Api.Auth;
using Cevicheria.Api.Data;
using Cevicheria.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Cevicheria.Api.Controllers;

public record LoginRequest([Required, MaxLength(50)] string NombreUsuario, [Required, MaxLength(128)] string Password);
public record LoginResponse(string Token, DateTime ExpiraUtc, string NombreUsuario, string Rol);
public record CrearUsuarioRequest(
    [Required, StringLength(50, MinimumLength = 3)] string NombreUsuario,
    [Required, StringLength(128, MinimumLength = 6)] string Password,
    [Required] string Rol);
public record CambiarCuentaRequest(
    [Required, MaxLength(128)] string PasswordActual,
    [StringLength(50, MinimumLength = 3)] string? NuevoNombreUsuario,
    [StringLength(128, MinimumLength = 6)] string? NuevaPassword);

[ApiController]
[Route("api/[controller]")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    // Hash ficticio: se verifica aunque el usuario no exista para que el tiempo de respuesta no revele usuarios válidos.
    private static readonly string HashFalso = BCrypt.Net.BCrypt.HashPassword("no-existe");

    /// <summary>Inicia sesión y devuelve un JWT. Limitado a 5 intentos por minuto por IP.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.NombreUsuario == req.NombreUsuario, ct);

        var ok = BCrypt.Net.BCrypt.Verify(req.Password, usuario?.PasswordHash ?? HashFalso);
        if (usuario is null || !usuario.Activo || !ok)
            return Unauthorized("Usuario o contraseña incorrectos.");

        var (token, expira) = tokens.Crear(usuario);
        return new LoginResponse(token, expira, usuario.NombreUsuario, usuario.Rol);
    }

    /// <summary>
    /// Cambia el usuario y/o la contraseña de quien está autenticado. Exige la contraseña actual.
    /// Tras el cambio hay que volver a iniciar sesión.
    /// </summary>
    [HttpPut("cuenta")]
    [Authorize]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> CambiarCuenta(CambiarCuentaRequest req, CancellationToken ct)
    {
        var nuevoNombre = req.NuevoNombreUsuario?.Trim();
        if (string.IsNullOrEmpty(nuevoNombre) && string.IsNullOrEmpty(req.NuevaPassword))
            return BadRequest("Indique un nuevo usuario y/o una nueva contraseña.");

        if (!int.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.Activo, ct);
        if (usuario is null) return Unauthorized();

        if (!BCrypt.Net.BCrypt.Verify(req.PasswordActual, usuario.PasswordHash))
            return BadRequest("La contraseña actual es incorrecta.");

        if (!string.IsNullOrEmpty(nuevoNombre) && nuevoNombre != usuario.NombreUsuario)
        {
            if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == nuevoNombre, ct))
                return Conflict("Ese nombre de usuario ya existe.");
            usuario.NombreUsuario = nuevoNombre;
        }
        if (!string.IsNullOrEmpty(req.NuevaPassword))
            usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NuevaPassword);

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Crea un usuario (solo Admin).</summary>
    [HttpPost("usuarios")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CrearUsuario(CrearUsuarioRequest req, CancellationToken ct)
    {
        if (!Roles.Todos.Contains(req.Rol))
            return BadRequest($"Rol inválido. Valores permitidos: {string.Join(", ", Roles.Todos)}.");
        if (await db.Usuarios.AnyAsync(u => u.NombreUsuario == req.NombreUsuario, ct))
            return Conflict("El nombre de usuario ya existe.");

        db.Usuarios.Add(new Usuario
        {
            NombreUsuario = req.NombreUsuario,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Rol = req.Rol
        });
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created);
    }
}
