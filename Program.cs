using System.Text;
using System.Threading.RateLimiting;
using Cevicheria.Api.Auth;
using Cevicheria.Api.Data;
using Cevicheria.Api.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- JWT: la configuración se valida al arrancar; sin clave válida la app no inicia. ---
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton<TokenService>();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

// Freno a fuerza bruta en el login: 5 intentos por minuto por IP.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers();
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecurityTransformer>());

// La app MAUI en Android no envía cabecera Origin del navegador, pero se habilita CORS
// para clientes web/pruebas. Restringir orígenes en producción si hay un cliente web.
builder.Services.AddCors(o => o.AddPolicy("Mobile", p => p
    .AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

await SembrarAdminAsync(app);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Cevichería API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

// Sin redirección HTTPS forzada en desarrollo: el emulador Android usa http://10.0.2.2:5134
if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseCors("Mobile");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

/// <summary>Crea el primer Admin si no hay usuarios, con credenciales de Seed:AdminUser / Seed:AdminPassword.</summary>
static async Task SembrarAdminAsync(WebApplication app)
{
    var nombre = app.Configuration["Seed:AdminUser"];
    var password = app.Configuration["Seed:AdminPassword"];
    if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(password)) return;

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        if (await db.Usuarios.AnyAsync()) return;
    }
    catch (Exception ex) // tabla inexistente: falta aplicar la migración
    {
        app.Logger.LogWarning("No se pudo sembrar el admin (¿falta 'dotnet ef database update'?): {Mensaje}", ex.Message);
        return;
    }

    db.Usuarios.Add(new Usuario
    {
        NombreUsuario = nombre,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Rol = Roles.Admin
    });
    await db.SaveChangesAsync();
    app.Logger.LogInformation("Usuario administrador '{Usuario}' creado.", nombre);
}
