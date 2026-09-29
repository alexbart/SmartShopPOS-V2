using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SmartShopPOS.Api.Branches;

public sealed class CookieAuthenticationOperationFilter : IOperationFilter
{
    public const string AuthenticationCookieScheme = "authenticationCookie";
    public const string SessionCookieScheme = "sessionCookie";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(AuthenticationCookieScheme, context.Document)] = [],
            [new OpenApiSecuritySchemeReference(SessionCookieScheme, context.Document)] = []
        });
    }
}