namespace DevForge.Application.Pipeline;

/// <summary>An expected stage failure (compile error, failing tests…) as opposed to a bug in DevForge.</summary>
public sealed class StageFailedException(string message) : Exception(message);
