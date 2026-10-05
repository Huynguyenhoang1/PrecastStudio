using Autodesk.Revit.DB;
using PrecastStudio.Core.Geometry;

namespace PrecastStudio.Revit.Services;

/// <summary>Revit-free description of a straight wall, in millimetres.</summary>
public sealed record WallInfo(double LengthMm, double HeightMm, double ThicknessMm, IReadOnlyList<Interval> Openings);

/// <summary>Reads what the panelizer needs from a Revit wall: length, height, thickness and opening ranges.</summary>
public static class WallReader
{
    public static WallInfo Read(Wall wall)
    {
        var line = GetLocationLine(wall);
        var length = line.Length;
        var height = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.AsDouble() ?? 0;

        var openings = new List<Interval>();
        foreach (var insertId in wall.FindInserts(addRectOpenings: true, includeShadows: false, includeEmbeddedWalls: false, includeSharedEmbeddedInserts: false))
        {
            var box = wall.Document.GetElement(insertId)?.get_BoundingBox(null);
            if (box is null) continue;

            // Project the four plan corners onto the wall axis so skewed walls work too.
            var corners = new[]
            {
                new XYZ(box.Min.X, box.Min.Y, 0), new XYZ(box.Max.X, box.Min.Y, 0),
                new XYZ(box.Min.X, box.Max.Y, 0), new XYZ(box.Max.X, box.Max.Y, 0),
            };
            var along = corners.Select(c => AlongWall(line, c)).ToList();
            var start = Math.Clamp(along.Min(), 0, length);
            var end = Math.Clamp(along.Max(), 0, length);
            if (end - start > 1e-6) openings.Add(new Interval(ToMm(start), ToMm(end)));
        }

        return new WallInfo(ToMm(length), ToMm(height), ToMm(wall.Width), openings);
    }

    public static Line GetLocationLine(Wall wall) =>
        (wall.Location as LocationCurve)?.Curve as Line
        ?? throw new InvalidOperationException("Only straight walls are supported.");

    private static double AlongWall(Line line, XYZ point)
    {
        var start = line.GetEndPoint(0);
        var flatStart = new XYZ(start.X, start.Y, 0);
        var flatDirection = new XYZ(line.Direction.X, line.Direction.Y, 0).Normalize();
        return (point - flatStart).DotProduct(flatDirection);
    }

    public static double ToMm(double feet) => UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);

    public static double ToFeet(double mm) => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);
}
