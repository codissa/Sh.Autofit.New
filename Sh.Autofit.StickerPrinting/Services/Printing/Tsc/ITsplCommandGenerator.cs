using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Tsc;

/// <summary>
/// Marker interface for TSPL-specific command generators
/// </summary>
public interface ITsplCommandGenerator : IPrinterCommandGenerator<string>
{
}
