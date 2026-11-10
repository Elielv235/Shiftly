using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shiftly.Server.Data;
using Shiftly.Server.Services;
using Shiftly.Shared.Interfaces;

if (!OperatingSystem.IsWindows())
{
    throw new PlatformNotSupportedException("Shiftly.Server requires Windows for Microsoft Access (System.Data.OleDb).");
}

var builder = WebApplication.CreateBuilder(args);

// 1. שכבת גישה לנתונים: Microsoft Access (.accdb) בלבד באמצעות System.Data.OleDb
builder.Services.AddSingleton<AccessConnectionFactory>();

// 2. הזרקת תלויות (Dependency Injection) לשכבת השירותים
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IShiftlyService, ShiftlyService>();
builder.Services.AddScoped<BranchService>();

// 3. הגדרת בקרים ו-Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. הגדרת מדיניות CORS לתקשורת פתוחה ובטוחה מול הקליינטים
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

// 5. אתחול אוטומטי של ShiftlyDatabase.accdb, יצירת 7 הטבלאות והזנת נתונים ראשוניים (Seeding)
using (var scope = app.Services.CreateScope())
{
    var connectionFactory = scope.ServiceProvider.GetRequiredService<AccessConnectionFactory>();
    AccessDbInitializer.Initialize(connectionFactory);
}

// 6. הגדרת צינור הבקשות (HTTP Request Pipeline)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
