using DevForge.Infrastructure.Execution;

namespace DevForge.IntegrationTests.Execution;

public class ImageNamingTests
{
    private static readonly Guid ApplicationId = Guid.Parse("01a0fdd6-11d0-7d67-9c20-317940ea4c59");

    [Theory]
    [InlineData("Todo API", "todo-api")]
    [InlineData("todo-api", "todo-api")]
    [InlineData("  My_App.v2 (Staging)!  ", "my-app-v2-staging")]
    [InlineData("Größe & Co", "gr-e-co")]
    [InlineData("---", "app")]
    [InlineData("日本語", "app")]
    public void Slugify_produces_a_valid_image_name_component(string name, string expected)
    {
        Assert.Equal(expected, ImageNaming.Slugify(name));
    }

    [Fact]
    public void Slugify_limits_the_length_without_leaving_a_trailing_separator()
    {
        var slug = ImageNaming.Slugify(new string('a', 49) + " " + new string('b', 30));

        Assert.Equal(new string('a', 49), slug);
    }

    [Fact]
    public void The_image_reference_combines_namespace_slug_id_suffix_and_version()
    {
        Assert.Equal("devforge/todo-api-40ea4c59:v3", ImageNaming.ImageReferenceFor(ApplicationId, "Todo API", "v3"));
    }

    [Fact]
    public void Applications_with_the_same_slug_get_different_images()
    {
        var other = Guid.Parse("01a0fdd6-11d0-7d67-9c20-3179aaaaaaaa");

        Assert.NotEqual(
            ImageNaming.RepositoryFor(ApplicationId, "Todo API"),
            ImageNaming.RepositoryFor(other, "todo-api"));
    }
}
