# Quick Start Guide - Stock Export Application

## 🚀 Build and Run (3 Steps)

### Step 1: Build the Project
```cmd
cd C:\Users\ASUS\source\repos\Sh.Autofit.New\Sh.Autofit.StockExport
dotnet build --configuration Release
```

### Step 2: Run the Application
```cmd
dotnet run
```

### Step 3: Use the Application
1. Enter a **Stock ID** (e.g., 1001)
2. Select **Document Type**: יתרת פתיחה (24)
3. Enter **Description** (e.g., "Opening Balance")
4. Enter **Document Number** (e.g., 1001)
5. Click **Browse** to select save location
6. Click **Export to Excel**

---

## 📦 Alternative: Run from Visual Studio

1. Open `Sh.Autofit.New.sln` in Visual Studio
2. Right-click `Sh.Autofit.StockExport` → Set as Startup Project
3. Press **F5** (or Ctrl+F5 for no debugging)

---

## 📋 What the Application Does

1. **Queries** SH2013.dbo.StockMoves for the specified StockID
2. **Aggregates** quantities by ItemKey (SUM)
3. **Generates** an Excel file with WizCount/H-ERP format:
   - Column A: DocType (with dropdown: 24)
   - Column B: Description (your input)
   - Column C: DocNumber (your input)
   - Column D: ItemKey (as TEXT)
   - Column E: Quantity (NEGATIVE values)
4. **Creates** a named range "StockOpening" for the entire table

---

## ✅ Example Usage

### Input
- Stock ID: `1001`
- Document Type: `יתרת פתיחה` (24)
- Description: `Opening Balance January 2025`
- Doc Number: `1001`
- Save Path: `C:\Exports\Stock_1001.xlsx`

### Output Excel File
```
| DocType | Description                    | DocNumber | ItemKey | Quantity |
|---------|-------------------------------|-----------|---------|----------|
| 24      | Opening Balance January 2025  | 1001      | ABC123  | -50.00   |
| 24      | Opening Balance January 2025  | 1001      | DEF456  | -100.00  |
| 24      | Opening Balance January 2025  | 1001      | GHI789  | -25.50   |
```

**Named Range**: "StockOpening" = A1:E4

---

## 🔧 Troubleshooting

### Problem: Build fails
**Solution**: Ensure .NET 8.0 SDK is installed
```cmd
dotnet --version
# Should show 8.0.x or later
```

### Problem: "Could not connect to database"
**Solution**:
- Verify network access to `server-pc\wizsoft2`
- Check SQL Server is running
- Confirm credentials in App.xaml.cs

### Problem: "No records found"
**Solution**: Verify StockID exists in database:
```sql
SELECT COUNT(*) FROM SH2013.dbo.StockMoves WHERE StockID = 1001
```

### Problem: Excel file won't open
**Solution**:
- Close Excel if file is already open
- Check write permissions to target folder
- Ensure sufficient disk space

---

## 📚 Documentation

- **Full Documentation**: [README.md](README.md)
- **Project Structure**: [PROJECT_STRUCTURE.md](PROJECT_STRUCTURE.md)

---

## 🎯 Critical Business Rules

1. ✅ **Quantity is ALWAYS negative** → Database value × -1
2. ✅ **ItemKey is ALWAYS text** → Formatted with @ to prevent number conversion
3. ✅ **DocType has dropdown** → Column A validation with value "24"
4. ✅ **Named range required** → "StockOpening" covering entire table
5. ✅ **READ-ONLY database** → No modifications to database

---

## 📞 Need Help?

Contact the development team for support.

**Version**: 1.0.0
**Status**: ✅ Production Ready
**Build**: ✅ Passing
