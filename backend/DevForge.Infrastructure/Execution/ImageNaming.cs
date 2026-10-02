using System.Text;

namespace DevForge.Infrastructure.Execution;

/// <summary>Turns an application and version into a Docker image reference such as <c>devforge/todo-api-9c2f41ab:v3</c>.</summary>
internal static class ImageNaming
{
    private const string Namespace = "devforge";
    private const string FallbackSlug = "app";
    private const int MaxSlugLength = 50;
    private const int IdSuffixLength = 8;

    public static string ImageReferenceFor(Guid applicationId, string applicationName, string version) =>
        $"{RepositoryFor(applicationId, applicationName)}:{version}";

    /// <summary>
    /// The image name without a tag. The slug makes it readable; the id suffix keeps two applications
    /// whose names reduce to the same slug ("Todo API", "todo-api") from overwriting each other's images.
    /// </summary>
    public static string RepositoryFor(Guid applicationId, string applicationName)
    {
        // The id's tail is its random part; the head of a version 7 id is a timestamp shared by neighbours.
        var id = applicationId.ToString("N");
        return $"{Namespace}/{Slugify(applicationName)}-{id[^IdSuffixLength..]}";
    }

    /// <summary>
    /// The container that runs one version of an application, e.g. <c>devforge-todo-api-9c2f41ab-v3</c>.
    /// The version is part of the name so a new version can start while the previous one is still serving.
    /// </summary>
    public static string ContainerNameFor(Guid applicationId, string applicationName, string version) =>
        $"{RepositoryFor(applicationId, applicationName).Replace('/', '-')}-{version}";

    /// <summary>
    /// Docker image names allow only lower-case letters, digits and single separators, and must start
    /// and end with a letter or digit. Everything else becomes a hyphen.
    /// </summary>
    public static string Slugify(string name)
    {
        var slug = new StringBuilder(name.Length);

        foreach (var character in name.ToLowerInvariant())
        {
            if (char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character))
            {
                slug.Append(character);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var result = slug.ToString().Trim('-');
        if (result.Length > MaxSlugLength)
        {
            result = result[..MaxSlugLength].TrimEnd('-');
        }

        return result.Length == 0 ? FallbackSlug : result;
    }
}
