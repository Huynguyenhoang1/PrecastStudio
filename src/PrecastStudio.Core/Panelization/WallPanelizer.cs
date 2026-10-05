using PrecastStudio.Core.Geometry;

namespace PrecastStudio.Core.Panelization;

/// <summary>
/// Splits a straight wall into precast panels.
/// Joints are placed so that every panel is between the min and max width, panels are as equal
/// as possible, and no joint falls inside an opening (plus clearance).
/// </summary>
public sealed class WallPanelizer
{
    private const double Tolerance = 1e-6;

    public PanelLayout Panelize(double wallLength, IEnumerable<Interval> openings, PanelizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(openings);
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        if (wallLength <= 0) throw new ArgumentOutOfRangeException(nameof(wallLength), "Wall length must be positive.");
        if (wallLength < options.MinPanelWidth - Tolerance)
            throw new PanelizationException($"Wall length {wallLength:0} mm is shorter than the min panel width {options.MinPanelWidth:0} mm.");

        var openingList = openings.ToList();
        var noJointZones = Interval.Merge(openingList.Select(o => o.Expand(options.OpeningClearance)));

        var joints = new List<double>();
        var position = 0.0;
        while (wallLength - position > options.MaxPanelWidth + Tolerance)
        {
            var joint = ChooseNextJoint(position, wallLength, noJointZones, options)
                ?? throw new PanelizationException(
                    $"No valid joint between {position + options.MinPanelWidth:0} and {position + options.MaxPanelWidth:0} mm: " +
                    "openings and clearance cover the whole range. Increase the max panel width or reduce the clearance.");
            joints.Add(joint);
            position = joint;
        }

        return new PanelLayout(wallLength, joints, BuildPanels(wallLength, joints, openingList, options), options);
    }

    private static double? ChooseNextJoint(double position, double wallLength, IReadOnlyList<Interval> noJointZones, PanelizationOptions o)
    {
        var remaining = wallLength - position;
        var panelsLeft = (int)Math.Ceiling(remaining / o.MaxPanelWidth - Tolerance);
        var ideal = position + remaining / panelsLeft;

        // The ideal (equal-width) position first; if it is blocked, the edges of the blocking zones
        // and the range limits are the only other positions worth trying.
        var candidates = new List<double> { ideal, position + o.MinPanelWidth, position + o.MaxPanelWidth };
        foreach (var zone in noJointZones)
        {
            candidates.Add(zone.Start);
            candidates.Add(zone.End);
        }

        return candidates
            .Where(x => IsValidJoint(x, position, wallLength, noJointZones, o))
            .OrderBy(x => Math.Abs(x - ideal))
            .Select(x => (double?)x)
            .FirstOrDefault();
    }

    private static bool IsValidJoint(double x, double position, double wallLength, IReadOnlyList<Interval> noJointZones, PanelizationOptions o)
    {
        var panelWidth = x - position;
        var rest = wallLength - x;
        return panelWidth >= o.MinPanelWidth - Tolerance
            && panelWidth <= o.MaxPanelWidth + Tolerance
            && rest >= o.MinPanelWidth - Tolerance
            && !noJointZones.Any(z => z.ContainsStrictly(x));
    }

    private static List<Panel> BuildPanels(double wallLength, IReadOnlyList<double> joints, IReadOnlyList<Interval> openings, PanelizationOptions o)
    {
        var bounds = new List<double> { 0 };
        bounds.AddRange(joints);
        bounds.Add(wallLength);

        var halfJoint = o.JointWidth / 2;
        var panels = new List<Panel>();
        for (var i = 0; i < bounds.Count - 1; i++)
        {
            var start = bounds[i] + (i > 0 ? halfJoint : 0);
            var end = bounds[i + 1] - (i < bounds.Count - 2 ? halfJoint : 0);
            var span = new Interval(start, end);
            var inside = openings.Where(span.Overlaps).Select(op => op.Shift(-start)).ToList();
            panels.Add(new Panel(i + 1, start, end, inside));
        }
        return panels;
    }
}
