﻿namespace Commands;

/// <summary>
///     An exception that is thrown when a component cannot be created due to bad parameter formatting.
/// </summary>
public sealed class ComponentFormatException(string reason)
    : Exception(reason)
{
}
