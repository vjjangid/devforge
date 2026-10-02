namespace DevForge.Domain.Common;

/// <summary>Thrown when a value violates a domain rule for a specific field.</summary>
public sealed class DomainValidationException(string field, string message) : Exception(message)
{
    public string Field { get; } = field;
}
