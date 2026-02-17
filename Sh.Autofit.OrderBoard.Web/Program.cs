using Sh.Autofit.OrderBoard.Web.Background;
using Sh.Autofit.OrderBoard.Web.Hubs;
using Sh.Autofit.OrderBoard.Web.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found.");

// Database services
builder.Services.AddSingleton<ISh2013PollingService>(new Sh2013PollingService(connectionString));
builder.Services.AddSingleton<IAppOrderService>(new AppOrderService(connectionString));
builder.Services.AddSingleton<IDeliveryService>(new DeliveryService(connectionString));
builder.Services.AddSingleton<IAccountsService>(new AccountsService(connectionString));

// Business logic
builder.Services.AddSingleton<IStageEngine, StageEngine>();
builder.Services.AddSingleton<IMergeService, MergeService>();
builder.Services.AddSingleton<IBoardBuilder, BoardBuilder>();

// Background polling
builder.Services.AddHostedService<PollingBackgroundService>();

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
app.MapHub<BoardHub>("/hubs/board").RequireCors("SignalR");
app.MapFallbackToFile("index.html");

app.Run();
