using PrecastStudio.Core.Panelization;

namespace PrecastStudio.Core.Lifting;

/// <param name="AnchorPositions">Anchor positions along the top edge, from the panel's left edge (mm).</param>
public sealed record LiftingDesign(double WeightKg, double DesignLoadKg, int AnchorCount, IReadOnlyList<double> AnchorPositions);

/// <summary>
/// Preliminary lifting anchor layout for a flat wall panel.
/// This is a detailing aid, not a substitute for the anchor supplier's design or an engineer's check.
/// </summary>
public static class LiftingAnchorCalculator
{
    public const double ConcreteDensityKgPerM3 = 2500;

    /// <summary>Two-point lift: anchors at 0.207 L from each end give equal hogging and sagging moments.</summary>
    public const double TwoPointLiftRatio = 0.207;

    public static LiftingDesign Design(
        double widthMm,
        double heightMm,
        double thicknessMm,
        double openingAreaMm2,
        double anchorCapacityKg,
        double dynamicFactor = 1.3)
    {
        if (widthMm <= 0 || heightMm <= 0 || thicknessMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(widthMm), "Panel dimensions must be positive.");
        if (anchorCapacityKg <= 0) throw new ArgumentOutOfRangeException(nameof(anchorCapacityKg));
        if (openingAreaMm2 < 0 || openingAreaMm2 >= widthMm * heightMm)
            throw new ArgumentOutOfRangeException(nameof(openingAreaMm2), "Opening area must be smaller than the panel face.");

        var volumeM3 = (widthMm * heightMm - openingAreaMm2) * thicknessMm * 1e-9;
        var weight = volumeM3 * ConcreteDensityKgPerM3;
        var designLoad = weight * dynamicFactor;

        var count = Math.Max(2, (int)Math.Ceiling(designLoad / anchorCapacityKg));
        if (count % 2 == 1) count++; // keep the layout symmetric

        return new LiftingDesign(weight, designLoad, count, Positions(widthMm, count));
    }

    /// <param name="openingHeightMm">Height used for every opening in the panel (e.g. 2100 for doors).</param>
    public static LiftingDesign DesignPanel(Panel panel, double heightMm, double thicknessMm, double openingHeightMm, double anchorCapacityKg) =>
        Design(panel.Width, heightMm, thicknessMm, panel.Openings.Sum(o => o.Length) * Math.Min(openingHeightMm, heightMm), anchorCapacityKg);

    private static IReadOnlyList<double> Positions(double width, int count)
    {
        if (count == 2)
            return [width * TwoPointLiftRatio, width * (1 - TwoPointLiftRatio)];

        // Equal tributary width per anchor.
        return Enumerable.Range(0, count).Select(i => width * (2 * i + 1) / (2.0 * count)).ToList();
    }
}
