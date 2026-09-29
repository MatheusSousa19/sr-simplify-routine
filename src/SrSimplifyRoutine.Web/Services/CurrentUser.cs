using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace SrSimplifyRoutine.Web.Services;

public interface ICurrentUser { Task<string> GetIdAsync(); }

public sealed class CurrentUser(AuthenticationStateProvider authentication) : ICurrentUser
{
    public async Task<string> GetIdAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (user.Identity?.IsAuthenticated != true)
            throw new UnauthorizedAccessException("Please sign in.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Please sign in again.");
    }
}
