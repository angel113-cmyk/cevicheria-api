using System.Text;
using System.Threading.RateLimiting;
using Cevicheria.Api.Auth;
using Cevicheria.Api.Data;
using Cevicheria.Api.Models;
using Cevicheria.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Render expone el puerto en PORT y escucha detrás de un proxy TLS.
var puerto = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(puerto)) builder.WebHost.UseUrls($"http://0.0.0.0:{puerto}");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(ResolverConexion(builder.Configuration)));

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

// --- JWT: la configuración se valida al arrancar; sin clave válida la app no inicia. ---
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<ReporteService>();
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community; // gratis para negocios < USD 1M de ingresos
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

await AplicarMigracionesAsync(app);
await SembrarAdminAsync(app);

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/openapi/v1.json", "Cevichería API v1"));
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapGet("/health", () => Results.Ok("ok")).ExcludeFromDescription();

// El TLS lo termina el proxy de Render; no se fuerza redirección dentro del contenedor.
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

/// <summary>Usa DATABASE_URL (postgres://usuario:clave@host:puerto/bd, formato de Render) si existe; si no, DefaultConnection.</summary>
static string? ResolverConexion(IConfiguration config)
{
    var url = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(url)) return config.GetConnectionString("DefaultConnection");

    var uri = new Uri(url);
    var credenciales = uri.UserInfo.Split(':', 2);
    return new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.TrimStart('/'),
        Username = Uri.UnescapeDataString(credenciales[0]),
        Password = credenciales.Length > 1 ? Uri.UnescapeDataString(credenciales[1]) : null,
        SslMode = Npgsql.SslMode.Require
    }.ConnectionString;
}

/// <summary>Aplica las migraciones pendientes al arrancar (el contenedor no tiene 'dotnet ef').</summary>
static async Task AplicarMigracionesAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
