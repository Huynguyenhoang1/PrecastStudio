// Usage:
//   dotnet run --project src/PrecastStudio.Demo
//   dotnet run --project src/PrecastStudio.Demo -- "tấm tối đa 2.4m, khe 15mm, ký hiệu PW"   (needs ANTHROPIC_API_KEY)
using System.Text;
using PrecastStudio.AI;
using PrecastStudio.Core.Geometry;
using PrecastStudio.Core.Lifting;
using PrecastStudio.Core.Panelization;

Console.OutputEncoding = Encoding.UTF8;

const double wallLength = 12_400, wallHeight = 3_200, wallThickness = 200;
Interval[] openings = [new(1_000, 1_900), new(5_800, 7_300), new(10_200, 11_100)];

var options = new PanelizationOptions();
if (args.Length > 0)
{
    var interpreter = ClaudePanelCommandInterpreter.TryCreateFromEnvironment();
    if (interpreter is null)
    {
        Console.WriteLine("Set ANTHROPIC_API_KEY to try the AI instruction. Using default options.");
    }
    else
    {
        var result = await interpreter.InterpretAsync(string.Join(' ', args), options);
        options = result.Options;
        Console.WriteLine($"AI: {result.Explanation}");
    }
}

Console.WriteLine($"Wall {wallLength} x {wallHeight} x {wallThickness} mm, openings: {string.Join(", ", openings)}");
Console.WriteLine($"Rules: max {options.MaxPanelWidth}, min {options.MinPanelWidth}, joint {options.JointWidth}, clearance {options.OpeningClearance}");
Console.WriteLine();

var layout = new WallPanelizer().Panelize(wallLength, openings, options);
var marked = new PanelMarkGenerator().Assign(layout);

Console.WriteLine($"{"Mark",-6}{"Start",8}{"Width",8}{"Openings",10}{"Weight kg",11}{"Anchors",9}");
foreach (var (panel, mark) in marked.Select(m => (m.Panel, m.Mark)))
{
    var lifting = LiftingAnchorCalculator.DesignPanel(panel, wallHeight, wallThickness, openingHeightMm: 2_100, anchorCapacityKg: 2_500);
    Console.WriteLine($"{mark,-6}{panel.Start,8:0}{panel.Width,8:0}{panel.Openings.Count,10}{lifting.WeightKg,11:0}{lifting.AnchorCount,9}");
}

Console.WriteLine();
Console.WriteLine($"Joints at: {string.Join(", ", layout.Joints.Select(j => j.ToString("0")))} mm");
