using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace OneNest.API.Swagger;

public class SecurityRequirementsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument document, DocumentFilterContext context)
    {
        var authorizedOperationIds = new HashSet<string>();

        foreach (var apiDescription in context.ApiDescriptions)
        {
            var actionDescriptor = apiDescription.ActionDescriptor;

            var hasAuthorize = actionDescriptor.EndpointMetadata
                .OfType<AuthorizeAttribute>().Any();

            var allowAnonymous = actionDescriptor.EndpointMetadata
                .OfType<AllowAnonymousAttribute>().Any();

            if (hasAuthorize && !allowAnonymous)
            {
                var key = $"/{apiDescription.RelativePath?.TrimStart('/')}|{apiDescription.HttpMethod?.ToUpperInvariant()}";
                authorizedOperationIds.Add(key);
            }
        }

        if (document.Paths is null)
            return;

        foreach (var path in document.Paths)
        {
            if (path.Value.Operations is null)
                continue;

            foreach (var operation in path.Value.Operations)
            {
                var key = $"{path.Key}|{operation.Key.ToString().ToUpperInvariant()}";

                if (!authorizedOperationIds.Contains(key))
                    continue;

                operation.Value.Security = new List<OpenApiSecurityRequirement>
                {
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecuritySchemeReference("Bearer", document),
                            new List<string>()
                        }
                    }
                };
            }
        }
    }
}
