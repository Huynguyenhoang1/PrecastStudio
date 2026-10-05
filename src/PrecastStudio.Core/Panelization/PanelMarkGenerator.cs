namespace PrecastStudio.Core.Panelization;

public sealed record MarkedPanel(Panel Panel, string Mark);

/// <summary>
/// Gives identical panels the same mark so the factory can reuse moulds.
/// Two panels are identical when width and opening positions match within <see cref="RoundingMm"/>.
/// </summary>
public sealed class PanelMarkGenerator
{
    public double RoundingMm { get; init; } = 1;

    public IReadOnlyList<MarkedPanel> Assign(PanelLayout layout)
    {
        var marksByShape = new Dictionary<string, string>();
        var result = new List<MarkedPanel>();
        foreach (var panel in layout.Panels)
        {
            var key = ShapeKey(panel);
            if (!marksByShape.TryGetValue(key, out var mark))
            {
                mark = $"{layout.Options.MarkPrefix}-{marksByShape.Count + 1:00}";
                marksByShape[key] = mark;
            }
            result.Add(new MarkedPanel(panel, mark));
        }
        return result;
    }

    private string ShapeKey(Panel panel)
    {
        var openings = panel.Openings.Select(o => $"{Round(o.Start)}-{Round(o.End)}");
        return $"{Round(panel.Width)}|{string.Join(",", openings)}";
    }

    private long Round(double value) => (long)Math.Round(value / RoundingMm);
}
