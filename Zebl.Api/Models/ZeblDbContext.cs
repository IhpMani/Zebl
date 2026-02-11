using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Zebl.Api.Models;

public partial class ZeblDbContext : DbContext
{
    public ZeblDbContext()
    {
    }

    public ZeblDbContext(DbContextOptions<ZeblDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=IHPOFFICE\\SQLEXPRESS;Database=EZClaimTest;User Id=sa;Password=Ihp@123;TrustServerCertificate=True;MultipleActiveResultSets=True");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.UserGuid).HasName("PK__AppUser__81B7740C5F82BC9D");

            entity.ToTable("AppUser");

            entity.Property(e => e.UserGuid)
                .ValueGeneratedNever()
                .HasColumnName("UserGUID");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(64);
            entity.Property(e => e.PasswordSalt).HasMaxLength(32);
            entity.Property(e => e.UserName).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
