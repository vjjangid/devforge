using DevForge.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevForge.Infrastructure.Persistence.Configurations;

internal sealed class AppConfiguration : IEntityTypeConfiguration<App>
{
    public const string UniqueNameIndex = "ux_applications_name";

    public void Configure(EntityTypeBuilder<App> builder)
    {
        builder.ToTable("applications");

        builder.HasKey(application => application.Id);
        builder.Property(application => application.Id).ValueGeneratedNever();

        builder.Property(application => application.Name).HasMaxLength(App.NameMaxLength).IsRequired();
        builder.Property(application => application.RepositoryUrl)
            .HasConversion(url => url.Value, value => RepositoryUrl.FromPersisted(value))
            .HasMaxLength(RepositoryUrl.MaxLength)
            .IsRequired();
        builder.Property(application => application.Branch).HasMaxLength(App.BranchMaxLength).IsRequired();
        builder.Property(application => application.Runtime).HasMaxLength(RuntimeCatalog.KeyMaxLength).IsRequired();
        builder.Property(application => application.Description).HasMaxLength(App.DescriptionMaxLength);

        builder.HasIndex(application => application.Name).IsUnique().HasDatabaseName(UniqueNameIndex);
        builder.HasIndex(application => application.CreatedAt);

        // Deleting an application removes its whole history. The application layer refuses the
        // delete while a deployment is still active.
        builder.HasMany(application => application.Deployments)
            .WithOne(deployment => deployment.Application)
            .HasForeignKey(deployment => deployment.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(application => application.Builds)
            .WithOne()
            .HasForeignKey(build => build.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
