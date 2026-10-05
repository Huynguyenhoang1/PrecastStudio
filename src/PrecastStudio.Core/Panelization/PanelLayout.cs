using PrecastStudio.Core.Geometry;

namespace PrecastStudio.Core.Panelization;

/// <summary>One precast panel. Coordinates are measured from the wall start, in millimetres.</summary>
/// <param name="Openings">Openings inside this panel, relative to the panel's own left edge.</param>
public sealed record Panel(int Index, double Start, double End, IReadOnlyList<Interval> Openings)
{
    public double Width => End - Start;
}

/// <param name="Joints">Joint centre lines measured from the wall start.</param>
public sealed record PanelLayout(
    double WallLength,
    IReadOnlyList<double> Joints,
    IReadOnlyList<Panel> Panels,
    PanelizationOptions Options);

public sealed class PanelizationException(string message) : Exception(message);
