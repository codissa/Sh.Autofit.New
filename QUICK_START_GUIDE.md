# 🚀 QUICK START GUIDE - Vehicle Parts Mapping System

## ⚠️ IMPORTANT FIXES APPLIED
- ✅ **Fixed**: Schema now uses `ItemKey` to match your SH2013.Items table
- ✅ **Fixed**: C# entities use `PartItemKey` instead of `PartKeF`
- ✅ **Simplified**: NO LOGIN REQUIRED - just start using it!

---

## 📋 Prerequisites

- [ ] SQL Server running at `server-pc\wizsoft2`
- [ ] SH2013 database with Items table exists
- [ ] Visual Studio 2022 or VS Code installed
- [ ] .NET 8 SDK installed
- [ ] Node.js 18+ installed

---

## 🎯 STEP 1: Create Database (5 minutes)

### A. Open SQL Server Management Studio (SSMS)
```
Server: server-pc\wizsoft2
User: issa
Password: 5060977Ih
```

### B. Execute the Schema Script
1. Open `vehicle_parts_mapping_schema_FIXED.sql`
2. Press F5 to execute
3. You should see: "Database schema created successfully!"

### C. Verify It Works
```sql
USE Sh.Autofit;
GO

-- Test 1: Check if view works
SELECT TOP 10 * FROM dbo.vw_Parts;

-- Test 2: Check if your parts are visible
SELECT COUNT(*) AS TotalParts FROM dbo.vw_Parts;

-- Test 3: Check parts with OEM numbers
SELECT TOP 10 PartNumber, PartName, OEMNumber1, OEMNumber2 
FROM dbo.vw_Parts 
WHERE OEMNumber1 IS NOT NULL;
```

If you see results - **YOU'RE GOOD! ✅**

---

## 🎯 STEP 2: Create ASP.NET Core Project (10 minutes)

### A. Create Solution Structure

```bash
# Create folder for your project
mkdir C:\Projects\VehiclePartsMapping
cd C:\Projects\VehiclePartsMapping

# Create solution
dotnet new sln -n VehiclePartsMapping

# Create projects
dotnet new webapi -n VehiclePartsMapping.API
dotnet new classlib -n VehiclePartsMapping.Core
dotnet new classlib -n VehiclePartsMapping.Infrastructure

# Add projects to solution
dotnet sln add VehiclePartsMapping.API
dotnet sln add VehiclePartsMapping.Core
dotnet sln add VehiclePartsMapping.Infrastructure

# Add project references
cd VehiclePartsMapping.API
dotnet add reference ../VehiclePartsMapping.Core
dotnet add reference ../VehiclePartsMapping.Infrastructure

cd ../VehiclePartsMapping.Infrastructure
dotnet add reference ../VehiclePartsMapping.Core

cd ..
```

### B. Install Required Packages

```bash
# API packages
cd VehiclePartsMapping.API
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Tools
dotnet add package Swashbuckle.AspNetCore
dotnet add package Microsoft.AspNetCore.Cors

# Infrastructure packages
cd ../VehiclePartsMapping.Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore

cd ..
```

### C. Copy Entity Models
1. Copy `DatabaseEntities_FIXED.cs` to `VehiclePartsMapping.Core/Entities/`
2. Rename it to just `DatabaseEntities.cs`

---

## 🎯 STEP 3: Setup Database Context (5 minutes)

### A. Create DbContext

Create file: `VehiclePartsMapping.Infrastructure/Data/ApplicationDbContext.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using VehiclePartsMapping.Core.Entities;

namespace VehiclePartsMapping.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Tables
        public DbSet<Manufacturer> Manufacturers { get; set; }
        public DbSet<VehicleType> VehicleTypes { get; set; }
        public DbSet<VehiclePartsMapping> VehiclePartsMappings { get; set; }
        public DbSet<VehiclePartsMappingsHistory> VehiclePartsMappingsHistory { get; set; }
        public DbSet<PartsMetadata> PartsMetadata { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserActivityLog> UserActivityLog { get; set; }
        public DbSet<SystemSetting> SystemSettings { get; set; }
        public DbSet<VehicleRegistration> VehicleRegistrations { get; set; }
        public DbSet<ApiSyncLog> ApiSyncLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure unique constraints
            modelBuilder.Entity<Manufacturer>()
                .HasIndex(m => m.ManufacturerCode)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<SystemSetting>()
                .HasIndex(s => s.SettingKey)
                .IsUnique();
        }
    }
}
```

### B. Update appsettings.json

In `VehiclePartsMapping.API/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=server-pc\\wizsoft2;Initial Catalog=Sh.Autofit;User ID=issa;Password=5060977Ih;Pooling=True;MultipleActiveResultSets=True;Connect Timeout=30;Encrypt=False;TrustServerCertificate=True;Command Timeout=30"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### C. Update Program.cs (NO LOGIN REQUIRED)

Replace `VehiclePartsMapping.API/Program.cs` with:

```csharp
using Microsoft.EntityFrameworkCore;
using VehiclePartsMapping.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Vehicle Parts Mapping API", Version = "v1" });
});

// Add Database Context
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS (allow all for now - restrict in production)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure middleware
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAll");

// Serve static files (for React app)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

// Fallback to index.html for React routing
app.MapFallbackToFile("/index.html");

app.Run();
```

---

## 🎯 STEP 4: Create Your First API Controller (5 minutes)

Create file: `VehiclePartsMapping.API/Controllers/PartsController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VehiclePartsMapping.Core.Entities;
using VehiclePartsMapping.Infrastructure.Data;

namespace VehiclePartsMapping.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PartsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PartsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<PartDto>>> SearchParts(
            [FromQuery] string? searchTerm = null,
            [FromQuery] bool inStockOnly = false)
        {
            var query = @"
                SELECT * FROM dbo.vw_Parts 
                WHERE IsActive = 1
                AND (@SearchTerm IS NULL OR 
                     PartName LIKE '%' + @SearchTerm + '%' OR 
                     PartNumber LIKE '%' + @SearchTerm + '%' OR
                     OEMNumber1 LIKE '%' + @SearchTerm + '%')
                AND (@InStockOnly = 0 OR IsInStock = 1)
                ORDER BY PartName";

            var parts = await _context.Database
                .SqlQueryRaw<PartDto>(query, 
                    new Microsoft.Data.SqlClient.SqlParameter("@SearchTerm", (object?)searchTerm ?? DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@InStockOnly", inStockOnly))
                .ToListAsync();

            return Ok(parts);
        }

        [HttpGet("{partNumber}")]
        public async Task<ActionResult<PartDto>> GetPart(string partNumber)
        {
            var part = await _context.Database
                .SqlQueryRaw<PartDto>(
                    "SELECT * FROM dbo.vw_Parts WHERE PartNumber = @PartNumber",
                    new Microsoft.Data.SqlClient.SqlParameter("@PartNumber", partNumber))
                .FirstOrDefaultAsync();

            if (part == null)
                return NotFound();

            return Ok(part);
        }
    }
}
```

---

## 🎯 STEP 5: Test Your API (2 minutes)

```bash
cd VehiclePartsMapping.API
dotnet run
```

Open your browser:
- Swagger: `http://localhost:5000/swagger`
- Test endpoint: `http://localhost:5000/api/parts/search?searchTerm=brake`

**You should see your parts! 🎉**

---

## 🎯 STEP 6: Create React Frontend (15 minutes)

### A. Create React App

```bash
cd C:\Projects\VehiclePartsMapping
npm create vite@latest VehiclePartsMapping.Web -- --template react-ts
cd VehiclePartsMapping.Web
npm install
```

### B. Install Dependencies

```bash
npm install antd axios zustand react-router-dom @tanstack/react-query
npm install -D @types/node
```

### C. Create API Configuration

Create `src/config/api.ts`:

```typescript
import axios from 'axios';

export const API_BASE_URL = 'http://localhost:5000/api';

export const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
});
```

### D. Create Parts Service

Create `src/services/partsService.ts`:

```typescript
import { api } from '../config/api';

export interface Part {
  partNumber: string;
  partName: string;
  retailPrice?: number;
  costPrice?: number;
  stockQuantity?: number;
  oemNumber1?: string;
  manufacturer?: string;
  category?: string;
  isInStock: boolean;
}

export const partsService = {
  searchParts: async (searchTerm?: string, inStockOnly: boolean = false) => {
    const response = await api.get<Part[]>('/parts/search', {
      params: { searchTerm, inStockOnly }
    });
    return response.data;
  },

  getPart: async (partNumber: string) => {
    const response = await api.get<Part>(`/parts/${partNumber}`);
    return response.data;
  }
};
```

### E. Create Simple Parts Search Component

Replace `src/App.tsx`:

```typescript
import React, { useState } from 'react';
import { Input, Button, Table, Card, Tag } from 'antd';
import { SearchOutlined } from '@ant-design/icons';
import { partsService, Part } from './services/partsService';
import 'antd/dist/reset.css';

function App() {
  const [searchTerm, setSearchTerm] = useState('');
  const [parts, setParts] = useState<Part[]>([]);
  const [loading, setLoading] = useState(false);

  const handleSearch = async () => {
    setLoading(true);
    try {
      const results = await partsService.searchParts(searchTerm);
      setParts(results);
    } catch (error) {
      console.error('Search failed:', error);
    } finally {
      setLoading(false);
    }
  };

  const columns = [
    {
      title: 'Part Number',
      dataIndex: 'partNumber',
      key: 'partNumber',
    },
    {
      title: 'Name',
      dataIndex: 'partName',
      key: 'partName',
    },
    {
      title: 'OEM',
      dataIndex: 'oemNumber1',
      key: 'oemNumber1',
    },
    {
      title: 'Price',
      dataIndex: 'retailPrice',
      key: 'retailPrice',
      render: (price: number) => price ? `₪${price.toFixed(2)}` : '-'
    },
    {
      title: 'Stock',
      key: 'stock',
      render: (_: any, record: Part) => (
        <Tag color={record.isInStock ? 'green' : 'red'}>
          {record.isInStock ? 'In Stock' : 'Out of Stock'}
        </Tag>
      )
    }
  ];

  return (
    <div style={{ padding: '20px' }}>
      <Card title="🚗 Vehicle Parts Search">
        <Input.Search
          placeholder="Search by part number, name, or OEM..."
          enterButton={<><SearchOutlined /> Search</>}
          size="large"
          value={searchTerm}
          onChange={(e) => setSearchTerm(e.target.value)}
          onSearch={handleSearch}
          loading={loading}
          style={{ marginBottom: '20px' }}
        />
        
        <Table
          dataSource={parts}
          columns={columns}
          rowKey="partNumber"
          loading={loading}
          pagination={{ pageSize: 20 }}
        />
      </Card>
    </div>
  );
}

export default App;
```

### F. Run React App

```bash
npm run dev
```

Open: `http://localhost:5173`

**You should see a working parts search! 🎊**

---

## 🎯 STEP 7: Deploy (Optional - When Ready)

### A. Build React for Production

```bash
cd VehiclePartsMapping.Web
npm run build

# Copy build to API
xcopy dist\* ..\VehiclePartsMapping.API\wwwroot\ /E /I /Y
```

### B. Publish API

```bash
cd ..\VehiclePartsMapping.API
dotnet publish -c Release -o C:\WebApps\VehiclePartsMapping
```

### C. Run in Production

```bash
cd C:\WebApps\VehiclePartsMapping
dotnet VehiclePartsMapping.API.dll
```

Access from any PC: `http://server-pc:5000`

---

## ✅ Success Checklist

- [ ] Database created and vw_Parts view works
- [ ] ASP.NET Core API running
- [ ] Swagger accessible at /swagger
- [ ] Can search parts via API
- [ ] React app running
- [ ] Can search parts via UI

---

## 🐛 Common Issues & Solutions

### Issue: "Cannot connect to database"
**Solution:**
```sql
-- Test connection in SSMS
sqlcmd -S server-pc\wizsoft2 -U issa -P 5060977Ih -Q "SELECT @@VERSION"
```

### Issue: "vw_Parts returns no data"
**Solution:**
```sql
-- Check if Items table has data
SELECT COUNT(*) FROM SH2013.dbo.Items WHERE TreeType = 0;

-- Check if ExtraNotes has data
SELECT COUNT(*) FROM SH2013.dbo.ExtraNotes WHERE NoteID IN (2,5,6,7,8);
```

### Issue: "CORS error in React"
**Solution:** Already handled in Program.cs with AllowAll policy

---

## 📞 Need Help?

1. Check the error message carefully
2. Look at the SQL Server error log
3. Check the API console output
4. Open browser developer console (F12)

---

## 🎓 Next Steps

Once everything works:
1. Add more controllers (Vehicles, Mappings)
2. Improve React UI with routing
3. Add bulk mapping features
4. Connect to Israeli Government API for vehicles

**You're ready to build! 🚀**
