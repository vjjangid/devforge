using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevForge.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public const string OneActivePerApplicationIndex = "ux_deployments_one_active_per_application";

    public void Configure(EntityTypeBuilder<Deployment> builder)
    {
        builder.ToTable("deployments", table =>
        {
            table.HasEnumCheckConstraint<Deployment, DeploymentStatus>("ck_deployments_status", "status");
            table.HasEnumCheckConstraint<Deployment, DeploymentStage>("ck_deployments_current_stage", "current_stage");
        });

        builder.HasKey(deployment => deployment.Id);
        builder.Property(deployment => deployment.Id).ValueGeneratedNever();

        builder.Property(deployment => deployment.Version).HasMaxLength(Deployment.VersionMaxLength).IsRequired();
        builder.Property(deployment => deployment.CommitSha).HasMaxLength(Deployment.CommitShaMaxLength);
        builder.Property(deployment => deployment.Status).HasConversion<string>().HasMaxLength(EnumColumn.MaxLength).IsRequired();
        builder.Property(deployment => deployment.CurrentStage).HasConversion<string>().HasMaxLength(EnumColumn.MaxLength);
        builder.Property(deployment => deployment.ErrorMessage).HasMaxLength(Deployment.ErrorMessageMaxLength);
        builder.Property(deployment => deployment.Url).HasMaxLength(Deployment.UrlMaxLength);
        builder.Property(deployment => deployment.WorkerId).HasMaxLength(Deployment.WorkerIdMaxLength);

        // Deployment history of an application, newest first; also guarantees unique version numbers.
        builder.HasIndex(deployment => new { deployment.ApplicationId, deployment.Number }).IsUnique();

        // At most one queued/running deployment per application, enforced by the database.
        builder.HasIndex(deployment => deployment.ApplicationId, OneActivePerApplicationIndex)
            .HasDatabaseName(OneActivePerApplicationIndex)
            .IsUnique()
            .HasFilter($"status IN ({EnumColumn.SqlList(Deployment.ActiveStatuses)})");

        // The worker's queue scan: oldest queued deployment first.
        builder.HasIndex(deployment => new { deployment.Status, deployment.CreatedAt });

        // Dashboard: recent deployments and "deployments today".
        builder.HasIndex(deployment => deployment.CreatedAt);

        builder.HasOne(deployment => deployment.Build)
            .WithMany()
            .HasForeignKey(deployment => deployment.BuildId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(deployment => deployment.Logs)
            .WithOne()
            .HasForeignKey(log => log.DeploymentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
