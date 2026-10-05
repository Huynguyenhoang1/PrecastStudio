using System.Globalization;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using PrecastStudio.Core.Panelization;

namespace PrecastStudio.AI;

/// <summary>
/// Uses Claude with structured outputs, so the reply is always JSON matching <see cref="OptionsSchema"/>.
/// The model only proposes values; <see cref="PanelizationOptions.Validate"/> stays the source of truth.
/// </summary>
public sealed class ClaudePanelCommandInterpreter(AnthropicClient client) : IPanelCommandInterpreter
{
    public const string ModelId = "claude-opus-5-5";

    private const string SystemPrompt = """
        You configure a precast concrete wall panelization tool inside Autodesk Revit.
        The user writes in Vietnamese or English. Read their instruction and return the full set of options:
        start from the current values and change only what the instruction asks for.
        All lengths are in millimetres; convert metres or centimetres ("2.4m", "240cm") to millimetres.
        "tấm" = panel, "khe"/"mạch" = joint, "cửa" = opening, "khoảng cách tới cửa" = opening clearance, "ký hiệu" = mark prefix.
        In "explanation", tell the user in their own language, in one short sentence, what you changed.
        If the instruction is unrelated to these options, return the current values unchanged and say so in "explanation".
        """;

    private static readonly Dictionary<string, JsonElement> OptionsSchema = new()
    {
        ["type"] = JsonSerializer.SerializeToElement("object"),
        ["properties"] = JsonSerializer.SerializeToElement(new
        {
            maxPanelWidthMm = new { type = "number" },
            minPanelWidthMm = new { type = "number" },
            jointWidthMm = new { type = "number" },
            openingClearanceMm = new { type = "number" },
            markPrefix = new { type = "string" },
            explanation = new { type = "string" },
        }),
        ["required"] = JsonSerializer.SerializeToElement(new[]
        {
            "maxPanelWidthMm", "minPanelWidthMm", "jointWidthMm", "openingClearanceMm", "markPrefix", "explanation",
        }),
        ["additionalProperties"] = JsonSerializer.SerializeToElement(false),
    };

    /// <summary>Returns null when no ANTHROPIC_API_KEY is configured, so the UI can hide the AI box.</summary>
    public static ClaudePanelCommandInterpreter? TryCreateFromEnvironment() =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"))
            ? null
            : new ClaudePanelCommandInterpreter(new AnthropicClient());

    public async Task<PanelCommandResult> InterpretAsync(string instruction, PanelizationOptions current, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(instruction)) throw new ArgumentException("Instruction is empty.", nameof(instruction));

        var response = await client.Messages.Create(new MessageCreateParams
        {
            Model = ModelId,
            MaxTokens = 2048,
            System = SystemPrompt,
            OutputConfig = new OutputConfig
            {
                Effort = Effort.Low,
                Format = new JsonOutputFormat { Schema = OptionsSchema },
            },
            Messages =
            [
                new() { Role = Role.User, Content = BuildUserMessage(instruction, current) },
            ],
        }, cancellationToken);

        if (response.StopReason == "refusal")
            throw new PanelCommandException("The AI declined this instruction. Please rephrase it.");

        var json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        return Parse(json, current);
    }

    private static string BuildUserMessage(string instruction, PanelizationOptions current) =>
        string.Create(CultureInfo.InvariantCulture, $"""
            <current_options>
            maxPanelWidthMm: {current.MaxPanelWidth}
            minPanelWidthMm: {current.MinPanelWidth}
            jointWidthMm: {current.JointWidth}
            openingClearanceMm: {current.OpeningClearance}
            markPrefix: {current.MarkPrefix}
            </current_options>

            <instruction>
            {instruction}
            </instruction>
            """);

    /// <summary>Parses and validates the model's JSON. Public so it can be unit-tested without network calls.</summary>
    public static PanelCommandResult Parse(string json, PanelizationOptions current)
    {
        PanelizationOptions options;
        string explanation;
        try
        {
            var root = JsonDocument.Parse(json).RootElement;
            options = current with
            {
                MaxPanelWidth = root.GetProperty("maxPanelWidthMm").GetDouble(),
                MinPanelWidth = root.GetProperty("minPanelWidthMm").GetDouble(),
                JointWidth = root.GetProperty("jointWidthMm").GetDouble(),
                OpeningClearance = root.GetProperty("openingClearanceMm").GetDouble(),
                MarkPrefix = root.GetProperty("markPrefix").GetString() ?? current.MarkPrefix,
            };
            explanation = root.GetProperty("explanation").GetString() ?? string.Empty;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new PanelCommandException("The AI returned an unreadable answer. Please try again.");
        }

        var errors = options.Validate();
        if (errors.Count > 0)
            throw new PanelCommandException("The requested values are not valid: " + string.Join(" ", errors));

        return new PanelCommandResult(options, explanation);
    }
}
