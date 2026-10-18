using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shiftly.Server.Data;
using Shiftly.Server.Services;
using Shiftly.Shared.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. הוספת שירותי מסד נתונים - Entity Framework Core עם SQL Server / LocalDB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=ShiftlyDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

builder.Services.AddDbContext<ShiftlyDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. הזרקת תלויות (Dependency Injection) לשכבת השירותים
builder.Services.AddScoped<IAuthService, AuthService>();

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

// 5. אתחול מסד הנתונים והזנת נתונים ראשוניים (Seeding)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ShiftlyDbContext>();
    try
    {
        dbContext.Database.EnsureCreated();
        dbContext.SeedInitialUsers();
    }
    catch (Exception)
    {
        dbContext.ChangeTracker.Clear();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        dbContext.SeedInitialUsers();
    }
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
