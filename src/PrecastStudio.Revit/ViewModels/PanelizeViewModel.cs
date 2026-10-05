using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrecastStudio.AI;
using PrecastStudio.Core.Lifting;
using PrecastStudio.Core.Panelization;
using PrecastStudio.Revit.Services;

namespace PrecastStudio.Revit.ViewModels;

/// <summary>One row in the panel schedule, with pixel values for the elevation preview.</summary>
public sealed record PanelRow(
    string Mark,
    double StartMm,
    double WidthMm,
    int OpeningCount,
    double WeightKg,
    int AnchorCount,
    double PreviewLeft,
    double PreviewWidth);

/// <summary>
/// Drives the panelize dialog. Has no Revit API calls, so it can be reused or tested outside Revit:
/// every option change re-runs the panelizer and refreshes the schedule and preview.
/// </summary>
public partial class PanelizeViewModel : ObservableObject
{
    public const double PreviewWidthPx = 640;

    /// <summary>Opening height assumed for the weight estimate (typical door height).</summary>
    private const double OpeningHeightMm = 2100;

    private readonly WallInfo _wall;
    private readonly WallPanelizer _panelizer;
    private readonly PanelMarkGenerator _markGenerator = new();
    private readonly IPanelCommandInterpreter? _interpreter;

    public PanelizeViewModel(WallInfo wall, WallPanelizer panelizer, IPanelCommandInterpreter? interpreter)
    {
        _wall = wall;
        _panelizer = panelizer;
        _interpreter = interpreter;
        ApplyOptions(new PanelizationOptions());
    }

    public event EventHandler<bool>? CloseRequested;

    public ObservableCollection<PanelRow> Panels { get; } = [];

    public string WallSummary =>
        $"Wall {_wall.LengthMm:0} × {_wall.HeightMm:0} mm, thickness {_wall.ThicknessMm:0} mm, {_wall.Openings.Count} opening(s)";

    public bool IsAiAvailable => _interpreter is not null;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(AcceptCommand))] private PanelLayout? _layout;
    [ObservableProperty] private IReadOnlyList<MarkedPanel> _marks = [];
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private double _maxPanelWidth;
    [ObservableProperty] private double _minPanelWidth;
    [ObservableProperty] private double _jointWidth;
    [ObservableProperty] private double _openingClearance;
    [ObservableProperty] private string _markPrefix = "W";
    [ObservableProperty] private double _anchorCapacityKg = 2500;
    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(AskAiCommand))] private string _aiInstruction = string.Empty;

    private bool _suppressRecalculate;

    partial void OnMaxPanelWidthChanged(double value) => Recalculate();
    partial void OnMinPanelWidthChanged(double value) => Recalculate();
    partial void OnJointWidthChanged(double value) => Recalculate();
    partial void OnOpeningClearanceChanged(double value) => Recalculate();
    partial void OnMarkPrefixChanged(string value) => Recalculate();
    partial void OnAnchorCapacityKgChanged(double value) => Recalculate();

    private PanelizationOptions CurrentOptions => new()
    {
        MaxPanelWidth = MaxPanelWidth,
        MinPanelWidth = MinPanelWidth,
        JointWidth = JointWidth,
        OpeningClearance = OpeningClearance,
        MarkPrefix = MarkPrefix,
    };

    private void ApplyOptions(PanelizationOptions options)
    {
        _suppressRecalculate = true;
        MaxPanelWidth = options.MaxPanelWidth;
        MinPanelWidth = options.MinPanelWidth;
        JointWidth = options.JointWidth;
        OpeningClearance = options.OpeningClearance;
        MarkPrefix = options.MarkPrefix;
        _suppressRecalculate = false;
        Recalculate();
    }

    private void Recalculate()
    {
        if (_suppressRecalculate) return;
        Panels.Clear();
        try
        {
            Layout = _panelizer.Panelize(_wall.LengthMm, _wall.Openings, CurrentOptions);
            Marks = _markGenerator.Assign(Layout);
            var scale = PreviewWidthPx / _wall.LengthMm;
            foreach (var (panel, mark) in Marks.Select(m => (m.Panel, m.Mark)))
            {
                var lifting = LiftingAnchorCalculator.DesignPanel(panel, _wall.HeightMm, _wall.ThicknessMm, OpeningHeightMm, AnchorCapacityKg);
                Panels.Add(new PanelRow(mark, panel.Start, panel.Width, panel.Openings.Count, lifting.WeightKg, lifting.AnchorCount,
                    panel.Start * scale, panel.Width * scale));
            }
            Status = $"{Layout.Panels.Count} panels, {Marks.Select(m => m.Mark).Distinct().Count()} unique marks.";
        }
        catch (Exception ex) when (ex is PanelizationException or ArgumentException)
        {
            Layout = null;
            Marks = [];
            Status = ex.Message;
        }
    }

    private bool CanAskAi() => _interpreter is not null && !string.IsNullOrWhiteSpace(AiInstruction);

    [RelayCommand(CanExecute = nameof(CanAskAi), IncludeCancelCommand = true)]
    private async Task AskAiAsync(CancellationToken cancellationToken)
    {
        Status = "Asking AI…";
        try
        {
            var result = await _interpreter!.InterpretAsync(AiInstruction, CurrentOptions, cancellationToken);
            ApplyOptions(result.Options);
            Status = $"AI: {result.Explanation}  →  {Status}";
        }
        catch (OperationCanceledException)
        {
            Status = "AI request cancelled.";
        }
        catch (Exception ex)
        {
            Status = $"AI error: {ex.Message}";
        }
    }

    private bool CanAccept() => Layout is not null;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept() => CloseRequested?.Invoke(this, true);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, false);
}
