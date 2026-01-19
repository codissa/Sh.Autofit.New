# Implementation Plan: TSC Label Sticker Printing Application

## Overview
A WPF application for printing stickers to TSC label printers with two modes: Print on Demand and Print by Stock Move. Supports Hebrew/Arabic descriptions with live preview and customization.

---

## User Requirements Summary

### Mode 1: Print on Demand
- Select item by ItemKey
- Auto complete by partial case insensitive part keys, show also part description in autocomplete list
- Radio button: Hebrew (default) / Arabic
- Live preview showing: Intro line, ItemKey, Description
- Edit preview: Text, font, font size
- Hebrew description from vw_Parts view
- Arabic description from ArabicPartDescriptions table
- **Arabic-only**: Button to edit Arabic description (persists to DB) also if no arabic description available i can create one and it will create a new record in the arabic description table
- Quantity selector
- Print button
- ability to edit both description for printing and persisting is optional

### Mode 2: Print by Stock Move
- Input StockID
- Display AccountName from Stock table
- Load items from StockMoves (aggregated by ItemKey)
- List of preview labels with quantities
- Toggle all labels Hebrew/Arabic (button above list)
- Edit individual label preview and quantity
- Add/remove labels
- Print button

### Special Rules
- **Prefix exclusion**: Items starting with `3pk`, `4pk`, `5pk`, `6pk`, `7pk`, `8pk`, `9.5X`, `12.5X`, `Ax`, `bx` → show ONLY ItemKey, no description
- **Default intro line**: "S.H. Car Rubber Import and Distribution" (editable per-preview or globally)
- **Text sizing**: Auto-expand to fit optimally (width + height), start with initial font size, shrink if too large
- **Multi-line description**: If description too long, start new line

### Printer Features
- Show connected printer
- Change printer
- Show printer status
- double label printing with dimensions, set persistable dimensions that can be controlled in 

---

## Technical Architecture

### Project Structure
```
Sh.Autofit.StickerPrinting/
├── App.xaml & App.xaml.cs           (DI setup, entry point)
├── Views/
│   ├── MainWindow.xaml & .xaml.cs   (Tab control: Print on Demand + Stock Move)
│   ├── LabelPreviewControl.xaml     (Reusable preview control)
│   ├── PrinterSettingsDialog.xaml  (Printer config)
│   └── EditArabicDialog.xaml        (Edit Arabic description)
├── ViewModels/
│   ├── MainViewModel.cs             (Tab management, printer selection)
│   ├── PrintOnDemandViewModel.cs    (Mode 1 logic)
│   ├── StockMoveViewModel.cs        (Mode 2 logic)
│   ├── LabelPreviewViewModel.cs     (Individual label preview)
│   └── PrinterSettingsViewModel.cs  (Printer management)
├── Models/
│   ├── StickerSettings.cs           (Label config: dimensions, fonts)
│   ├── LabelData.cs                 (Label content: intro, key, desc, language)
│   ├── PrintJob.cs                  (Print job with quantity)
│   ├── StockInfo.cs                 (Stock header info)
│   └── PrinterInfo.cs               (Printer status)
├── Services/
│   ├── Database/
│   │   ├── IPartDataService.cs
│   │   ├── PartDataService.cs       (Load parts, Arabic descriptions)
│   │   ├── IArabicDescriptionService.cs
│   │   ├── ArabicDescriptionService.cs (CRUD for Arabic descriptions)
│   │   ├── IStockDataService.cs
│   │   └── StockDataService.cs      (Load Stock + StockMoves)
│   ├── Printing/
│   │   ├── ITSCPrinterService.cs
│   │   ├── TSCPrinterService.cs     (TSPL generation, printer comm)
│   │   └── PrinterStatusMonitor.cs  (Printer status polling)
│   └── Label/
│       ├── ILabelRenderService.cs
│       └── LabelRenderService.cs    (Text sizing, multi-line logic)
├── Commands/
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
└── Helpers/
    ├── PrefixChecker.cs             (Check for 3pk, 4pk, etc.)
    └── FontSizeCalculator.cs        (Auto-fit text logic)
```

---

## Phase 1: Project Setup & Infrastructure

### 1.1 Create WPF Project

**File**: `Sh.Autofit.StickerPrinting\Sh.Autofit.StickerPrinting.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <AssemblyName>Sh.Autofit.StickerPrinting</AssemblyName>
    <RootNamespace>Sh.Autofit.StickerPrinting</RootNamespace>
    <Version>1.0.0</Version>
    <Product>TSC Sticker Printing - Label Printer</Product>
  </PropertyGroup>

  <ItemGroup>
    <!-- Database Access -->
    <PackageReference Include="Dapper" Version="2.1.35" />
    <PackageReference Include="System.Data.SqlClient" Version="4.8.6" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.10" />

    <!-- Printer communication (placeholder - depends on TSC SDK) -->
    <!-- <PackageReference Include="TSCSDK" Version="X.X.X" /> -->

    <!-- UI utilities -->
    <PackageReference Include="System.Drawing.Common" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <!-- Shared data access -->
    <ProjectReference Include="..\Sh.Autofit.New.Entities\Sh.Autofit.New.Entities.csproj" />
  </ItemGroup>
</Project>
```

### 1.2 App.xaml.cs Setup

**File**: `App.xaml.cs`

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);

    // Connection string (same as StockExport)
    const string connectionString =
        "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;" +
        "User ID=issa;Password=5060977Ih;TrustServerCertificate=True;";

    // Initialize services
    var partDataService = new PartDataService(connectionString);
    var arabicDescService = new ArabicDescriptionService(connectionString);
    var stockDataService = new StockDataService(connectionString);
    var printerService = new TSCPrinterService();
    var labelRenderService = new LabelRenderService();

    // Initialize ViewModels
    var printOnDemandVM = new PrintOnDemandViewModel(
        partDataService, arabicDescService, printerService, labelRenderService);
    var stockMoveVM = new StockMoveViewModel(
        stockDataService, partDataService, printerService, labelRenderService);

    var mainViewModel = new MainViewModel(
        printOnDemandVM, stockMoveVM, printerService);

    // Show main window
    var mainWindow = new MainWindow { DataContext = mainViewModel };
    mainWindow.Show();
}
```

---

## Phase 2: Data Models

### 2.1 Label Data Model

**File**: `Models/LabelData.cs`

```csharp
public class LabelData : INotifyPropertyChanged
{
    private string _introLine = "S.H. Car Rubber Import and Distribution";
    private string _itemKey = string.Empty;
    private string _description = string.Empty;
    private string _language = "he"; // "he" or "ar"
    private double _fontSize = 12.0;
    private string _fontFamily = "Arial";
    private int _quantity = 1;

    public string IntroLine
    {
        get => _introLine;
        set { _introLine = value; OnPropertyChanged(); }
    }

    public string ItemKey
    {
        get => _itemKey;
        set { _itemKey = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string Language
    {
        get => _language;
        set { _language = value; OnPropertyChanged(); }
    }

    public double FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(); }
    }

    public string FontFamily
    {
        get => _fontFamily;
        set { _fontFamily = value; OnPropertyChanged(); }
    }

    public int Quantity
    {
        get => _quantity;
        set { _quantity = value; OnPropertyChanged(); }
    }

    // Computed properties
    public bool ShouldShowDescription => !PrefixChecker.HasExcludedPrefix(ItemKey);
    public bool IsArabic => Language == "ar";
    public bool IsHebrew => Language == "he";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

### 2.2 Print Job Model

**File**: `Models/PrintJob.cs`

```csharp
public class PrintJob
{
    public LabelData LabelData { get; set; } = null!;
    public StickerSettings Settings { get; set; } = null!;

    // TSPL coordinates (calculated based on DPI and margins)
    public int IntroX => CalculateX(Settings.LeftMargin);
    public int IntroY => CalculateY(Settings.TopMargin);

    public int ItemKeyX => CalculateX(Settings.WidthMm / 2); // Center
    public int ItemKeyY => CalculateY(Settings.HeightMm * 0.35);

    public int DescriptionX => CalculateX(Settings.WidthMm / 2); // Center
    public int DescriptionY => CalculateY(Settings.HeightMm * 0.6);

    private int CalculateX(double mm) => (int)(mm * Settings.DPI / 25.4);
    private int CalculateY(double mm) => (int)(mm * Settings.DPI / 25.4);
}
```

### 2.3 Sticker Settings Model

**File**: `Models/StickerSettings.cs`

```csharp
public class StickerSettings
{
    // Label dimensions (TSC printer)
    public double WidthMm { get; set; } = 106.0;  // 10.6cm
    public double HeightMm { get; set; } = 25.0;  // 2.5cm

    // Printer settings
    public string PrinterName { get; set; } = string.Empty;
    public int DPI { get; set; } = 203; // TSC standard DPI

    // Layout (margins in mm)
    public double TopMargin { get; set; } = 2.0;
    public double BottomMargin { get; set; } = 2.0;
    public double LeftMargin { get; set; } = 2.0;
    public double RightMargin { get; set; } = 2.0;

    // Default font sizes (in points)
    public int IntroFontSize { get; set; } = 10;
    public int ItemKeyFontSize { get; set; } = 14;
    public int DescriptionFontSize { get; set; } = 12;

    // Global defaults
    public string DefaultIntroLine { get; set; } =
        "S.H. Car Rubber Import and Distribution";
}
```

### 2.4 Stock Info Model

**File**: `Models/StockInfo.cs`

```csharp
public class StockInfo
{
    public int StockId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string AccountKey { get; set; } = string.Empty;
    public DateTime? ValueDate { get; set; }
    public string Remarks { get; set; } = string.Empty;
}
```

### 2.5 Printer Info Model

**File**: `Models/PrinterInfo.cs`

```csharp
public enum PrinterStatus
{
    Unknown,
    Ready,
    Offline,
    OutOfPaper,
    Error
}

public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public PrinterStatus Status { get; set; } = PrinterStatus.Unknown;
    public string StatusMessage { get; set; } = "Unknown";
    public bool IsConnected => Status != PrinterStatus.Offline &&
                                Status != PrinterStatus.Unknown;
}
```

---

## Phase 3: Helper Classes

### 3.1 Prefix Checker

**File**: `Helpers/PrefixChecker.cs`

```csharp
public static class PrefixChecker
{
    private static readonly string[] ExcludedPrefixes =
    {
        "3pk", "4pk", "5pk", "6pk", "7pk", "8pk",
        "9.5X", "12.5X", "Ax", "bx"
    };

    public static bool HasExcludedPrefix(string itemKey)
    {
        if (string.IsNullOrWhiteSpace(itemKey))
            return false;

        var upperKey = itemKey.ToUpperInvariant();
        return ExcludedPrefixes.Any(prefix =>
            upperKey.StartsWith(prefix.ToUpperInvariant()));
    }
}
```

### 3.2 Font Size Calculator

**File**: `Helpers/FontSizeCalculator.cs`

```csharp
public static class FontSizeCalculator
{
    /// <summary>
    /// Calculate optimal font size to fit text within bounds
    /// </summary>
    public static double CalculateOptimalFontSize(
        string text,
        double maxWidthMm,
        double maxHeightMm,
        double initialFontSize,
        string fontFamily = "Arial")
    {
        if (string.IsNullOrWhiteSpace(text))
            return initialFontSize;

        // Convert mm to pixels (approximate)
        double maxWidthPx = maxWidthMm * 3.7795; // ~96 DPI
        double maxHeightPx = maxHeightMm * 3.7795;

        double fontSize = initialFontSize;

        while (fontSize > 6) // Minimum readable size
        {
            var size = MeasureText(text, fontSize, fontFamily);

            if (size.Width <= maxWidthPx && size.Height <= maxHeightPx)
                return fontSize;

            fontSize -= 0.5;
        }

        return 6; // Minimum
    }

    /// <summary>
    /// Split text into multiple lines if too long
    /// </summary>
    public static List<string> SplitTextToFit(
        string text,
        double maxWidthMm,
        double fontSize,
        string fontFamily = "Arial")
    {
        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine)
                ? word
                : $"{currentLine} {word}";

            var size = MeasureText(testLine, fontSize, fontFamily);
            double maxWidthPx = maxWidthMm * 3.7795;

            if (size.Width > maxWidthPx && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
            lines.Add(currentLine);

        return lines;
    }

    private static Size MeasureText(string text, double fontSize, string fontFamily)
    {
        var typeface = new Typeface(fontFamily);
        var formattedText = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black,
            new NumberSubstitution(),
            VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);

        return new Size(formattedText.Width, formattedText.Height);
    }
}
```

---

## Phase 4: Database Services

### 4.1 Part Data Service

**File**: `Services/Database/IPartDataService.cs`

```csharp
public interface IPartDataService
{
    Task<PartInfo?> GetPartByItemKeyAsync(string itemKey);
    Task<List<PartInfo>> SearchPartsAsync(string searchTerm);
}

public class PartInfo
{
    public string ItemKey { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string? HebrewDescription { get; set; }
    public string? ArabicDescription { get; set; }
    public string? Category { get; set; }
}
```

**File**: `Services/Database/PartDataService.cs`

```csharp
public class PartDataService : IPartDataService
{
    private readonly string _connectionString;

    public PartDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<PartInfo?> GetPartByItemKeyAsync(string itemKey)
    {
        const string sql = @"
            SELECT TOP 1
                PartNumber AS ItemKey,
                PartName,
                CustomDescription AS HebrewDescription,
                ArabicDescription
            FROM dbo.vw_Parts
            WHERE PartNumber = @ItemKey AND IsActive = 1";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var result = await connection.QuerySingleOrDefaultAsync<PartInfo>(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );

        return result;
    }

    public async Task<List<PartInfo>> SearchPartsAsync(string searchTerm)
    {
        const string sql = @"
            SELECT TOP 100
                PartNumber AS ItemKey,
                PartName,
                CustomDescription AS HebrewDescription,
                ArabicDescription,
                Category
            FROM dbo.vw_Parts
            WHERE IsActive = 1
                AND (PartNumber LIKE @Search OR PartName LIKE @Search)
            ORDER BY PartNumber";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<PartInfo>(
            sql,
            new { Search = $"%{searchTerm}%" },
            commandTimeout: 30
        );

        return results.ToList();
    }
}
```

### 4.2 Arabic Description Service

**File**: `Services/Database/IArabicDescriptionService.cs`

```csharp
public interface IArabicDescriptionService
{
    Task<string?> GetArabicDescriptionAsync(string itemKey);
    Task SaveArabicDescriptionAsync(string itemKey, string description, string userName);
    Task DeleteArabicDescriptionAsync(string itemKey);
}
```

**File**: `Services/Database/ArabicDescriptionService.cs`

```csharp
public class ArabicDescriptionService : IArabicDescriptionService
{
    private readonly string _connectionString;

    public ArabicDescriptionService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string?> GetArabicDescriptionAsync(string itemKey)
    {
        const string sql = @"
            SELECT ArabicDescription
            FROM dbo.ArabicPartDescriptions
            WHERE ItemKey = @ItemKey AND IsActive = 1";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleOrDefaultAsync<string>(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );
    }

    public async Task SaveArabicDescriptionAsync(string itemKey, string description, string userName)
    {
        const string sql = @"
            MERGE dbo.ArabicPartDescriptions AS target
            USING (SELECT @ItemKey AS ItemKey) AS source
            ON target.ItemKey = source.ItemKey
            WHEN MATCHED THEN
                UPDATE SET
                    ArabicDescription = @Description,
                    UpdatedAt = GETDATE(),
                    UpdatedBy = @UserName,
                    IsActive = 1
            WHEN NOT MATCHED THEN
                INSERT (ItemKey, ArabicDescription, IsActive, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
                VALUES (@ItemKey, @Description, 1, GETDATE(), GETDATE(), @UserName, @UserName);";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            sql,
            new { ItemKey = itemKey, Description = description, UserName = userName },
            commandTimeout: 30
        );
    }

    public async Task DeleteArabicDescriptionAsync(string itemKey)
    {
        const string sql = @"
            UPDATE dbo.ArabicPartDescriptions
            SET IsActive = 0, UpdatedAt = GETDATE()
            WHERE ItemKey = @ItemKey";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await connection.ExecuteAsync(
            sql,
            new { ItemKey = itemKey },
            commandTimeout: 30
        );
    }
}
```

### 4.3 Stock Data Service

**File**: `Services/Database/IStockDataService.cs`

```csharp
public interface IStockDataService
{
    Task<StockInfo?> GetStockInfoAsync(int stockId);
    Task<List<StockMoveItem>> GetStockMovesAsync(int stockId);
}
```

**File**: `Services/Database/StockDataService.cs`

```csharp
public class StockDataService : IStockDataService
{
    private readonly string _connectionString;

    public StockDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<StockInfo?> GetStockInfoAsync(int stockId)
    {
        const string sql = @"
            SELECT TOP 1
                ID AS StockId,
                AccountName,
                AccountKey,
                ValueDate,
                Remarks
            FROM SH2013.dbo.Stock
            WHERE ID = @StockId";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        return await connection.QuerySingleOrDefaultAsync<StockInfo>(
            sql,
            new { StockId = stockId },
            commandTimeout: 30
        );
    }

    public async Task<List<StockMoveItem>> GetStockMovesAsync(int stockId)
    {
        const string sql = @"
            SELECT
                ItemKey,
                SUM(Quantity) AS TotalQuantity
            FROM SH2013.dbo.StockMoves
            WHERE StockID = @StockID
            GROUP BY ItemKey
            ORDER BY ItemKey";

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var results = await connection.QueryAsync<StockMoveItem>(
            sql,
            new { StockID = stockId },
            commandTimeout: 60
        );

        return results.ToList();
    }
}

public class StockMoveItem
{
    public string ItemKey { get; set; } = string.Empty;
    public double TotalQuantity { get; set; }
}
```

---

## Phase 5: TSC Printer Service

### 5.1 Printer Service Interface

**File**: `Services/Printing/ITSCPrinterService.cs`

```csharp
public interface ITSCPrinterService
{
    Task<List<PrinterInfo>> GetAvailablePrintersAsync();
    Task<PrinterInfo> GetPrinterStatusAsync(string printerName);
    Task<string> GenerateTSPLAsync(PrintJob job);
    Task PrintLabelAsync(string tspl, string printerName);
    Task PrintMultipleLabelsAsync(List<PrintJob> jobs, string printerName);
}
```

### 5.2 TSC Printer Service Implementation

**File**: `Services/Printing/TSCPrinterService.cs`

```csharp
public class TSCPrinterService : ITSCPrinterService
{
    public async Task<List<PrinterInfo>> GetAvailablePrintersAsync()
    {
        return await Task.Run(() =>
        {
            var printers = PrinterSettings.InstalledPrinters;
            var result = new List<PrinterInfo>();

            foreach (string printerName in printers)
            {
                // Filter for TSC printers (or return all)
                if (printerName.Contains("TSC", StringComparison.OrdinalIgnoreCase) ||
                    printerName.Contains("TTP", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(new PrinterInfo
                    {
                        Name = printerName,
                        Status = PrinterStatus.Unknown,
                        StatusMessage = "Not checked"
                    });
                }
            }

            return result;
        });
    }

    public async Task<PrinterInfo> GetPrinterStatusAsync(string printerName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var printerSettings = new PrinterSettings { PrinterName = printerName };

                if (!printerSettings.IsValid)
                {
                    return new PrinterInfo
                    {
                        Name = printerName,
                        Status = PrinterStatus.Offline,
                        StatusMessage = "Printer not found"
                    };
                }

                // TODO: Query actual printer status via TSC SDK
                // For now, assume ready if printer exists
                return new PrinterInfo
                {
                    Name = printerName,
                    Status = PrinterStatus.Ready,
                    StatusMessage = "Ready"
                };
            }
            catch (Exception ex)
            {
                return new PrinterInfo
                {
                    Name = printerName,
                    Status = PrinterStatus.Error,
                    StatusMessage = ex.Message
                };
            }
        });
    }

    public async Task<string> GenerateTSPLAsync(PrintJob job)
    {
        return await Task.Run(() =>
        {
            var sb = new StringBuilder();
            var settings = job.Settings;
            var label = job.LabelData;

            // Label size
            int widthDots = (int)(settings.WidthMm * settings.DPI / 25.4);
            int heightDots = (int)(settings.HeightMm * settings.DPI / 25.4);

            sb.AppendLine($"SIZE {settings.WidthMm:F1} mm, {settings.HeightMm:F1} mm");
            sb.AppendLine("GAP 2 mm, 0 mm");
            sb.AppendLine("DIRECTION 1");
            sb.AppendLine("REFERENCE 0,0");
            sb.AppendLine("OFFSET 0 mm");
            sb.AppendLine("SET PEEL OFF");
            sb.AppendLine("SET CUTTER OFF");
            sb.AppendLine("CLS");

            // Line 1: Intro text (top, left-aligned, small)
            sb.AppendLine($"TEXT {job.IntroX},{job.IntroY},\"0\",0,1,1,\"{EscapeTSPL(label.IntroLine)}\"");

            // Line 2: Item Key (center, larger)
            sb.AppendLine($"TEXT {job.ItemKeyX},{job.ItemKeyY},\"0\",0,2,2,\"{EscapeTSPL(label.ItemKey)}\"");

            // Line 3: Description (center, auto-sized, RTL if Arabic/Hebrew)
            if (label.ShouldShowDescription && !string.IsNullOrWhiteSpace(label.Description))
            {
                bool isRTL = label.Language == "ar" || label.Language == "he";

                // Calculate available width for description
                double availableWidthMm = settings.WidthMm - settings.LeftMargin - settings.RightMargin;

                // Split into multiple lines if needed
                var lines = FontSizeCalculator.SplitTextToFit(
                    label.Description,
                    availableWidthMm,
                    label.FontSize,
                    label.FontFamily
                );

                int lineY = job.DescriptionY;
                int lineSpacing = (int)(label.FontSize * 1.2 * settings.DPI / 72); // Convert pt to dots

                foreach (var line in lines)
                {
                    var textToRender = isRTL ? ReverseForRTL(line) : line;

                    if (isRTL)
                    {
                        // Use Hebrew/Arabic font if available
                        sb.AppendLine($"TEXT {job.DescriptionX},{lineY},\"HEBREW.TTF\",0,1,1,\"{EscapeTSPL(textToRender)}\"");
                    }
                    else
                    {
                        sb.AppendLine($"TEXT {job.DescriptionX},{lineY},\"0\",0,1,1,\"{EscapeTSPL(textToRender)}\"");
                    }

                    lineY += lineSpacing;
                }
            }

            sb.AppendLine("PRINT 1,1");

            return sb.ToString();
        });
    }

    public async Task PrintLabelAsync(string tspl, string printerName)
    {
        await Task.Run(() =>
        {
            // TODO: Implement actual printer communication
            // Option 1: Use TSC SDK (if available)
            // TSCLibrary.openport(printerName);
            // TSCLibrary.sendcommand(tspl);
            // TSCLibrary.closeport();

            // Option 2: Send to printer port directly
            // RawPrinterHelper.SendStringToPrinter(printerName, tspl);

            // Option 3: Write to file for testing
            File.WriteAllText($"label_{DateTime.Now:yyyyMMddHHmmss}.tspl", tspl);
        });
    }

    public async Task PrintMultipleLabelsAsync(List<PrintJob> jobs, string printerName)
    {
        foreach (var job in jobs)
        {
            var tspl = await GenerateTSPLAsync(job);

            // Print multiple copies based on quantity
            for (int i = 0; i < job.LabelData.Quantity; i++)
            {
                await PrintLabelAsync(tspl, printerName);
            }
        }
    }

    private string EscapeTSPL(string text)
    {
        // Escape quotes and special characters for TSPL
        return text.Replace("\"", "\\\"");
    }

    private string ReverseForRTL(string text)
    {
        // Simple reversal (may need BiDi algorithm for complex cases)
        return new string(text.Reverse().ToArray());
    }
}
```

---

## Phase 6: Label Render Service

**File**: `Services/Label/ILabelRenderService.cs`

```csharp
public interface ILabelRenderService
{
    LabelData CreateLabelData(string itemKey, PartInfo partInfo, string language);
    void OptimizeFontSize(LabelData labelData, StickerSettings settings);
    BitmapImage GeneratePreviewImage(LabelData labelData, StickerSettings settings);
}
```

**File**: `Services/Label/LabelRenderService.cs`

```csharp
public class LabelRenderService : ILabelRenderService
{
    public LabelData CreateLabelData(string itemKey, PartInfo partInfo, string language)
    {
        var labelData = new LabelData
        {
            ItemKey = itemKey,
            Language = language
        };

        // Determine description based on language
        if (language == "ar")
        {
            labelData.Description = partInfo.ArabicDescription ?? partInfo.PartName;
        }
        else // Hebrew default
        {
            labelData.Description = partInfo.HebrewDescription ?? partInfo.PartName;
        }

        // Check if description should be hidden
        if (PrefixChecker.HasExcludedPrefix(itemKey))
        {
            labelData.Description = string.Empty;
        }

        return labelData;
    }

    public void OptimizeFontSize(LabelData labelData, StickerSettings settings)
    {
        if (!labelData.ShouldShowDescription || string.IsNullOrWhiteSpace(labelData.Description))
            return;

        // Calculate available space
        double availableWidth = settings.WidthMm - settings.LeftMargin - settings.RightMargin;
        double availableHeight = settings.HeightMm - settings.TopMargin - settings.BottomMargin - 20; // Reserve space for intro + key

        // Calculate optimal font size
        double optimalSize = FontSizeCalculator.CalculateOptimalFontSize(
            labelData.Description,
            availableWidth,
            availableHeight,
            settings.DescriptionFontSize,
            labelData.FontFamily
        );

        labelData.FontSize = optimalSize;
    }

    public BitmapImage GeneratePreviewImage(LabelData labelData, StickerSettings settings)
    {
        // TODO: Generate actual preview bitmap
        // For now, return placeholder
        throw new NotImplementedException("Preview generation to be implemented");
    }
}
```

---

## Phase 7: ViewModels

### 7.1 Main ViewModel

**File**: `ViewModels/MainViewModel.cs`

```csharp
public class MainViewModel : INotifyPropertyChanged
{
    private readonly ITSCPrinterService _printerService;
    private string _selectedPrinter = string.Empty;
    private PrinterInfo? _printerStatus;
    private int _selectedTabIndex = 0;

    public PrintOnDemandViewModel PrintOnDemandVM { get; }
    public StockMoveViewModel StockMoveVM { get; }

    public ObservableCollection<string> AvailablePrinters { get; } = new();

    public string SelectedPrinter
    {
        get => _selectedPrinter;
        set
        {
            if (_selectedPrinter != value)
            {
                _selectedPrinter = value;
                OnPropertyChanged();
                _ = UpdatePrinterStatusAsync();
            }
        }
    }

    public PrinterInfo? PrinterStatus
    {
        get => _printerStatus;
        set { _printerStatus = value; OnPropertyChanged(); }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set { _selectedTabIndex = value; OnPropertyChanged(); }
    }

    public AsyncRelayCommand RefreshPrintersCommand { get; }
    public AsyncRelayCommand CheckPrinterStatusCommand { get; }

    public MainViewModel(
        PrintOnDemandViewModel printOnDemandVM,
        StockMoveViewModel stockMoveVM,
        ITSCPrinterService printerService)
    {
        PrintOnDemandVM = printOnDemandVM;
        StockMoveVM = stockMoveVM;
        _printerService = printerService;

        RefreshPrintersCommand = new AsyncRelayCommand(_ => LoadPrintersAsync());
        CheckPrinterStatusCommand = new AsyncRelayCommand(_ => UpdatePrinterStatusAsync());

        // Load printers on startup
        _ = LoadPrintersAsync();
    }

    private async Task LoadPrintersAsync()
    {
        try
        {
            var printers = await _printerService.GetAvailablePrintersAsync();

            AvailablePrinters.Clear();
            foreach (var printer in printers)
            {
                AvailablePrinters.Add(printer.Name);
            }

            if (AvailablePrinters.Any() && string.IsNullOrEmpty(SelectedPrinter))
            {
                SelectedPrinter = AvailablePrinters.First();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load printers: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task UpdatePrinterStatusAsync()
    {
        if (string.IsNullOrEmpty(SelectedPrinter))
            return;

        try
        {
            PrinterStatus = await _printerService.GetPrinterStatusAsync(SelectedPrinter);
        }
        catch (Exception ex)
        {
            PrinterStatus = new PrinterInfo
            {
                Name = SelectedPrinter,
                Status = PrinterStatus.Error,
                StatusMessage = ex.Message
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

### 7.2 Print On Demand ViewModel

**File**: `ViewModels/PrintOnDemandViewModel.cs`

```csharp
public class PrintOnDemandViewModel : INotifyPropertyChanged
{
    private readonly IPartDataService _partDataService;
    private readonly IArabicDescriptionService _arabicDescService;
    private readonly ITSCPrinterService _printerService;
    private readonly ILabelRenderService _labelRenderService;

    private string _itemKey = string.Empty;
    private string _selectedLanguage = "he";
    private LabelData? _currentLabel;
    private bool _isLoading = false;
    private string _statusMessage = string.Empty;

    public string ItemKey
    {
        get => _itemKey;
        set
        {
            if (_itemKey != value)
            {
                _itemKey = value;
                OnPropertyChanged();
                LoadItemCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage != value)
            {
                _selectedLanguage = value;
                OnPropertyChanged();
                _ = LoadItemAsync();
            }
        }
    }

    public LabelData? CurrentLabel
    {
        get => _currentLabel;
        set { _currentLabel = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            OnPropertyChanged();
            LoadItemCommand.RaiseCanExecuteChanged();
            PrintCommand.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsArabicMode => SelectedLanguage == "ar";
    public bool CanEditArabicDescription => IsArabicMode && CurrentLabel != null;

    public AsyncRelayCommand LoadItemCommand { get; }
    public AsyncRelayCommand PrintCommand { get; }
    public AsyncRelayCommand EditArabicCommand { get; }

    public PrintOnDemandViewModel(
        IPartDataService partDataService,
        IArabicDescriptionService arabicDescService,
        ITSCPrinterService printerService,
        ILabelRenderService labelRenderService)
    {
        _partDataService = partDataService;
        _arabicDescService = arabicDescService;
        _printerService = printerService;
        _labelRenderService = labelRenderService;

        LoadItemCommand = new AsyncRelayCommand(_ => LoadItemAsync(), _ => CanLoadItem());
        PrintCommand = new AsyncRelayCommand(_ => PrintAsync(), _ => CanPrint());
        EditArabicCommand = new AsyncRelayCommand(_ => EditArabicDescriptionAsync(), _ => CanEditArabicDescription);
    }

    private bool CanLoadItem() => !string.IsNullOrWhiteSpace(ItemKey) && !IsLoading;
    private bool CanPrint() => CurrentLabel != null && !IsLoading;

    private async Task LoadItemAsync()
    {
        if (string.IsNullOrWhiteSpace(ItemKey))
            return;

        IsLoading = true;
        StatusMessage = "Loading part...";

        try
        {
            var partInfo = await _partDataService.GetPartByItemKeyAsync(ItemKey);

            if (partInfo == null)
            {
                MessageBox.Show($"Part not found: {ItemKey}", "Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StatusMessage = "Part not found";
                return;
            }

            // Create label data
            CurrentLabel = _labelRenderService.CreateLabelData(ItemKey, partInfo, SelectedLanguage);

            // Optimize font size
            var settings = new StickerSettings(); // TODO: Load from config
            _labelRenderService.OptimizeFontSize(CurrentLabel, settings);

            StatusMessage = "Ready to print";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading part: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Error occurred";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PrintAsync()
    {
        if (CurrentLabel == null)
            return;

        IsLoading = true;
        StatusMessage = "Printing...";

        try
        {
            var settings = new StickerSettings(); // TODO: Load from config
            var printJob = new PrintJob
            {
                LabelData = CurrentLabel,
                Settings = settings
            };

            var tspl = await _printerService.GenerateTSPLAsync(printJob);

            // Print specified quantity
            for (int i = 0; i < CurrentLabel.Quantity; i++)
            {
                await _printerService.PrintLabelAsync(tspl, settings.PrinterName);
            }

            MessageBox.Show($"Printed {CurrentLabel.Quantity} label(s) successfully", "Success",
                MessageBoxButton.OK, MessageBoxImage.Information);
            StatusMessage = "Print complete";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Print failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Print failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task EditArabicDescriptionAsync()
    {
        if (CurrentLabel == null || !IsArabicMode)
            return;

        // TODO: Show dialog to edit Arabic description
        // var dialog = new EditArabicDialog(ItemKey, CurrentLabel.Description);
        // if (dialog.ShowDialog() == true)
        // {
        //     await _arabicDescService.SaveArabicDescriptionAsync(
        //         ItemKey,
        //         dialog.UpdatedDescription,
        //         Environment.UserName);
        //
        //     CurrentLabel.Description = dialog.UpdatedDescription;
        //     StatusMessage = "Arabic description saved";
        // }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
```

---

## Implementation Order

### Step 1: Project Setup (Week 1)
1. Create WPF project with proper structure
2. Add NuGet packages (Dapper, SqlClient, System.Drawing)
3. Add project reference to Entities
4. Setup App.xaml.cs with DI

### Step 2: Models & Helpers (Week 1)
1. Create all model classes (LabelData, PrintJob, StickerSettings, etc.)
2. Implement PrefixChecker helper
3. Implement FontSizeCalculator helper
4. Add Commands (RelayCommand, AsyncRelayCommand)

### Step 3: Database Services (Week 2)
1. Implement PartDataService
2. Implement ArabicDescriptionService
3. Implement StockDataService
4. Test database connections

### Step 4: Printer Service Skeleton (Week 2)
1. Implement TSCPrinterService interface
2. Add printer discovery logic
3. Implement TSPL generation
4. Add file output for testing (before actual printer)

### Step 5: ViewModels (Week 3)
1. Implement MainViewModel (printer management, tab switching)
2. Implement PrintOnDemandViewModel
3. Implement StockMoveViewModel (basic structure)
4. Wire up commands

### Step 6: UI - Print on Demand (Week 3-4)
1. Create MainWindow with TabControl
2. Create Print on Demand tab UI
3. Implement label preview control
4. Add language toggle, quantity input
5. Test with file output

### Step 7: UI - Stock Move Mode (Week 4)
1. Create Stock Move tab UI
2. Implement list of labels with preview
3. Add bulk language toggle
4. Add/remove label functionality
5. Test with stock data

### Step 8: TSC Printer Integration (Week 5)
1. Research TSC SDK or printer communication method
2. Implement actual printer communication
3. Test with real TSC printer
4. Handle RTL text rendering
5. Test font sizing and multi-line

### Step 9: Arabic Description Editing (Week 5)
1. Create EditArabicDialog
2. Wire up to database save
3. Test persistence

### Step 10: Polish & Testing (Week 6)
1. Add global settings (default intro line)
2. Implement preview image generation
3. Add error handling throughout
4. Test with real data and printer
5. User documentation

---

## Critical Files Summary

### New Files (Sticker Printing App)
- **Project**: `Sh.Autofit.StickerPrinting.csproj`
- **Models**: LabelData.cs, PrintJob.cs, StickerSettings.cs, StockInfo.cs, PrinterInfo.cs
- **Helpers**: PrefixChecker.cs, FontSizeCalculator.cs
- **Services**: PartDataService.cs, ArabicDescriptionService.cs, StockDataService.cs, TSCPrinterService.cs, LabelRenderService.cs
- **ViewModels**: MainViewModel.cs, PrintOnDemandViewModel.cs, StockMoveViewModel.cs
- **Views**: MainWindow.xaml, LabelPreviewControl.xaml, EditArabicDialog.xaml

### Depends On (Existing)
- `Sh.Autofit.New.Entities` project (for ShAutofitContext, VwPart, ArabicPartDescription)
- Database: `Sh.Autofit` (vw_Parts view, ArabicPartDescriptions table)

---

## Success Criteria

✅ **Mode 1 - Print on Demand**
- Load part by ItemKey
- Toggle Hebrew/Arabic language
- Preview shows intro, key, description (or key only for excluded prefixes)
- Edit Arabic description (persists to DB)
- Quantity selector works
- Print to TSC printer

✅ **Mode 2 - Stock Move**
- Load stock by StockID, show AccountName
- Display list of labels from StockMoves
- Toggle all labels Hebrew/Arabic
- Edit individual label preview and quantity
- Add/remove labels
- Print all labels

✅ **Printer Management**
- Show connected printer
- Change printer from dropdown
- Display printer status
- Handle printer errors gracefully

✅ **Text Rendering**
- Auto-fit text to label dimensions
- Multi-line for long descriptions
- RTL support for Hebrew/Arabic
- Respect prefix exclusion rules

✅ **Customization**
- Edit intro line per-label or globally
- Change font size
- Preview updates in real-time
