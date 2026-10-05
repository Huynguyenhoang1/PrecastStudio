using PrecastStudio.Core.Panelization;

namespace PrecastStudio.AI;

/// <param name="Explanation">Short note from the model on what it changed, shown to the user before applying.</param>
public sealed record PanelCommandResult(PanelizationOptions Options, string Explanation);

/// <summary>Turns a user instruction such as "tấm tối đa 2.4m, khe 15mm" into panelization options.</summary>
public interface IPanelCommandInterpreter
{
    Task<PanelCommandResult> InterpretAsync(string instruction, PanelizationOptions current, CancellationToken cancellationToken = default);
}

public sealed class PanelCommandException(string message) : Exception(message);
