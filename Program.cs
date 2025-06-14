using DatabaseBackup.Extensions;
using DatabaseBackup.Models.Configurations;
using DatabaseBackup.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Configuration
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();

// Ensure Logs folder exists (optional)
//Directory.CreateDirectory("Logs");

// Correct usage: attach .UseSerilog() to builder.Host
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
{
    loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext();

});


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// services
builder.Services.AddTransient<DatabaseBackupRunner>();
builder.Services.RegisterConfigurations(builder.Configuration);

builder.Services.Configure<HostedServiceConfigurations>(
    builder.Configuration.GetSection("HostedServiceConfigurations"));

builder.Services.AddHostedService<BackgroundDatabaseBackupService>();
builder.Services.AddHealthChecks();

var app = builder.Build();

//app.Urls.Add("http://0.0.0.0:80");
// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Docker"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.MapHealthChecks("/health");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

Log.Information("Database Backup App started in {Environment} mode", app.Environment.EnvironmentName);

