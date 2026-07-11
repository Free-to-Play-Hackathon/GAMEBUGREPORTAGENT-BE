using System.Security.Claims;
using GameBug.Application.Abstractions.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace GameBug.Infrastructure.Security;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHostEnvironment _environment;

    public CurrentUser(IHttpContextAccessor httpContextAccessor, IHostEnvironment environment)
    {
        _httpContextAccessor = httpContextAccessor;
        _environment = environment;
    }

    public string? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;

            // Return NameIdentifier claim if present
            var nameIdentifier = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(nameIdentifier))
            {
                return nameIdentifier;
            }

            // Explicitly limited to Development; Production never silently authenticates.
            return _environment.IsDevelopment() ? "DevUser" : null;
        }
    }

    public bool IsAuthenticated => !string.IsNullOrEmpty(UserId);
}
