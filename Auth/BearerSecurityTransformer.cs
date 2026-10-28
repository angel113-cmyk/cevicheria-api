using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Cevicheria.Api.Auth;

/// <summary>Declara el esquema Bearer en el documento OpenAPI para que Swagger UI muestre el botón "Authorize".</summary>
public class BearerSecurityTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
        {
            ["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Pegue el token obtenido en POST /api/auth/login (sin el prefijo 'Bearer')."
            }
        };
        document.Security =
        [
            new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }
        ];
        return Task.CompletedTask;
    }
}
