using System.Security.Claims;
using System.Text;
using Cevicheria.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Cevicheria.Api.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _o = options.Value;

    public (string Token, DateTime ExpiraUtc) Crear(Usuario u)
    {
        var expira = DateTime.UtcNow.AddMinutes(_o.ExpiraEnMinutos);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _o.Issuer,
            Audience = _o.Audience,
            Expires = expira,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("name", u.NombreUsuario),
                new Claim("role", u.Rol)
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_o.Key)), SecurityAlgorithms.HmacSha256)
        };
        return (new JsonWebTokenHandler().CreateToken(descriptor), expira);
    }
}
