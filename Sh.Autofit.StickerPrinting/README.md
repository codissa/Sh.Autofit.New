# TSC Label Sticker Printing Application

A WPF application for printing labels directly to TSC label printers using TSPL commands. Supports Hebrew/Arabic RTL text with live preview and customization.

## Features

### ✅ Implemented
- **Direct Printer Communication** - Sends TSPL commands directly via Win32 API (no file creation)
- **Print on Demand Mode** - Single item label printing
- **Hebrew/Arabic Support** - RTL text rendering with character reversal
- **Prefix Exclusion Rules** - Items with 3pk, 4pk, 5pk, 6pk, 7pk, 8pk, 9.5X, 12.5X, Ax, bx show ItemKey only
- **Font Auto-Sizing** - Calculates optimal font size to fit label dimensions
- **Multi-Line Text** - Splits long descriptions across multiple lines
- **Printer Discovery** - Auto-detects TSC printers (TTP, TDP, MH, ME, Alpha models)
- **Generic Architecture** - Extensible design supports future printer types (Zebra, Dymo, etc.)

### 🚧 To Be Implemented
- Stock Move Mode (batch printing)
- Arabic description editing dialog
- Label preview visualization
- Settings persistence
- Autocomplete for ItemKey input

## Architecture

### Three-Layer Design
```
ViewModels (UI Logic)
    ↓
Business Services (Label Rendering, Database Access)
    ↓
Printer Abstraction (IPrinterService, IPrinterCommandGenerator)
    ↓
Printer Implementation (TscPrinterService, TsplCommandGenerator, RawPrinterCommunicator)
```

### Key Components

**Printer Services:**
- `RawPrinterCommunicator` - Win32 API wrapper (OpenPrinter, WritePrinter with "RAW" data type)
- `TsplCommandGenerator` - TSPL command generation (SIZE, GAP, TEXT, PRINT)
- `TscPrinterService` - Complete TSC printer service

**Database Services:**
- `PartDataService` - Load parts from vw_Parts view
- `ArabicDescriptionService` - CRUD for Arabic translations
- `StockDataService` - Load Stock and StockMoves

**Label Rendering:**
- `LabelRenderService` - Creates label data, applies prefix rules, optimizes font sizes
- `FontSizeCalculator` - Text measurement and multi-line splitting
- `PrefixChecker` - Prefix exclusion logic

## Database Schema

### Tables/Views Used
- `dbo.vw_Parts` - Part information with Hebrew descriptions
- `dbo.ArabicPartDescriptions` - Arabic translations (ItemKey, ArabicDescription, IsActive)
- `SH2013.dbo.Stock` - Stock header (StockID, AccountName, AccountKey)
- `SH2013.dbo.StockMoves` - Stock move items (aggregated by ItemKey)

### Connection String
```
Data Source=server-pc\wizsoft2;Initial Catalog=Sh.Autofit;
User ID=issa;Password=5060977Ih;TrustServerCertificate=True;
```

## How to Use

### Print on Demand Mode

1. **Select Printer** - Choose TSC printer from dropdown
2. **Enter Item Key** - Type the part number (e.g., "12345")
3. **Click "Load Part"** - Fetches part information from database
4. **Select Language** - Choose Hebrew (default) or Arabic
5. **Review Preview** - Check intro line, item key, and description
6. **Set Quantity** - Enter number of labels to print
7. **Click "Print Label"** - Sends TSPL commands directly to printer

### Label Layout

```
┌────────────────────────────────────┐
│ S.H. Car Rubber Import and Dist... │  (Intro line - small, left)
│                                    │
│         ITEMKEY123                 │  (Item key - large, center, bold)
│                                    │
│    תיאור החלק בעברית או עربית      │  (Description - center, auto-sized)
│    or multi-line if too long       │
└────────────────────────────────────┐
```

## Label Dimensions

- **Width**: 106mm (10.6cm)
- **Height**: 25mm (2.5cm)
- **DPI**: 203 (TSC standard)
- **Margins**: 2mm on all sides

## TSPL Commands Generated

Sample TSPL output for a label:
```tspl
SIZE 106.0 mm, 25.0 mm
GAP 2 mm, 0 mm
DIRECTION 1
REFERENCE 0,0
OFFSET 0 mm
SET PEEL OFF
SET CUTTER OFF
SET TEAR ON
CLS
TEXT 10,10,"0",0,1,1,"S.H. Car Rubber Import and Distribution"
TEXT 200,40,"0",0,2,2,"ITEMKEY123"
TEXT 200,70,"HEBREW.TTF",0,1,1,"תיאור בעברית"
PRINT 1,1
```

## Running the Application

```bash
# Build the project
dotnet build Sh.Autofit.StickerPrinting/Sh.Autofit.StickerPrinting.csproj

# Run the application
dotnet run --project Sh.Autofit.StickerPrinting/Sh.Autofit.StickerPrinting.csproj
```

Or open in Visual Studio and press F5.

## Testing Without a Printer

The application will still run without a TSC printer connected. You can:
- Test the UI and data loading
- Verify label data generation
- Check TSPL command generation (commands are sent to printer but will fail gracefully)

For actual printing, you need:
- TSC label printer (TTP, TDP, MH, ME, Alpha series)
- Printer installed in Windows with TSC driver
- Labels loaded (106mm x 25mm recommended)

## Troubleshooting

### Printer Not Found
- Check printer is powered on and connected
- Verify printer is installed in Windows (Control Panel > Devices and Printers)
- Ensure printer name contains "TSC", "TTP", "TDP", "MH", "ME", or "Alpha"

### RTL Text Issues
- Hebrew/Arabic text uses simple character reversal
- For complex diacritics, may need BiDi algorithm library (ICU4N)
- Font files (HEBREW.TTF, ARABIC.TTF) must be installed on printer

### Database Connection Issues
- Verify SQL Server is accessible at `server-pc\wizsoft2`
- Check credentials (User: issa, Password: 5060977Ih)
- Ensure database `Sh.Autofit` exists with required tables/views

## Future Enhancements

1. **Stock Move Mode** - Batch printing from stock movements
2. **Arabic Description Editor** - Dialog for editing/creating Arabic translations
3. **Label Preview** - Visual preview using WPF DrawingVisual
4. **Autocomplete** - Real-time suggestions for ItemKey input
5. **Settings Persistence** - Save selected printer, default intro line
6. **Barcode Support** - Add barcode/QR code generation
7. **Multiple Printer Support** - Switch between Zebra (ZPL), Dymo, etc.

## Project Structure

```
Sh.Autofit.StickerPrinting/
├── App.xaml & App.xaml.cs           (DI setup, entry point)
├── Models/                          (7 models)
│   ├── LabelData.cs
│   ├── PrintJob.cs
│   ├── StickerSettings.cs
│   ├── PrinterInfo.cs
│   ├── StockInfo.cs
│   ├── PartInfo.cs
│   └── StockMoveItem.cs
├── Helpers/
│   ├── PrefixChecker.cs
│   └── FontSizeCalculator.cs
├── Commands/
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
├── Converters/                      (WPF value converters)
│   ├── LanguageConverter.cs
│   ├── NullToBoolConverter.cs
│   └── InverseBoolConverter.cs
├── Services/
│   ├── Database/
│   │   ├── PartDataService.cs
│   │   ├── ArabicDescriptionService.cs
│   │   └── StockDataService.cs
│   ├── Printing/
│   │   ├── Abstractions/            (Generic interfaces)
│   │   │   ├── IPrinterService.cs
│   │   │   ├── IPrinterCommandGenerator.cs
│   │   │   ├── IRawPrinterCommunicator.cs
│   │   │   └── PrinterCapabilities.cs
│   │   ├── Tsc/                     (TSC implementation)
│   │   │   ├── TscPrinterService.cs
│   │   │   ├── TsplCommandGenerator.cs
│   │   │   └── ITsplCommandGenerator.cs
│   │   └── Infrastructure/          (Win32 API)
│   │       └── RawPrinterCommunicator.cs
│   └── Label/
│       ├── LabelRenderService.cs
│       └── ILabelRenderService.cs
├── ViewModels/
│   ├── MainViewModel.cs
│   ├── PrintOnDemandViewModel.cs
│   └── StockMoveViewModel.cs
└── Views/
    └── MainWindow.xaml
```

## License

Internal use for S.H. Car Rubber Import and Distribution.
