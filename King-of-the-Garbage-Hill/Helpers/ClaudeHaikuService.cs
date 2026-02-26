using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace King_of_the_Garbage_Hill.Helpers;

/// <summary>
/// Lightweight Claude Haiku API caller registered in the Lamar container.
/// Used by game logic (CharacterPassives) for short AI-generated text.
/// </summary>
public class ClaudeHaikuService : IServiceSingleton
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-haiku-4-5-20251001";

    public ClaudeHaikuService(HttpClient httpClient, Config config)
    {
        _httpClient = httpClient;
        _apiKey = config.AnthropicApiKey ?? "";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Generates a Geralt-style one-liner hint about a target character.
    /// Returns null on failure (caller should fall back to static hints).
    /// </summary>
    public async Task<string> GenerateWitcherHintAsync(string characterName, string description, string monsterType)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            return null;

        var prompt = $"Ты - Геральт из ёбанной Ривии. Ты медитируешь и чувствуешь след монстра.\n" +
                     $"Монстр: {characterName}" +
                     (string.IsNullOrWhiteSpace(description) ? "" : $" ({description})") +
                     $"\nТип монстра: {monsterType}\n" +
                     $"Напиши одну-две короткую фразу (максимум 15 слов) в стиле ведьмачьего расследования - что ты нашёл на месте." +
                     $"Без кавычек, без пояснений. Только фраза/две. Никогда не упоминай имя персонажа в фразе, но нужена подсказка, чтобы можно было догадаться имя Монста. Шутки приветствуются.";

        try
        {
            var requestBody = new
            {
                model = Model,
                max_tokens = 100,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            request.Headers.Add("x-api-key", _apiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await _httpClient.SendAsync(request, cts.Token);
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = JsonDocument.Parse(responseBody);
            var text = doc.RootElement
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString()
                ?.Trim();
            
            Console.WriteLine($"[WitcherHint] Generated hint for {characterName}: {text}");

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WitcherHint] Error: {ex.Message}");
            return null;
        }
    }
}
