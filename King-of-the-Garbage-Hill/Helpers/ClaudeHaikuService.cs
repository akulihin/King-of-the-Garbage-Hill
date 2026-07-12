using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    /// <summary>Set true in headless simulation (--sim) so mass bot games never spend API credits.</summary>
    public static bool Disabled { get; set; }

    public ClaudeHaikuService(HttpClient httpClient, Config config)
    {
        _httpClient = httpClient;
        _apiKey = config.AnthropicApiKey ?? "";
    }

    public Task InitializeAsync() => Task.CompletedTask;

    /// <summary>
    /// Generates paired Geralt-style one-liner hints about a target character.
    /// Returns null on failure or malformed output (caller should fall back to static hints).
    /// </summary>
    public async Task<BilingualGeneratedText> GenerateWitcherHintPairAsync(
        string characterName, string description, string monsterType)
    {
        if (Disabled || string.IsNullOrWhiteSpace(_apiKey))
            return null;

        var prompt = $"Ты — Геральт из ёбанной Ривии. Ты медитируешь и чувствуешь след монстра.\n" +
                     $"Монстр: {characterName}" +
                     (string.IsNullOrWhiteSpace(description) ? "" : $" ({description})") +
                     $"\nТип монстра: {monsterType}\n" +
                     "Напиши одну короткую улику ведьмачьего расследования (максимум 15 слов) в двух адаптированных версиях. " +
                     "Обе версии должны описывать одну и ту же находку и шутку. Не упоминай имя персонажа, но оставь догадку. " +
                     "Русский текст помести строго внутрь <ru>...</ru>. Затем напиши естественную, не дословную английскую адаптацию " +
                     "строго внутри <en>...</en>. Никаких других тегов, кавычек или пояснений.";

        try
        {
            var requestBody = new
            {
                model = Model,
                max_tokens = 180,
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

            if (!BilingualGeneratedTextParser.TryParse(text, out var generated))
            {
                Console.WriteLine($"[WitcherHint] Invalid bilingual output for {characterName}: {text}");
                return null;
            }

            var russian = BilingualGeneratedTextParser.CollapseToOneLine(generated.Russian);
            var english = BilingualGeneratedTextParser.CollapseToOneLine(generated.English);
            if (russian.Length > 300 || english.Length > 300 ||
                !Regex.IsMatch(russian, "[А-Яа-яЁё]") || Regex.IsMatch(english, "[А-Яа-яЁё]"))
            {
                Console.WriteLine($"[WitcherHint] Rejected bilingual output for {characterName}: {text}");
                return null;
            }

            Console.WriteLine($"[WitcherHint] Generated paired hint for {characterName}: RU={russian} EN={english}");
            return new BilingualGeneratedText(russian, english);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WitcherHint] Error: {ex.Message}");
            return null;
        }
    }
}
