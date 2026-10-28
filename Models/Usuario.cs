namespace Cevicheria.Api.Models;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Mozo = "Mozo";

    public static readonly string[] Todos = [Admin, Mozo];
}

public class Usuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    /// <summary>Hash BCrypt; nunca se almacena ni se devuelve la contraseña en claro.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string Rol { get; set; } = Roles.Mozo;
    public bool Activo { get; set; } = true;
}
