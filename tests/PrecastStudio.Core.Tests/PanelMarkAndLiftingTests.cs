using PrecastStudio.Core.Geometry;
using PrecastStudio.Core.Lifting;
using PrecastStudio.Core.Panelization;

namespace PrecastStudio.Core.Tests;

public class PanelMarkGeneratorTests
{
    [Fact]
    public void Identical_panels_share_a_mark()
    {
        var options = new PanelizationOptions { MarkPrefix = "PW" };
        var layout = new WallPanelizer().Panelize(9000, [], options);

        var marks = new PanelMarkGenerator().Assign(layout).Select(m => m.Mark).ToList();

        // Two end panels are identical, the middle one is narrower (joint on both sides).
        Assert.Equal(["PW-01", "PW-02", "PW-01"], marks);
    }

    [Fact]
    public void Panel_with_an_opening_gets_its_own_mark()
    {
        var layout = new WallPanelizer().Panelize(9000, [new Interval(800, 1700)], new PanelizationOptions());

        var marks = new PanelMarkGenerator().Assign(layout).Select(m => m.Mark).ToList();

        Assert.Equal(3, marks.Distinct().Count());
    }
}

public class LiftingAnchorCalculatorTests
{
    [Fact]
    public void Light_panel_uses_two_anchors_at_0_207_of_the_width()
    {
        // 3000 x 3000 x 200 = 1.8 m3 = 4500 kg; x1.3 = 5850 kg, under 2 x 5000 kg.
        var design = LiftingAnchorCalculator.Design(3000, 3000, 200, 0, anchorCapacityKg: 5000);

        Assert.Equal(4500, design.WeightKg, 3);
        Assert.Equal(2, design.AnchorCount);
        Assert.Equal(621, design.AnchorPositions[0], 3);
        Assert.Equal(2379, design.AnchorPositions[1], 3);
    }

    [Fact]
    public void Heavy_panel_gets_an_even_number_of_evenly_spaced_anchors()
    {
        var design = LiftingAnchorCalculator.Design(3000, 3000, 200, 0, anchorCapacityKg: 2000);

        Assert.Equal(4, design.AnchorCount);
        Assert.Equal([375, 1125, 1875, 2625], design.AnchorPositions);
    }

    [Fact]
    public void Openings_reduce_the_weight()
    {
        var solid = LiftingAnchorCalculator.Design(3000, 3000, 200, 0, 5000);
        var withWindow = LiftingAnchorCalculator.Design(3000, 3000, 200, 1000 * 1500, 5000);

        Assert.True(withWindow.WeightKg < solid.WeightKg);
    }
}
