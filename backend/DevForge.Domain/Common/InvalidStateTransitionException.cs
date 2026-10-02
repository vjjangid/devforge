namespace DevForge.Domain.Common;

/// <summary>Thrown when an entity is asked to move to a state it cannot reach from its current one.</summary>
public sealed class InvalidStateTransitionException(string message) : Exception(message);
