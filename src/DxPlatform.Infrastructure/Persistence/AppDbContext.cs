using DxPlatform.Domain.Agents;
using DxPlatform.Domain.Applications;
using DxPlatform.Domain.Common;
using DxPlatform.Domain.DataAssets;
using DxPlatform.Domain.Groups;
using DxPlatform.Domain.Operations;
using DxPlatform.Domain.Relations;
using DxPlatform.Domain.Tags;
using DxPlatform.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DxPlatform.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<ApplicationAsset> ApplicationAssets => Set<ApplicationAsset>();
    public DbSet<ApplicationPermission> ApplicationPermissions => Set<ApplicationPermission>();
    public DbSet<DataAsset> DataAssets => Set<DataAsset>();
    public DbSet<DataAssetRelation> DataAssetRelations => Set<DataAssetRelation>();
    public DbSet<AgentAsset> AgentAssets => Set<AgentAsset>();
    public DbSet<ToolAsset> ToolAssets => Set<ToolAsset>();
    public DbSet<ApplicationDataAsset> ApplicationDataAssets => Set<ApplicationDataAsset>();
    public DbSet<AgentDataAsset> AgentDataAssets => Set<AgentDataAsset>();
    public DbSet<AgentToolAsset> AgentToolAssets => Set<AgentToolAsset>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<HealthCheckResult> HealthCheckResults => Set<HealthCheckResult>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AssetTag> AssetTags => Set<AssetTag>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentity(builder);
        ConfigureGroups(builder);
        ConfigureApplications(builder);
        ConfigureCatalogs(builder);
        ConfigureOperations(builder);
        ConfigureSqliteDateTimes(builder);

        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(type => typeof(BaseEntity).IsAssignableFrom(type.ClrType)))
        {
            builder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.RowVersion))
                .HasMaxLength(16)
                .IsConcurrencyToken()
                .ValueGeneratedNever();
            builder.Entity(entityType.ClrType).HasIndex(nameof(BaseEntity.IsActive));
        }
    }

    private void ConfigureSqliteDateTimes(ModelBuilder builder)
    {
        if (!Database.IsSqlite())
        {
            return;
        }

        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            value => value.UtcDateTime.Ticks,
            value => new DateTimeOffset(value, TimeSpan.Zero));
        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            value => value.HasValue ? value.Value.UtcDateTime.Ticks : null,
            value => value.HasValue ? new DateTimeOffset(value.Value, TimeSpan.Zero) : null);

        foreach (var property in builder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
        {
            if (property.ClrType == typeof(DateTimeOffset))
            {
                property.SetValueConverter(dateTimeOffsetConverter);
            }
            else if (property.ClrType == typeof(DateTimeOffset?))
            {
                property.SetValueConverter(nullableDateTimeOffsetConverter);
            }
        }
    }

    private static void ConfigureIdentity(ModelBuilder builder)
    {
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.DisplayName).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Department).HasMaxLength(160);
            entity.Property(x => x.CreatedBy).HasMaxLength(450);
            entity.Property(x => x.UpdatedBy).HasMaxLength(450);
            entity.Property(x => x.RowVersion).HasMaxLength(16).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(x => x.IsActive);
        });
        builder.Entity<ApplicationRole>(entity =>
        {
            entity.Property(x => x.CreatedBy).HasMaxLength(450);
            entity.Property(x => x.UpdatedBy).HasMaxLength(450);
            entity.Property(x => x.RowVersion).HasMaxLength(16).IsConcurrencyToken().ValueGeneratedNever();
            entity.HasIndex(x => x.IsActive);
        });
    }

    private static void ConfigureGroups(ModelBuilder builder)
    {
        builder.Entity<Group>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(160);
        });

        builder.Entity<GroupMember>(entity =>
        {
            entity.HasIndex(x => new { x.GroupId, x.UserId }).IsUnique();
            entity.HasOne(x => x.Group).WithMany(x => x.Members).HasForeignKey(x => x.GroupId);
            entity.HasOne<ApplicationUser>().WithMany(x => x.GroupMemberships)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureApplications(ModelBuilder builder)
    {
        builder.Entity<ApplicationAsset>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.Status, x.AssetType, x.IsActive });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.AssetType).HasConversion<string>().HasMaxLength(32);
        });

        builder.Entity<ApplicationPermission>(entity =>
        {
            entity.Property(x => x.Level).HasConversion<string>().HasMaxLength(16);
            entity.HasIndex(x => new { x.ApplicationAssetId, x.UserId }).IsUnique()
                .HasFilter("[UserId] IS NOT NULL");
            entity.HasIndex(x => new { x.ApplicationAssetId, x.GroupId }).IsUnique()
                .HasFilter("[GroupId] IS NOT NULL");
            entity.HasOne(x => x.ApplicationAsset).WithMany(x => x.Permissions)
                .HasForeignKey(x => x.ApplicationAssetId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Group>().WithMany().HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ApplicationPermission_Principal",
                "([UserId] IS NOT NULL AND [GroupId] IS NULL) OR ([UserId] IS NULL AND [GroupId] IS NOT NULL)"));
        });
    }

    private static void ConfigureCatalogs(ModelBuilder builder)
    {
        builder.Entity<DataAsset>(entity =>
        {
            entity.HasIndex(x => new { x.DataType, x.Status, x.IsActive });
            entity.Property(x => x.DataType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<AgentAsset>(entity =>
        {
            entity.HasIndex(x => new { x.AgentType, x.Status, x.IsActive });
            entity.Property(x => x.AgentType).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RiskLevel).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.EvaluationStatus).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<AssetTag>(entity =>
        {
            entity.HasIndex(x => new { x.AssetKind, x.AssetId, x.TagId }).IsUnique();
            entity.Property(x => x.AssetKind).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<Tag>().HasIndex(x => x.Name).IsUnique();

        builder.Entity<ApplicationDataAsset>().HasIndex(x => new { x.ApplicationAssetId, x.DataAssetId }).IsUnique();
        builder.Entity<AgentDataAsset>().HasIndex(x => new { x.AgentAssetId, x.DataAssetId }).IsUnique();
        builder.Entity<AgentToolAsset>().HasIndex(x => new { x.AgentAssetId, x.ToolAssetId }).IsUnique();
        builder.Entity<DataAssetRelation>().HasIndex(x => new { x.SourceDataAssetId, x.TargetDataAssetId }).IsUnique();

        builder.Entity<DataAssetRelation>().HasOne(x => x.SourceDataAsset).WithMany()
            .HasForeignKey(x => x.SourceDataAssetId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<DataAssetRelation>().HasOne(x => x.TargetDataAsset).WithMany()
            .HasForeignKey(x => x.TargetDataAssetId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureOperations(ModelBuilder builder)
    {
        builder.Entity<Deployment>(entity =>
        {
            entity.HasIndex(x => new { x.ApplicationAssetId, x.StartedAt });
            entity.Property(x => x.Result).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<HealthCheckResult>(entity =>
        {
            entity.HasIndex(x => new { x.ApplicationAssetId, x.CheckedAt });
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        });
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.OccurredAt);
            entity.HasIndex(x => new { x.TargetType, x.TargetId });
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditValues();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyAuditValues()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationUser>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        foreach (var entry in ChangeTracker.Entries<ApplicationRole>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
