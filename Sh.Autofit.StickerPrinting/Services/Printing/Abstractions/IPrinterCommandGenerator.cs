using Sh.Autofit.StickerPrinting.Models;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

/// <summary>
/// Generates printer-specific command strings
/// Generic interface allows for different command languages (TSPL, ZPL, ESC/POS, etc.)
/// </summary>
/// <typeparam name="TCommand">Command result type (string for most printers)</typeparam>
public interface IPrinterCommandGenerator<TCommand>
{
    /// <summary>
    /// Generate printer commands for a single label
    /// </summary>
    TCommand GenerateLabelCommands(LabelData labelData, StickerSettings settings, int pairs, bool printTwoUp);

    /// <summary>
    /// Generate initialization commands (if needed before printing)
    /// </summary>
    TCommand? GenerateInitializationCommands(StickerSettings settings);

    /// <summary>
    /// Generate finalization commands (if needed after printing)
    /// </summary>
    TCommand? GenerateFinalizationCommands();
}
