using DevForge.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevForge.Infrastructure.Persistence.Configurations;

internal sealed class DeploymentLogConfiguration : IEntityTypeConfiguration<DeploymentLog>
{
    public void Configure(EntityTypeBuilder<DeploymentLog> builder)
    {
        builder.ToTable("deployment_logs", table =>
        {
            table.HasEnumCheckConstraint<DeploymentLog, DeploymentLogLevel>("ck_deployment_logs_level", "level");
            table.HasEnumCheckConstraint<DeploymentLog, DeploymentStage>("ck_deployment_logs_stage", "stage");
        });

        builder.HasKey(log => log.Id);
        builder.Property(log => log.Id).UseIdentityAlwaysColumn();

        builder.Property(log => log.Level).HasConversion<string>().HasMaxLength(EnumColumn.MaxLength).IsRequired();
        builder.Property(log => log.Stage).HasConversion<string>().HasMaxLength(EnumColumn.MaxLength);
        builder.Property(log => log.Message).HasMaxLength(DeploymentLog.MessageMaxLength).IsRequired();

        // Logs are always read per deployment in insertion order, optionally from a cursor.
        builder.HasIndex(log => new { log.DeploymentId, log.Id });
    }
}
