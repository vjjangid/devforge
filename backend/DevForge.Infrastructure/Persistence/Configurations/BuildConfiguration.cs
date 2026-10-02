using DevForge.Domain.Builds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevForge.Infrastructure.Persistence.Configurations;

internal sealed class BuildConfiguration : IEntityTypeConfiguration<Build>
{
    public void Configure(EntityTypeBuilder<Build> builder)
    {
        builder.ToTable("builds", table =>
            table.HasEnumCheckConstraint<Build, BuildStatus>("ck_builds_status", "status"));

        builder.HasKey(build => build.Id);
        builder.Property(build => build.Id).ValueGeneratedNever();

        builder.Property(build => build.Status).HasConversion<string>().HasMaxLength(EnumColumn.MaxLength).IsRequired();
        builder.Property(build => build.ArtifactReference).HasMaxLength(Build.ArtifactReferenceMaxLength);
        builder.Property(build => build.ErrorMessage).HasMaxLength(Build.ErrorMessageMaxLength);

        builder.HasIndex(build => new { build.ApplicationId, build.StartedAt });
    }
}
