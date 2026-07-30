using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace King_of_the_Garbage_Hill.Localization;

public enum MessageVisibility
{
    Public,
    Owner,
    Server,
}

/// <summary>
/// Loads the shared, product-scoped localization schema used by C# and TypeScript.
/// The former arbitrary-string translator remains a legacy compatibility boundary;
/// new authored messages resolve through stable keys here.
/// </summary>
public sealed class MessageCatalog
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{(?<name>[A-Za-z][A-Za-z0-9_.-]*)\}", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, MessageDefinition> _messages;

    public MessageCatalog(string catalogDirectory = null)
    {
        var directory = catalogDirectory ?? FindCatalogDirectory();
        _messages = Load(directory);
    }

    public IReadOnlyCollection<string> Keys => _messages.Keys.ToArray();

    public string Render(LocalizedMessage message, string language)
    {
        if (!_messages.TryGetValue(message.Key, out var definition))
            throw new KeyNotFoundException($"Unknown localization key '{message.Key}'.");

        var template = string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)
            ? definition.En
            : definition.Ru;
        var required = Placeholders(template);
        var missing = required
            .Where(name => !message.Arguments.ContainsKey(name))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException(
                $"Localization key '{message.Key}' is missing arguments: {string.Join(", ", missing)}.");

        return PlaceholderPattern.Replace(template, match =>
            message.Arguments[match.Groups["name"].Value]);
    }

    public LocalizedText ResolveBoth(LocalizedMessage message) =>
        new(Render(message, "ru"), Render(message, "en"));

    public MessageVisibility VisibilityOf(string key) =>
        _messages.TryGetValue(key, out var definition)
            ? definition.Visibility
            : throw new KeyNotFoundException($"Unknown localization key '{key}'.");

    private static IReadOnlyDictionary<string, MessageDefinition> Load(string directory)
    {
        var messages = new Dictionary<string, MessageDefinition>(StringComparer.Ordinal);
        var errors = new List<string>();
        var files = Directory.GetFiles(directory, "*.messages.json", SearchOption.TopDirectoryOnly);
        if (files.Length == 0)
            throw new InvalidDataException($"No *.messages.json catalogs were found in {directory}.");

        foreach (var file in files.OrderBy(path => path, StringComparer.Ordinal))
        {
            ProductCatalog catalog;
            try
            {
                var json = File.ReadAllText(file);
                using var document = JsonDocument.Parse(json);
                var duplicateProperties = new List<string>();
                FindDuplicateProperties(document.RootElement, "$", duplicateProperties);
                errors.AddRange(duplicateProperties.Select(path =>
                    $"{Path.GetFileName(file)}: duplicate JSON property '{path}'"));
                catalog = JsonSerializer.Deserialize<ProductCatalog>(
                    json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (Exception exception)
            {
                errors.Add($"{Path.GetFileName(file)}: {exception.Message}");
                continue;
            }

            if (catalog == null
                || string.IsNullOrWhiteSpace(catalog.Product)
                || catalog.Messages == null)
            {
                errors.Add($"{Path.GetFileName(file)}: product and messages are required");
                continue;
            }

            foreach (var (key, source) in catalog.Messages)
            {
                if (source == null)
                {
                    errors.Add($"{key}: message definition is required");
                    continue;
                }
                if (!key.StartsWith(catalog.Product + ".", StringComparison.Ordinal))
                    errors.Add($"{key}: key must start with '{catalog.Product}.'");
                if (string.IsNullOrWhiteSpace(source.Ru) || string.IsNullOrWhiteSpace(source.En))
                    errors.Add($"{key}: both ru and en are required");

                var ruPlaceholders = Placeholders(source.Ru);
                var enPlaceholders = Placeholders(source.En);
                if (!ruPlaceholders.SequenceEqual(enPlaceholders))
                    errors.Add(
                        $"{key}: placeholder mismatch (ru: {string.Join(", ", ruPlaceholders)}; " +
                        $"en: {string.Join(", ", enPlaceholders)})");

                if (!Enum.TryParse<MessageVisibility>(source.Visibility, true, out var visibility))
                {
                    errors.Add($"{key}: visibility must be public, owner, or server");
                    continue;
                }

                if (!messages.TryAdd(key, new MessageDefinition(source.Ru, source.En, visibility)))
                    errors.Add($"{key}: duplicate key");
            }
        }

        if (errors.Count > 0)
            throw new InvalidDataException(
                "Invalid structured localization catalog:\n- " + string.Join("\n- ", errors));
        return messages;
    }

    private static string[] Placeholders(string value) =>
        PlaceholderPattern.Matches(value)
            .Cast<Match>()
            .Select(match => match.Groups["name"].Value)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static void FindDuplicateProperties(
        JsonElement element,
        string path,
        ICollection<string> duplicates)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                var propertyPath = $"{path}.{property.Name}";
                if (!names.Add(property.Name))
                    duplicates.Add(propertyPath);
                FindDuplicateProperties(property.Value, propertyPath, duplicates);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                FindDuplicateProperties(item, $"{path}[{index}]", duplicates);
                index++;
            }
        }
    }

    private static string FindCatalogDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Localization"),
            Path.Combine(Directory.GetCurrentDirectory(), "Localization"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Localization"),
        };
        return candidates.FirstOrDefault(Directory.Exists)
               ?? throw new DirectoryNotFoundException(
                   "The shared Localization directory was not found.");
    }

    private sealed record MessageDefinition(string Ru, string En, MessageVisibility Visibility);

    private sealed class ProductCatalog
    {
        public string Product { get; set; } = "";
        public Dictionary<string, SourceMessage> Messages { get; set; } =
            new(StringComparer.Ordinal);
    }

    private sealed class SourceMessage
    {
        public string Visibility { get; set; } = "";
        public string Ru { get; set; } = "";
        public string En { get; set; } = "";
    }
}
