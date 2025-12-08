# Stock Export - WizCount/H-ERP Excel Generator

A production-quality WPF application for exporting stock moves from the SH2013 database to Excel files formatted for WizCount/H-ERP import.

## Features

- ✅ Query stock moves from SH2013.dbo.StockMoves table
- ✅ Aggregate quantities by ItemKey
- ✅ Export to Excel with WizCount/H-ERP format
- ✅ Document type selection in WPF application
- ✅ Named range for the entire data table
- ✅ Quantity always exported as negative values
- ✅ ItemKey preserved as text format
- ✅ Async database operations
- ✅ Comprehensive input validation
- ✅ Clean MVVM architecture
- ✅ READ-ONLY database access (no modifications to database)

## Architecture

```
Sh.Autofit.StockExport/
├── Models/
│   ├── StockMoveItem.cs          # Database result model
│   ├── DocumentType.cs            # Document type dropdown model
│   └── ExportSettings.cs          # Export configuration model
├── Services/
│   ├── Database/
│   │   ├── IStockMovesService.cs
│   │   └── StockMovesService.cs   # READ-ONLY Dapper service
│   └── Excel/
│       ├── IExcelExportService.cs
│       └── ExcelExportService.cs   # ClosedXML export service
├── ViewModels/
│   └── MainViewModel.cs            # Main view model with business logic
├── Views/
│   ├── MainWindow.xaml             # Main UI
│   ├── MainWindow.xaml.cs
│   └── InverseBooleanConverter.cs
├── Commands/
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
├── App.xaml
├── App.xaml.cs                     # Dependency injection setup
└── Sh.Autofit.StockExport.csproj
```

## Technologies Used

- **.NET 8.0** - Target framework
- **WPF** - User interface framework
- **MVVM Pattern** - Clean separation of concerns
- **Dapper** - Lightweight ORM for database access
- **ClosedXML** - Excel file generation
- **System.Data.SqlClient** - SQL Server connectivity

## Prerequisites

- .NET 8.0 SDK or later
- Windows OS (WPF requirement)
- Access to SH2013 database (via Sh.Autofit catalog)
- Visual Studio 2022 or later (recommended)

## Database Requirements

### Connection String
The application uses the same connection string as PartsMappingUI:
```
Data Source=server-pc\wizsoft2;
Initial Catalog=Sh.Autofit;
User ID=issa;
Password=5060977Ih;
Encrypt=False;
TrustServerCertificate=True
```

### Database Access
- **Database**: SH2013
- **Table**: dbo.StockMoves
- **Required Fields**:
  - StockID (int)
  - ItemKey (varchar(20))
  - Quantity (float)
- **Access Level**: READ-ONLY (no write operations)

### SQL Query
```sql
SELECT ItemKey, SUM(Quantity) AS TotalQuantity
FROM SH2013.dbo.StockMoves
WHERE StockID = @StockID
GROUP BY ItemKey
ORDER BY ItemKey
```

## Installation & Compilation

### Method 1: Using Visual Studio

1. **Open the solution:**
   ```
   Open Sh.Autofit.New.sln in Visual Studio
   ```

2. **Add the project to the solution:**
   - Right-click solution → Add → Existing Project
   - Navigate to `Sh.Autofit.StockExport\Sh.Autofit.StockExport.csproj`
   - Click "Open"

3. **Restore NuGet packages:**
   ```
   Right-click solution → Restore NuGet Packages
   ```
   Or use Package Manager Console:
   ```powershell
   dotnet restore
   ```

4. **Build the project:**
   - Right-click on `Sh.Autofit.StockExport` project
   - Select "Build"

   Or use keyboard shortcut: `Ctrl+Shift+B`

5. **Run the application:**
   - Right-click on `Sh.Autofit.StockExport` project
   - Select "Set as Startup Project"
   - Press `F5` to run with debugging
   - Or `Ctrl+F5` to run without debugging

### Method 2: Using Command Line

1. **Navigate to the project directory:**
   ```cmd
   cd C:\Users\ASUS\source\repos\Sh.Autofit.New\Sh.Autofit.StockExport
   ```

2. **Restore dependencies:**
   ```cmd
   dotnet restore
   ```

3. **Build the project:**
   ```cmd
   dotnet build --configuration Release
   ```

4. **Run the application:**
   ```cmd
   dotnet run
   ```

### Method 3: Build Executable

To create a standalone executable:

```cmd
cd C:\Users\ASUS\source\repos\Sh.Autofit.New\Sh.Autofit.StockExport
dotnet publish --configuration Release --output ./publish
```

The executable will be created in the `publish` folder:
```
Sh.Autofit.StockExport\publish\Sh.Autofit.StockExport.exe
```

## Usage Instructions

### Step 1: Enter Stock ID
- Enter the Stock ID (מספר מזהה) you want to export
- Must be a positive integer
- Example: `1001`

### Step 2: Select Document Type
- Choose from the dropdown (currently only "יתרת פתיחה" with value 24)
- This will be Column 1 (DocType) in the Excel file

### Step 3: Enter Description
- Enter the description text for Column 2
- This is a free text field
- Example: `Opening Balance January 2024`

### Step 4: Enter Document Number
- Enter the document number for Column 3
- Must be a positive integer
- Example: `1001`

### Step 5: Select Save Path
- Click the "Browse..." button
- Choose where to save the Excel file
- Default filename format: `StockExport_YYYYMMDD_HHMMSS.xlsx`

### Step 6: Export
- Click "Export to Excel" button
- The application will:
  1. Query the database for the specified StockID
  2. Aggregate quantities by ItemKey
  3. Generate the Excel file with proper formatting
  4. Show a success message with the count of exported items

## Excel Output Format

### Worksheet Structure
- **Worksheet Name**: "Data"
- **Named Range**: "StockOpening" (covers entire table including header)

### Column Layout

| Column | Name | Type | Source | Notes |
|--------|------|------|--------|-------|
| A | DocType | Number | User selection (24) | Plain numeric value |
| B | Description | Text | User input | Free text |
| C | DocNumber | Number | User input | Positive integer |
| D | ItemKey | Text | Database | **MUST be text format** |
| E | Quantity | Number | Database (negated) | **ALWAYS negative** |

### Critical Business Rules

1. **Quantity is ALWAYS negative**: The value from the database is multiplied by -1
2. **ItemKey is ALWAYS text**: Formatted with `@` to prevent Excel auto-conversion
3. **DocType is numeric**: Column A contains the plain numeric value (e.g., 24)
4. **Named Range**: The range "StockOpening" covers A1:E{lastRow}

### Example Output

```
| DocType | Description          | DocNumber | ItemKey | Quantity |
|---------|---------------------|-----------|---------|----------|
| 24      | Opening Balance     | 1001      | ABC123  | -50.00   |
| 24      | Opening Balance     | 1001      | DEF456  | -100.00  |
| 24      | Opening Balance     | 1001      | GHI789  | -25.50   |
```

## Error Handling

The application handles various error scenarios:

### Input Validation Errors
- Empty Stock ID → "Please enter a Stock ID"
- Non-numeric Stock ID → "Stock ID must be a positive number"
- Empty Description → "Please enter a description"
- Empty/Invalid Doc Number → "Document number must be a positive number"
- No save path selected → "Please select a save path"

### Database Errors
- Connection failure → "Failed to retrieve data from database"
- No records found → "No stock moves found for Stock ID: {id}"
- Query timeout → Handled by 60-second timeout with error message

### Excel Export Errors
- File write failure → "Failed to export Excel file"
- File in use → Error message with details
- Insufficient permissions → Error message with details

## Project Structure Details

### Models Layer
- **StockMoveItem**: Represents aggregated database results
- **DocumentType**: Dropdown option model
- **ExportSettings**: Export configuration container

### Services Layer
- **StockMovesService**: READ-ONLY database access using Dapper
- **ExcelExportService**: Excel file generation using ClosedXML

### ViewModels Layer
- **MainViewModel**: Business logic, validation, and UI state management
- Implements INotifyPropertyChanged for data binding
- Async command execution with proper error handling

### Commands Layer
- **RelayCommand**: Synchronous command implementation
- **AsyncRelayCommand**: Async command with execution state tracking

## NuGet Packages

```xml
<!-- Database Access -->
<PackageReference Include="Dapper" Version="2.1.35" />
<PackageReference Include="System.Data.SqlClient" Version="4.8.6" />

<!-- Excel Generation -->
<PackageReference Include="ClosedXML" Version="0.102.3" />
```

## Security Considerations

- ✅ READ-ONLY database access (no INSERT, UPDATE, DELETE operations)
- ✅ SQL injection prevention through parameterized queries
- ✅ Input validation before database queries
- ✅ Connection string stored in code (consider moving to secure configuration)

## Troubleshooting

### Issue: "Could not connect to database"
**Solution**: Verify the connection string and ensure access to server-pc\wizsoft2

### Issue: "No records found"
**Solution**: Verify that the StockID exists in SH2013.dbo.StockMoves table

### Issue: "Failed to export Excel file"
**Solution**:
- Check if the file is already open in Excel
- Verify write permissions to the target directory
- Ensure sufficient disk space

### Issue: "ItemKey appears as number in Excel"
**Solution**: The application forces text format with `@` - if still appearing as number, check ClosedXML version

### Issue: "Quantity is not negative"
**Solution**: This is a critical bug - verify ExcelExportService.cs line with `item.TotalQuantity * -1`

## Adding to Solution File

To add this project to your existing Sh.Autofit.New solution:

```cmd
cd C:\Users\ASUS\source\repos\Sh.Autofit.New
dotnet sln add Sh.Autofit.StockExport\Sh.Autofit.StockExport.csproj
```

## Future Enhancements

Potential improvements for future versions:
- [ ] Multiple document type support
- [ ] Configuration file for connection string
- [ ] Export history/logging
- [ ] Batch export for multiple StockIDs
- [ ] Custom date range filtering
- [ ] Export to multiple formats (CSV, XML)
- [ ] Print preview functionality

## Support

For issues or questions, please contact the development team.

## License

Internal use only - Sh.Autofit

## Version History

### Version 1.0.0 (2025-11-27)
- Initial release
- Core export functionality
- WizCount/H-ERP format compliance
- READ-ONLY database access
- Comprehensive error handling
- Full MVVM implementation
