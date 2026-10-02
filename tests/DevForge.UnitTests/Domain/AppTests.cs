using DevForge.Domain.Applications;
using DevForge.Domain.Common;

namespace DevForge.UnitTests.Domain;

public class AppTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_trims_input_and_normalises_the_repository_url()
    {
        var app = App.Create("  Todo API ", "github.com/vijay/todo-api/", " main ", "dotnet-10", "  ", Now);

        Assert.Equal("Todo API", app.Name);
        Assert.Equal("https://github.com/vijay/todo-api", app.RepositoryUrl.Value);
        Assert.Equal("github.com/vijay/todo-api", app.RepositoryUrl.DisplayName);
        Assert.Equal("main", app.Branch);
        Assert.Equal(RuntimeCatalog.DotNet10.Key, app.Runtime);
        Assert.Null(app.Description);
        Assert.Equal(Now, app.CreatedAt);
        Assert.NotEqual(Guid.Empty, app.Id);
    }

    [Theory]
    [InlineData("", "github.com/a/b", "main", "dotnet-10", nameof(App.Name))]
    [InlineData("App", "", "main", "dotnet-10", nameof(App.RepositoryUrl))]
    [InlineData("App", "ftp://github.com/a/b", "main", "dotnet-10", nameof(App.RepositoryUrl))]
    [InlineData("App", "https://github.com", "main", "dotnet-10", nameof(App.RepositoryUrl))]
    [InlineData("App", "https://user:secret@github.com/a/b", "main", "dotnet-10", nameof(App.RepositoryUrl))]
    [InlineData("App", "github.com/a/b", "", "dotnet-10", nameof(App.Branch))]
    [InlineData("App", "github.com/a/b", "feature branch", "dotnet-10", nameof(App.Branch))]
    [InlineData("App", "github.com/a/b", "--upload-pack=evil", "dotnet-10", nameof(App.Branch))]
    [InlineData("App", "github.com/a/b", "main", "cobol", nameof(App.Runtime))]
    public void Create_rejects_invalid_input(string name, string repositoryUrl, string branch, string runtime, string expectedField)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => App.Create(name, repositoryUrl, branch, runtime, description: null, Now));

        Assert.Equal(expectedField, exception.Field);
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_the_limit()
    {
        var name = new string('a', App.NameMaxLength + 1);

        Assert.Throws<DomainValidationException>(() => App.Create(name, "github.com/a/b", "main", "dotnet-10", null, Now));
    }

    [Fact]
    public void UpdateDetails_changes_editable_fields_but_not_the_runtime()
    {
        var app = App.Create("Todo API", "github.com/vijay/todo-api", "main", "dotnet-10", null, Now);
        var later = Now.AddHours(1);

        app.UpdateDetails("Todo Service", "https://gitlab.com/vijay/todo.git", "develop", "Renamed", later);

        Assert.Equal("Todo Service", app.Name);
        Assert.Equal("https://gitlab.com/vijay/todo.git", app.RepositoryUrl.Value);
        Assert.Equal("gitlab.com/vijay/todo", app.RepositoryUrl.DisplayName);
        Assert.Equal("develop", app.Branch);
        Assert.Equal("Renamed", app.Description);
        Assert.Equal(RuntimeCatalog.DotNet10.Key, app.Runtime);
        Assert.Equal(Now, app.CreatedAt);
        Assert.Equal(later, app.UpdatedAt);
    }
}
