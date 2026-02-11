using Sh.Autofit.StickerPrinting.Services.Database;
using Sh.Autofit.StickerPrinting.Services.Label;
using Sh.Autofit.StickerPrinting.Services.Printing.Abstractions;
using Sh.Autofit.StickerPrinting.Services.Printing.Infrastructure;
using Sh.Autofit.StickerPrinting.Services.Printing.Zebra;
using Sh.Autofit.StickerPrinting.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

// Database services
builder.Services.AddSingleton<IPartDataService>(new PartDataService(connectionString));
builder.Services.AddSingleton<IArabicDescriptionService>(new ArabicDescriptionService(connectionString));
builder.Services.AddSingleton<IStockDataService>(new StockDataService(connectionString));

// Printer infrastructure
builder.Services.AddSingleton<IRawPrinterCommunicator, RawPrinterCommunicator>();
builder.Services.AddSingleton<IZplCommandGenerator, ZplCommandGenerator>();
builder.Services.AddSingleton<IPrinterService, ZebraPrinterService>();

// Label services
builder.Services.AddSingleton<ILabelRenderService, LabelRenderService>();

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    // SignalR requires specific origins (not AllowAnyOrigin) when using credentials
    options.AddPolicy("SignalR", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapHub<PrintHub>("/hubs/print").RequireCors("SignalR");
app.MapFallbackToFile("index.html");

app.Run();
