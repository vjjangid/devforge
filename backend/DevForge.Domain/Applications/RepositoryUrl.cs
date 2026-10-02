using DevForge.Domain.Common;

namespace DevForge.Domain.Applications;

/// <summary>
/// A normalised HTTP(S) Git repository URL. Input without a scheme
/// (e.g. <c>github.com/owner/repo</c>) is treated as HTTPS.
/// </summary>
public sealed record RepositoryUrl
{
    public const int MaxLength = 500;

    private const string Field = nameof(App.RepositoryUrl);
    private const string SchemeSeparator = "://";
    private const string GitSuffix = ".git";

    private RepositoryUrl(string value) => Value = value;

    public string Value { get; }

    /// <summary>Host and path without scheme or <c>.git</c> suffix, e.g. <c>github.com/owner/repo</c>.</summary>
    public string DisplayName
    {
        get
        {
            var withoutScheme = Value[(Value.IndexOf(SchemeSeparator, StringComparison.Ordinal) + SchemeSeparator.Length)..];
            return withoutScheme.EndsWith(GitSuffix, StringComparison.OrdinalIgnoreCase)
                ? withoutScheme[..^GitSuffix.Length]
                : withoutScheme;
        }
    }

    public static RepositoryUrl Create(string? input)
    {
        var candidate = input?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            throw new DomainValidationException(Field, "Repository URL is required.");
        }

        if (!candidate.Contains(SchemeSeparator, StringComparison.Ordinal))
        {
            candidate = Uri.UriSchemeHttps + SchemeSeparator + candidate;
        }

        if (candidate.Length > MaxLength)
        {
            throw new DomainValidationException(Field, $"Repository URL must be at most {MaxLength} characters.");
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new DomainValidationException(Field, "Repository URL must be a valid http(s) URL.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DomainValidationException(Field, "Repository URL must not contain credentials.");
        }

        if (uri.AbsolutePath.Trim('/').Length == 0)
        {
            throw new DomainValidationException(Field, "Repository URL must include the repository path.");
        }

        return new RepositoryUrl(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
    }

    /// <summary>Rehydrates a value that was already validated before it was stored.</summary>
    public static RepositoryUrl FromPersisted(string value) => new(value);

    public override string ToString() => Value;
}
