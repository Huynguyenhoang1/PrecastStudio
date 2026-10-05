namespace PrecastStudio.Core.Panelization;

/// <summary>Rules used to split a wall into precast panels. All lengths in millimetres.</summary>
public sealed record PanelizationOptions
{
    /// <summary>Largest panel that can be cast, transported and erected.</summary>
    public double MaxPanelWidth { get; init; } = 3000;

    /// <summary>Smallest panel worth producing (avoids slivers at wall ends).</summary>
    public double MinPanelWidth { get; init; } = 600;

    /// <summary>Gap between adjacent panels.</summary>
    public double JointWidth { get; init; } = 20;

    /// <summary>Minimum distance from a joint to the edge of a door or window opening.</summary>
    public double OpeningClearance { get; init; } = 150;

    public string MarkPrefix { get; init; } = "W";

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (MaxPanelWidth <= 0) errors.Add("Max panel width must be positive.");
        if (MinPanelWidth <= 0) errors.Add("Min panel width must be positive.");
        if (MinPanelWidth * 2 > MaxPanelWidth)
            errors.Add("Max panel width must be at least twice the min panel width, otherwise some wall lengths cannot be split.");
        if (JointWidth < 0) errors.Add("Joint width cannot be negative.");
        if (JointWidth >= MinPanelWidth) errors.Add("Joint width must be smaller than the min panel width.");
        if (OpeningClearance < 0) errors.Add("Opening clearance cannot be negative.");
        if (string.IsNullOrWhiteSpace(MarkPrefix)) errors.Add("Mark prefix is required.");
        return errors;
    }

    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
    }
}
