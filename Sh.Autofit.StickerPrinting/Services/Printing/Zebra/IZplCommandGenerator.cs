using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;

namespace Sh.Autofit.StickerPrinting.Services.Printing.Zebra;

/// <summary>
/// Marker interface for ZPL command generators
/// Inherits from generic IPrinterCommandGenerator with string commands
/// </summary>
public interface IZplCommandGenerator : IPrinterCommandGenerator<string>
{
    // Marker interface - all functionality inherited from base
}
