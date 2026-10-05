using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PrecastStudio.AI;
using PrecastStudio.Core.Panelization;
using PrecastStudio.Revit.Services;
using PrecastStudio.Revit.ViewModels;
using PrecastStudio.Revit.Views;

namespace PrecastStudio.Revit.Commands;

[Transaction(TransactionMode.Manual)]
public class PanelizeWallCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        var uiDocument = commandData.Application.ActiveUIDocument;
        var document = uiDocument.Document;

        Wall wall;
        try
        {
            var picked = uiDocument.Selection.PickObject(ObjectType.Element, new WallSelectionFilter(), "Select a straight wall to panelize");
            wall = (Wall)document.GetElement(picked);
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
            return Result.Cancelled;
        }

        WallInfo info;
        try
        {
            info = WallReader.Read(wall);
        }
        catch (InvalidOperationException ex)
        {
            message = ex.Message;
            return Result.Failed;
        }

        var viewModel = new PanelizeViewModel(info, new WallPanelizer(), ClaudePanelCommandInterpreter.TryCreateFromEnvironment());
        var window = new PanelizeWindow(viewModel);
        new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
        if (window.ShowDialog() != true || viewModel.Layout is null) return Result.Cancelled;

        using var transaction = new Transaction(document, "Precast Studio: panelize wall");
        transaction.Start();
        try
        {
            var marked = WallPartDivider.Divide(document, wall, viewModel.Layout, viewModel.Marks);
            transaction.Commit();
            TaskDialog.Show("Precast Studio",
                $"Created {viewModel.Layout.Panels.Count} panels." +
                (marked > 0 ? $" Marks written to the Comments parameter of {marked} parts." : " Marks could not be matched to parts; check the wall layers."));
            return Result.Succeeded;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Autodesk.Revit.Exceptions.ArgumentException)
        {
            transaction.RollBack();
            message = ex.Message;
            return Result.Failed;
        }
    }

    private sealed class WallSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element element) => element is Wall { Location: LocationCurve { Curve: Line } };

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
