using System.ComponentModel.DataAnnotations;

namespace Cevicheria.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>Clave simétrica HS256. Mínimo 32 caracteres; guardar en user-secrets / variable de entorno.</summary>
    [Required, MinLength(32)] public string Key { get; set; } = string.Empty;
    [Required] public string Issuer { get; set; } = "Cevicheria.Api";
    [Required] public string Audience { get; set; } = "Cevicheria.App";
    [Range(1, 1440)] public int ExpiraEnMinutos { get; set; } = 480; // un turno de trabajo
}
