using System.Windows;
using PrecastStudio.Revit.ViewModels;

namespace PrecastStudio.Revit.Views;

public partial class PanelizeWindow : Window
{
    public PanelizeWindow(PanelizeViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, accepted) => DialogResult = accepted;
    }
}
