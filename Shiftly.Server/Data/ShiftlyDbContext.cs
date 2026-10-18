using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Shiftly.Server.Models;
using Shiftly.Server.Security;

namespace Shiftly.Server.Data
{
    /// <summary>
    /// הקשר מסד הנתונים של פרויקט Shiftly
    /// מנהל את המיפוי בין ישויות C# לטבלאות ב-SQL Server באמצעות Entity Framework Core
    /// </summary>
    public class ShiftlyDbContext : DbContext
    {
        public ShiftlyDbContext(DbContextOptions<ShiftlyDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // מיפוי טבלת Roles
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(r => r.RoleId);
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
                entity.HasIndex(r => r.RoleName).IsUnique();
                entity.Property(r => r.Description).HasMaxLength(200);

                // הזנת נתוני תפקידים קבועים (Seed Roles)
                entity.HasData(
                    new Role { RoleId = 1, RoleName = "Guest", Description = "אורח - צפייה בדמו בלבד" },
                    new Role { RoleId = 2, RoleName = "Worker", Description = "עובד סניף - הגשת זמינות וצפייה בסידור עבודה אישי" },
                    new Role { RoleId = 3, RoleName = "ShiftManager", Description = "מנהל משמרת - ניהול משמרת בזמן אמת ובקרת סניף" },
                    new Role { RoleId = 4, RoleName = "HRAdmin", Description = "מנהל סניף / כוח אדם - ניהול מלא של משתמשים וסידורי עבודה" }
                );
            });

            // מיפוי טבלת Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.UserId);
                entity.Property(u => u.Username).IsRequired().HasMaxLength(50);
                entity.HasIndex(u => u.Username).IsUnique();

                entity.Property(u => u.PasswordHash).IsRequired();
                entity.Property(u => u.PasswordSalt).IsRequired();

                entity.Property(u => u.FullName).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(100);
                entity.Property(u => u.PhoneNumber).IsRequired().HasMaxLength(20);

                entity.Property(u => u.IsActive).HasDefaultValue(true);
                entity.Property(u => u.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

                // קשר של מפתח זר לטבלת Roles
                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);
            });
        }

        /// <summary>
        /// פונקציית אתחול נתונים ראשוניים למערכת (Seed Users)
        /// מופעלת בעליית השרת כדי להבטיח קיום משתמש מנהל ומשתמש עובד לבדיקות והצגה לבוחן
        /// </summary>
        public void SeedInitialUsers()
        {
            // בדיקה אם קיים משתמש Admin
            if (!Users.Any(u => u.Username == "admin"))
            {
                PasswordSecurityHelper.CreatePasswordHash("Admin123!", out byte[] hash, out byte[] salt);
                Users.Add(new User
                {
                    Username = "admin",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = "אליאל וולקונסקי (מנהל ראשי)",
                    Email = "admin@shiftly.co.il",
                    PhoneNumber = "050-1234567",
                    RoleId = 4, // HRAdmin
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            // בדיקה אם קיים משתמש עובד לדוגמה
            if (!Users.Any(u => u.Username == "worker1"))
            {
                PasswordSecurityHelper.CreatePasswordHash("Worker123!", out byte[] hash, out byte[] salt);
                Users.Add(new User
                {
                    Username = "worker1",
                    PasswordHash = hash,
                    PasswordSalt = salt,
                    FullName = "דניאל כהן (עובד סניף)",
                    Email = "daniel@shiftly.co.il",
                    PhoneNumber = "052-7654321",
                    RoleId = 2, // Worker
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            SaveChanges();
        }
    }
}
