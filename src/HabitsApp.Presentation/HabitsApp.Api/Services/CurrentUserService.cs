using System.Security.Claims;
using HabitsApp.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Http;

namespace HabitsApp.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return sub is not null && Guid.TryParse(sub, out var userId) ? userId : null;
        }
    }
}