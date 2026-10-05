namespace PrecastStudio.Core.Geometry;

/// <summary>A closed 1D range along the wall axis, in millimetres.</summary>
public readonly record struct Interval
{
    public Interval(double start, double end)
    {
        Start = Math.Min(start, end);
        End = Math.Max(start, end);
    }

    public double Start { get; }
    public double End { get; }
    public double Length => End - Start;

    public bool ContainsStrictly(double x) => x > Start && x < End;

    public bool Overlaps(Interval other) => Start < other.End && other.Start < End;

    public Interval Expand(double distance) => new(Start - distance, End + distance);

    public Interval Shift(double offset) => new(Start + offset, End + offset);

    /// <summary>Sorts and merges overlapping or touching intervals.</summary>
    public static IReadOnlyList<Interval> Merge(IEnumerable<Interval> intervals)
    {
        var merged = new List<Interval>();
        foreach (var current in intervals.OrderBy(i => i.Start))
        {
            if (merged.Count > 0 && current.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = new Interval(last.Start, Math.Max(last.End, current.End));
            }
            else
            {
                merged.Add(current);
            }
        }
        return merged;
    }

    public override string ToString() => $"[{Start:0}, {End:0}]";
}
