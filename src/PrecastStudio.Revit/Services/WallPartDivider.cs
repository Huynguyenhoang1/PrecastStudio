using Autodesk.Revit.DB;
using PrecastStudio.Core.Panelization;

namespace PrecastStudio.Revit.Services;

/// <summary>
/// Turns a <see cref="PanelLayout"/> into real Revit Parts: the wall is converted to Parts,
/// divided at each joint with the joint width as the division gap, and every resulting part gets its panel mark.
/// Must run inside an open transaction.
/// </summary>
public static class WallPartDivider
{
    public static int Divide(Document document, Wall wall, PanelLayout layout, IReadOnlyList<MarkedPanel> marks)
    {
        var wallIds = new List<ElementId> { wall.Id };
        if (!PartUtils.AreElementsValidForCreateParts(document, wallIds))
            throw new InvalidOperationException("This wall cannot be converted to Parts (it may already have parts).");

        PartUtils.CreateParts(document, wallIds);
        document.Regenerate();

        if (layout.Joints.Count > 0)
        {
            var line = WallReader.GetLocationLine(wall);
            var origin = line.GetEndPoint(0);
            var direction = line.Direction;
            var box = wall.get_BoundingBox(null);
            var bottom = box.Min.Z - 1;
            var top = box.Max.Z + 1;

            // Vertical sketch plane along the wall axis; each joint is a vertical line in that plane.
            var sketchPlane = SketchPlane.Create(document, Plane.CreateByNormalAndOrigin(wall.Orientation, origin));
            var cuts = layout.Joints
                .Select(mm => origin + direction * WallReader.ToFeet(mm))
                .Select(p => (Curve)Line.CreateBound(new XYZ(p.X, p.Y, bottom), new XYZ(p.X, p.Y, top)))
                .ToList();

            var parts = PartUtils.GetAssociatedParts(document, wall.Id, false, true);
            var maker = PartUtils.DivideParts(document, parts, new List<ElementId>(), cuts, sketchPlane.Id);
            PartUtils.GetPartMakerMethodToDivideVolumeFW(maker).DivisionGap = WallReader.ToFeet(layout.Options.JointWidth);
            document.Regenerate();
        }

        return WriteMarks(document, wall, marks);
    }

    private static int WriteMarks(Document document, Wall wall, IReadOnlyList<MarkedPanel> marks)
    {
        var line = WallReader.GetLocationLine(wall);
        var leafParts = PartUtils.GetAssociatedParts(document, wall.Id, false, true)
            .Select(document.GetElement)
            .OfType<Part>()
            .Where(p => !PartUtils.HasAssociatedParts(document, p.Id))
            .OrderBy(p => (Centre(p) - line.GetEndPoint(0)).DotProduct(line.Direction))
            .ToList();

        // Wall layers produce one part per layer per panel; group them back into panels by position.
        var perPanel = leafParts.Count / Math.Max(1, marks.Count);
        if (perPanel == 0 || leafParts.Count % marks.Count != 0) return 0;

        for (var i = 0; i < leafParts.Count; i++)
        {
            leafParts[i].get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(marks[i / perPanel].Mark);
        }
        return leafParts.Count;
    }

    private static XYZ Centre(Element element)
    {
        var box = element.get_BoundingBox(null);
        return (box.Min + box.Max) / 2;
    }
}
