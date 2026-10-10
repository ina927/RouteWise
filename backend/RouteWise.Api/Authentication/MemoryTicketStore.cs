using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace RouteWise.Api.Authentication;

// A single-server session store. Restarting the API invalidates existing sessions.
public sealed class MemoryTicketStore : ITicketStore, IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        cache.Set(key, TicketSerializer.Default.Serialize(ticket),
            ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8));
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var ticket = cache.TryGetValue(key, out byte[]? bytes) && bytes is not null
            ? TicketSerializer.Default.Deserialize(bytes)
            : null;
        return Task.FromResult(ticket);
    }

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }

    public void Dispose() => cache.Dispose();
}
