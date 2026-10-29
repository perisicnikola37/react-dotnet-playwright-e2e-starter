using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace E2eStarter.AspNetCore;

// Short-lived random tokens that authorize the dashboard iframe. An iframe cannot send an Authorization header,
// so the authenticated app asks for a token (POST dashboard-token) and puts it in the iframe URL instead.
public sealed class DashboardTokens(IOptions<AutomatedTestsOptions> options, TimeProvider time)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new(StringComparer.Ordinal);

    public DashboardToken Issue()
    {
        var now = time.GetUtcNow();
        foreach (var (key, expiresAt) in _tokens)
        {
            if (expiresAt <= now)
            {
                _tokens.TryRemove(key, out _);
            }
        }

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expires = now + options.Value.DashboardTokenLifetime;
        _tokens[token] = expires;
        return new DashboardToken(token, expires);
    }

    public bool IsValid(string? token) =>
        token is { Length: 43 } && _tokens.TryGetValue(token, out var expiresAt) && expiresAt > time.GetUtcNow();

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
