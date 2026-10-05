using System.Reflection;
using Autodesk.Revit.UI;
using PrecastStudio.Revit.Commands;

namespace PrecastStudio.Revit;

public class App : IExternalApplication
{
    public Result OnStartup(UIControlledApplication application)
    {
        var panel = application.CreateRibbonPanel("Precast Studio");
        var button = new PushButtonData(
            nameof(PanelizeWallCommand),
            "Panelize\nWall",
            Assembly.GetExecutingAssembly().Location,
            typeof(PanelizeWallCommand).FullName)
        {
            ToolTip = "Split a wall into precast panels, avoiding openings, and mark identical panels.",
        };
        panel.AddItem(button);
        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
