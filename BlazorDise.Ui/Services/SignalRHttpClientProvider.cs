using BlazorDise.Shared;
using Microsoft.AspNetCore.Components;

namespace BlazorDise.Ui.Services;

public class SignalRHttpClientProvider(IHttpClientFactory httpClientFactory, NavigationManager navigationManager)
{
    public HttpClient GetClient()
    {
        var client = httpClientFactory.CreateClient(Constants.SignalRHttpName);
        if (client.BaseAddress == null)
            client.BaseAddress = new Uri(navigationManager.BaseUri);
        return client;
    }
}