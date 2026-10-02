namespace DevForge.Application.Common;

/// <summary>The request is valid but cannot be applied to the resource in its current state.</summary>
public sealed class ConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
