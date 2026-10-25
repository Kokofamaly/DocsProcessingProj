using System.Security.Claims;

namespace DocsProcessingProj.Api.Endpoints;

public static class EndpointsHelper
{
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}