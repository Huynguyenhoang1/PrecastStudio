using PrecastStudio.Core.Geometry;
using PrecastStudio.Core.Panelization;

namespace PrecastStudio.Core.Tests;

public class WallPanelizerTests
{
    private readonly WallPanelizer _panelizer = new();
    private static readonly PanelizationOptions Default = new() { MaxPanelWidth = 3000, MinPanelWidth = 600, JointWidth = 20, OpeningClearance = 150 };

    [Fact]
    public void Short_wall_becomes_a_single_panel_without_joints()
    {
        var layout = _panelizer.Panelize(2400, [], Default);

        Assert.Empty(layout.Joints);
        var panel = Assert.Single(layout.Panels);
        Assert.Equal(2400, panel.Width, 3);
    }

    [Fact]
    public void Plain_wall_is_split_into_equal_panels()
    {
        var layout = _panelizer.Panelize(9000, [], Default);

        Assert.Equal([3000, 6000], layout.Joints);
        Assert.Equal(3, layout.Panels.Count);
        // End panels lose half a joint, the middle panel loses half a joint on each side.
        Assert.Equal(2990, layout.Panels[0].Width, 3);
        Assert.Equal(2980, layout.Panels[1].Width, 3);
        Assert.Equal(2990, layout.Panels[2].Width, 3);
    }

    [Fact]
    public void Wall_slightly_longer_than_max_is_split_in_two_halves_not_max_plus_sliver()
    {
        var layout = _panelizer.Panelize(3200, [], Default);

        Assert.Equal(1600, Assert.Single(layout.Joints), 3);
    }

    [Fact]
    public void Joint_is_moved_out_of_an_opening_and_its_clearance()
    {
        // Ideal joint at 3000 falls inside the window [2500, 3500].
        var window = new Interval(2500, 3500);

        var layout = _panelizer.Panelize(6000, [window], Default);

        Assert.NotEmpty(layout.Joints);
        Assert.All(layout.Joints, joint => Assert.False(window.Expand(Default.OpeningClearance).ContainsStrictly(joint)));
        Assert.All(layout.Panels, p => Assert.InRange(p.Width, Default.MinPanelWidth - Default.JointWidth, Default.MaxPanelWidth));
    }

    [Fact]
    public void Opening_is_reported_relative_to_the_panel_that_contains_it()
    {
        var door = new Interval(500, 1400);

        var layout = _panelizer.Panelize(6000, [door], Default);

        var first = layout.Panels[0];
        var opening = Assert.Single(first.Openings);
        Assert.Equal(500, opening.Start, 3);
        Assert.Equal(1400, opening.End, 3);
        Assert.Empty(layout.Panels[1].Openings);
    }

    [Fact]
    public void Every_panel_respects_min_and_max_width_for_many_wall_lengths()
    {
        for (var length = 600; length <= 20000; length += 37)
        {
            var layout = _panelizer.Panelize(length, [], Default);
            foreach (var panel in layout.Panels)
            {
                Assert.InRange(panel.Width, Default.MinPanelWidth - Default.JointWidth, Default.MaxPanelWidth);
            }
            Assert.Equal(length, layout.Panels.Sum(p => p.Width) + layout.Joints.Count * Default.JointWidth, 3);
        }
    }

    [Fact]
    public void Throws_a_clear_error_when_openings_block_every_joint_position()
    {
        var wideOpening = new Interval(300, 5700);

        var ex = Assert.Throws<PanelizationException>(() => _panelizer.Panelize(6000, [wideOpening], Default));
        Assert.Contains("No valid joint", ex.Message);
    }

    [Fact]
    public void Rejects_invalid_options()
    {
        var bad = Default with { MinPanelWidth = 2000, MaxPanelWidth = 3000 };

        Assert.Throws<ArgumentException>(() => _panelizer.Panelize(6000, [], bad));
    }
}
