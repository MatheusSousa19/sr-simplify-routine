using System.Text.Json.Serialization;

namespace SrSimplifyRoutine.Web.Services;

public sealed class BotProtection(HttpClient client, IConfiguration config, IWebHostEnvironment environment)
{
    public async Task<bool> VerifyAsync(string token, CancellationToken cancellationToken)
    {
        if (!config.GetValue<bool>("BotProtection:Enabled")) return environment.IsDevelopment();
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;
        try
        {
            using var response = await client.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["secret"] = config["BotProtection:SecretKey"]!, ["response"] = token
                }), cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var result = await response.Content.ReadFromJsonAsync<Verification>(cancellationToken);
            var host = new Uri(config["Application:PublicOrigin"]!).Host;
            return result is { Success: true, Action: "register" } && string.Equals(result.Hostname, host, StringComparison.OrdinalIgnoreCase);
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    private sealed record Verification(
        [property: JsonPropertyName("success")] bool Success,
        [property: JsonPropertyName("hostname")] string? Hostname,
        [property: JsonPropertyName("action")] string? Action);
}
