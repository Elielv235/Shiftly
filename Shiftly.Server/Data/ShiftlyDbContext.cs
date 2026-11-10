using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Shiftly.Server.Models;
using Shiftly.Server.Security;

namespace Shiftly.Server.Data
{
    public class ShiftlyDbContext : DbContext
    {
        private static readonly byte[] AdminHash;
        private static readonly byte[] AdminSalt;
        private static readonly byte[] WorkerHash;
        private static readonly byte[] WorkerSalt;

        static ShiftlyDbContext()
        {
            PasswordSecurityHelper.CreatePasswordHash("Admin123!", out AdminHash, out AdminSalt);
            PasswordSecurityHelper.CreatePasswordHash("Worker123!", out WorkerHash, out WorkerSalt);
        }

        public ShiftlyDbContext(DbContextOptions<ShiftlyDbContext> options) : base(options) { }

        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Shift> Shifts { get; set; } = null!;
        public DbSet<Availability> Availabilities { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Roles configuration and seed
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Roles");
                entity.HasKey(r => r.RoleId);
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
                entity.HasIndex(r => r.RoleName).IsUnique();
                entity.Property(r => r.Description).HasMaxLength(200);

                entity.HasData(
                    new Role { RoleId = 1, RoleName = "Guest", Description = "אורח - צפייה בדמו בלבד" },
                    new Role { RoleId = 2, RoleName = "Worker", Description = "עובד סניף - הגשת זמינות וצפייה בסידור עבודה אישי" },
                    new Role { RoleId = 3, RoleName = "ShiftManager", Description = "מנהל משמרת - ניהול משמרת בזמן אמת ובקרת סניף" },
                    new Role { RoleId = 4, RoleName = "HRAdmin", Description = "מנהל סניף / כוח אדם - ניהול מלא של משתמשים וסידורי עבודה" }
                );
            });

            // Users configuration and seed
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

                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.RoleId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasData(
                    new User
                    {
                        UserId = 1,
                        Username = "admin",
                        PasswordHash = AdminHash,
                        PasswordSalt = AdminSalt,
                        FullName = "אליאל וולקונסקי (מנהל ראשי)",
                        Email = "admin@shiftly.co.il",
                        PhoneNumber = "050-1234567",
                        RoleId = 4,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new User
                    {
                        UserId = 2,
                        Username = "worker1",
                        PasswordHash = WorkerHash,
                        PasswordSalt = WorkerSalt,
                        FullName = "דניאל כהן (עובד סניף)",
                        Email = "daniel@shiftly.co.il",
                        PhoneNumber = "052-7654321",
                        RoleId = 2,
                        IsActive = true,
                        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                );
            });

            // Seed sample shifts for branch metrics
            modelBuilder.Entity<Shift>().HasData(
                new Shift { ShiftId = 1, ShiftDate = DateTime.Today.AddDays(1), ShiftType = "Morning", RequiredWorkers = 4, CreatedByUserId = 1 },
                new Shift { ShiftId = 2, ShiftDate = DateTime.Today.AddDays(1), ShiftType = "Evening", RequiredWorkers = 3, CreatedByUserId = 1 }
            );

            // Seed initial availability records for worker1 (UserId: 2)
            modelBuilder.Entity<Availability>().HasData(
                new Availability { AvailabilityId = 1, UserId = 2, TargetDate = DateTime.Today.AddDays(2), CanWorkMorning = true, CanWorkEvening = false, Notes = "זמין בוקר בלבד", Status = "Pending" },
                new Availability { AvailabilityId = 2, UserId = 2, TargetDate = DateTime.Today.AddDays(3), CanWorkMorning = false, CanWorkEvening = true, Notes = "לימודים בבוקר", Status = "Pending" }
            );
        }

        public void SeedInitialUsers()
        {
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
                    RoleId = 4,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

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
                    RoleId = 2,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            SaveChanges();

            if (!Shifts.Any())
            {
                Shifts.AddRange(
                    new Shift { ShiftDate = DateTime.Today.AddDays(1), ShiftType = "Morning", RequiredWorkers = 4, CreatedByUserId = 1 },
                    new Shift { ShiftDate = DateTime.Today.AddDays(1), ShiftType = "Evening", RequiredWorkers = 3, CreatedByUserId = 1 }
                );
                SaveChanges();
            }

            if (!Availabilities.Any())
            {
                Availabilities.AddRange(
                    new Availability { UserId = 2, TargetDate = DateTime.Today.AddDays(2), CanWorkMorning = true, CanWorkEvening = false, Notes = "זמין בוקר בלבד", Status = "Pending" },
                    new Availability { UserId = 2, TargetDate = DateTime.Today.AddDays(3), CanWorkMorning = false, CanWorkEvening = true, Notes = "לימודים בבוקר", Status = "Pending" }
                );
                SaveChanges();
            }
        }
    }
}
