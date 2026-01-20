# Zebra Label Sticker Printing Application

A WPF application for printing labels directly to Zebra label printers using ZPL commands. Supports Hebrew/Arabic RTL text with live preview and customization.

## Features

### ✅ Implemented
- **Direct Printer Communication** - Sends ZPL commands directly via Win32 API (no file creation)
- **Print on Demand Mode** - Single item label printing
- **2-Up Label Layout** - Prints 2 labels side-by-side on 106mm media (51mm per label)
- **Hebrew/Arabic Bitmap Rendering** - RTL text rendered as bitmaps using System.Drawing (Arial font) for proper display
- **Prefix Exclusion Rules** - Items with 3pk, 4pk, 5pk, 6pk, 7pk, 8pk, 9.5X, 12.5X, Ax, bx show ItemKey only
- **Font Auto-Sizing** - Calculates optimal font size to fit label dimensions
- **Multi-Line Text** - Splits long descriptions across multiple lines
- **Printer Discovery** - Auto-detects Zebra printers (ZDesigner, ZD, ZT, S4M models)
- **Generic Architecture** - Extensible design supports future printer types (Dymo, Brother, etc.)

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
Printer Implementation (ZebraPrinterService, ZplCommandGenerator, RawPrinterCommunicator)
```

### Key Components

**Printer Services:**
- `RawPrinterCommunicator` - Win32 API wrapper (OpenPrinter, WritePrinter with "RAW" data type)
- `ZplCommandGenerator` - ZPL command generation (^XA, ^PW, ^LL, ^FO, ^FD, ^XZ)
- `ZebraPrinterService` - Complete Zebra printer service

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

1. **Select Printer** - Choose Zebra printer from dropdown
2. **Enter Item Key** - Type the part number (e.g., "12345")
3. **Click "Load Part"** - Fetches part information from database
4. **Select Language** - Choose Hebrew (default) or Arabic
5. **Review Preview** - Check intro line, item key, and description
6. **Set Quantity** - Enter number of labels to print
7. **Click "Print Label"** - Sends ZPL commands directly to printer

### Label Layout (2-Up Configuration)

```
┌─────────────────────────────────────────────────────────────────────┐
│  [Left Label 51mm]       [2mm gap]      [Right Label 51mm]         │
│  ┌───────────────────┐                  ┌───────────────────┐      │
│  │ S.H. Car Rubber...│                  │ S.H. Car Rubber...│      │
│  │                   │                  │                   │      │
│  │    ITEMKEY123     │                  │    ITEMKEY123     │      │
│  │                   │                  │                   │      │
│  │  תיאור החלק בעברית │                  │  תיאור החלק בעברית│      │
│  └───────────────────┘                  └───────────────────┘      │
└─────────────────────────────────────────────────────────────────────┘
         2mm margin                                      Total: 106mm
```

## Label Dimensions (2-Up)

- **Web Width**: 106mm (full paper roll width)
- **Single Label Width**: 51mm
- **Label Height**: 25mm (2.5cm)
- **Horizontal Gap**: 2mm (between left and right labels)
- **Vertical Spacing**: 2mm (between label rows)
- **Left Margin**: 2mm
- **DPI**: 203 (Zebra S4M standard)

## Quantity Printing Logic

When printing N labels:
- Prints N/2 rows in 2-up format (2 labels side-by-side)
- Prints N%2 single label on left (if odd quantity)

**Examples:**
- **1 label**: 1 label on left
- **2 labels**: 1 row (2-up)
- **3 labels**: 1 row (2-up) + 1 label on left
- **5 labels**: 2 rows (2-up) + 1 label on left
- **10 labels**: 5 rows (2-up)

## Hebrew/Arabic Rendering

Labels use **bitmap rendering** for Hebrew/Arabic text:
- Renders text as 1-bit bitmap using **System.Drawing**
- Converts to ZPL `^GFA` (Graphic Field Alphanumeric) commands
- Uses **Arial font** for proper character shaping and RTL direction
- Supports mixed RTL/LTR text (e.g., numbers in Hebrew descriptions)
- English/numbers use native ZPL fonts for better performance

**Why Bitmap Rendering?**
- Zebra built-in fonts (`^A0`) do not support Unicode Hebrew/Arabic properly
- Bitmap rendering ensures text appears exactly as intended (matching LBL template output)
- Windows GDI+ handles BiDi text direction automatically

## ZPL Commands Generated

### Sample ZPL for 2-Up Layout (English Text)
```zpl
^XA
^PW847        # Web width: 106mm = 847 dots @ 203 DPI
^LL197        # Label height: 25mm = 197 dots
^LH0,0        # Label home position
# Left Label (baseX = 16 dots)
^FO16,16
^A0N,30,30
^FDS.H. Car Rubber Import and Distribution^FS
^FO186,69
^A0N,60,60
^FDITEMKEY123^FS
# Right Label (baseX = 432 dots)
^FO432,16
^A0N,30,30
^FDS.H. Car Rubber Import and Distribution^FS
^FO602,69
^A0N,60,60
^FDITEMKEY123^FS
^PQ1,0,1,Y    # Print quantity (controlled by service)
^XZ
```

### Sample ZPL with Hebrew GFA Bitmap
```zpl
^XA
^PW847
^LL197
^LH0,0
# English text uses native ZPL font
^FO16,16
^A0N,30,30
^FDS.H. Car Rubber Import and Distribution^FS
# Hebrew text rendered as bitmap
^FO186,118
^GFA,240,240,15,
003FE0007FF000FFE001FFC003FF8003FF8003FF8003FF8001FFC000FFE0007FF00003FE0
^FS
^PQ1,0,1,Y
^XZ
```

**Key Changes from Old Format:**
- `^PW` now set to **web width** (847 dots) instead of label width
- `^LL` set to **label height** (197 dots)
- **Removed** `^MNN` (media tracking)
- **Dynamic** `^PQ` based on quantity (not hardcoded to 1)
- Hebrew/Arabic uses `^GFA` bitmap commands

## Running the Application

```bash
# Build the project
dotnet build Sh.Autofit.StickerPrinting/Sh.Autofit.StickerPrinting.csproj

# Run the application
dotnet run --project Sh.Autofit.StickerPrinting/Sh.Autofit.StickerPrinting.csproj
```

Or open in Visual Studio and press F5.

## Testing Without a Printer

The application will still run without a Zebra printer connected. You can:
- Test the UI and data loading
- Verify label data generation
- Check ZPL command generation (commands are sent to printer but will fail gracefully)

For actual printing, you need:
- Zebra label printer (ZDesigner S4M, ZD, ZT series)
- Printer installed in Windows with Zebra driver
- Labels loaded (106mm x 25mm recommended)

## Troubleshooting

### Printer Not Found
- Check printer is powered on and connected
- Verify printer is installed in Windows (Control Panel > Devices and Printers)
- Ensure printer name contains "Zebra", "ZDesigner", "ZD", "ZT", "S4M", "GK", or "GX"

### RTL Text Issues
- Hebrew/Arabic text uses BiDi algorithm for proper character ordering
- Handles mixed LTR/RTL text (e.g., numbers in Hebrew text)
- Uses Zebra built-in Font 0 for all text

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
7. **Multiple Printer Support** - Add support for other printer types (Dymo, Brother, etc.)

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
│   │   ├── Zebra/                   (Zebra implementation)
│   │   │   ├── ZebraPrinterService.cs
│   │   │   ├── ZplCommandGenerator.cs
│   │   │   └── IZplCommandGenerator.cs
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
