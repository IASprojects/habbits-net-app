using System.Net.Http.Headers;
using System.Net.Http.Json;
using HabitsApp.WebBlazor.Models.Auth;

namespace HabitsApp.WebBlazor.Services;

public sealed class AuthorizingHttpMessageHandler : DelegatingHandler
{
    private readonly TokenStorage _tokenStorage;

    public AuthorizingHttpMessageHandler(TokenStorage tokenStorage)
    {
        _tokenStorage = tokenStorage;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStorage.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}