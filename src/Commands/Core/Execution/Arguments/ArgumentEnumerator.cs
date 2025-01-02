﻿using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Commands;

/// <summary>
///     Contains a set of arguments for the command pipeline.
///     By using either <see cref="IComponentTree.Execute{T}(T, IEnumerable{KeyValuePair{string, object?}}, CommandOptions?)"/> or <see cref="IComponentTree.Execute{T}(T, IEnumerable{object?}, CommandOptions?)"/> you can use named or unnamed command entry.
/// </summary>
public struct ArgumentEnumerator
{
    const char U0020 = ' ';

    private int _size;
    private int _indexUnnamed = 0;

    private readonly object[] _unnamedArgs;
    private readonly Dictionary<string, object?> _namedArgs;

    /// <summary>
    ///     Gets the length of the argument set. This is represented by a sum of named and unnamed arguments, reducing it by the search range of the resulted command by calling <see cref="SetSize(int)"/>.
    /// </summary>
    public readonly int Length
        => _size;

    /// <summary>
    ///     Creates a new <see cref="ArgumentEnumerator"/> from a set of named arguments.
    /// </summary>
    /// <param name="args">The range of named arguments to enumerate in this set.</param>
    /// <param name="comparer">The comparer to evaluate keys in the inner named dictionary.</param>
    public ArgumentEnumerator(IEnumerable<KeyValuePair<string, object?>> args, StringComparer comparer)
    {
        _namedArgs = new(comparer);

        var unnamedFill = new List<string>();

        foreach (var kvp in args)
        {
            if (kvp.Key == null)
                throw new ArgumentNullException(nameof(args), "One of the keys in the provided collection is null.");

            if (kvp.Value == null)
                unnamedFill.Add(kvp.Key);
            else
                _namedArgs[kvp.Key] = kvp.Value;
        }

        _unnamedArgs = [.. unnamedFill];
        _size = _unnamedArgs.Length + _namedArgs.Count;
    }

    /// <summary>
    ///     Creates a new <see cref="ArgumentEnumerator"/> from a set of unnamed arguments.
    /// </summary>
    /// <param name="args">The range of unnamed arguments to enumerate in this set.</param>
    public ArgumentEnumerator(IEnumerable<object> args)
    {
        _namedArgs = [];

        _unnamedArgs = args.ToArray();
        _size = _unnamedArgs.Length;
    }

    /// <summary>
    ///     Makes an attempt to retrieve the next argument in the set. If a named argument is found, it will be removed from the set and returned. 
    ///     If an unnamed argument is found, it will be returned and the currently observed index will be incremented to return the next unnamed argument on the next try.
    /// </summary>
    /// <remarks>
    ///     This method compares the parameter name to the named arguments known to the set at the point of execution and determines the result based on the <see cref="StringComparer"/> set in <see cref="CommandOptions.Comparer"/>.
    /// </remarks>
    /// <param name="parameterName">The name of the command parameter that this set attempts to match to.</param>
    /// <param name="value">The value returned when an item is discovered in the set.</param>
    /// <returns><see langword="true"/> when an item was discovered in the set, otherwise <see langword="false"/>.</returns>
    public bool TryNext(string parameterName, out object? value)
    {
        if (_namedArgs.TryGetValue(parameterName, out value))
            return true;

        if (_indexUnnamed >= _unnamedArgs.Length)
            return false;

        value = _unnamedArgs[_indexUnnamed++];

        return true;
    }

    /// <summary>
    ///     Makes an attempt to retrieve the next argument in the set, exclusively browsing unnamed arguments to match in search operations.
    /// </summary>
    /// <param name="searchHeight">The next incrementation that the search operation should attempt to match in the command set.</param>
    /// <param name="value">The value returned when an item is discovered in the set.</param>
    /// <returns><see langword="true"/> when an item was discovered in the set, otherwise <see langword="false"/>.</returns>
#if NET8_0_OR_GREATER
    public readonly bool TryNext(int searchHeight, [NotNullWhen(true)] out string? value)
#else
    public readonly bool TryNext(int searchHeight, out string? value)
#endif
    {
        if (searchHeight < _unnamedArgs.Length && _unnamedArgs[searchHeight] is string str)
        {
            value = str;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    ///     Sets the size of the set, reducing the length by the search height that ended up discovering the command.
    /// </summary>
    /// <param name="searchHeight">The final incrementation that the search operation returned the discovered result with.</param>
    public void SetSize(int searchHeight)
    {
        _indexUnnamed = searchHeight;
        _size = _unnamedArgs.Length - searchHeight;
    }

    /// <summary>
    ///     Joins the remaining unnamed arguments in the set into a single string.
    /// </summary>
    /// <returns>A joined string containing all remaining arguments in this enumerator.</returns>
    public readonly string JoinRemaining(char separator = U0020)
#if NET8_0_OR_GREATER
        => string.Join(separator, _unnamedArgs[_indexUnnamed..]);
#else
    {
        var sb = new StringBuilder();
        for (var i = _indexUnnamed; i < _unnamedArgs.Length; i++)
        {
            sb.Append(_unnamedArgs[i]);
            if (i < _unnamedArgs.Length - 1)
                sb.Append(separator);
        }

        return sb.ToString();
    }
#endif

    /// <summary>
    ///     Takes the remaining unnamed arguments in the set into an array which is used by Collector arguments.
    /// </summary>
    /// <returns>An array of objects that represent the remaining arguments of this enumerator.</returns>
    public readonly object[] TakeRemaining()
#if NET8_0_OR_GREATER
        => _unnamedArgs[_indexUnnamed..];
#else
        => _unnamedArgs.Skip(_indexUnnamed).ToArray();
#endif

    /// <summary>
    ///     Implicitly converts an array of objects into an <see cref="ArgumentEnumerator"/> instance.
    /// </summary>
    /// <param name="args">An array of objects to create a new <see cref="ArgumentEnumerator"/> from.</param>
    public static implicit operator ArgumentEnumerator(object[] args)
        => new(args);

    /// <summary>
    ///     Implicitly converts an array of key-value pairs into an <see cref="ArgumentEnumerator"/> instance, using the standard <see cref="StringComparer.OrdinalIgnoreCase"/> to compare keys.
    /// </summary>
    /// <param name="args">An array of <see cref="KeyValuePair{TKey, TValue}"/>'s to create a new <see cref="ArgumentEnumerator"/> from.</param>
    public static implicit operator ArgumentEnumerator(KeyValuePair<string, object?>[] args)
        => new(args, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Implicitly converts a string into an <see cref="ArgumentEnumerator"/> instance, using the standard <see cref="StringComparer.OrdinalIgnoreCase"/> to compare keys.
    /// </summary>
    /// <param name="args">The string to parse, and then create a new <see cref="ArgumentEnumerator"/> from.</param>
    public static implicit operator ArgumentEnumerator(string args)
#if NET8_0_OR_GREATER
        => new(ArgumentReader.ReadNamed(args), StringComparer.OrdinalIgnoreCase);
#else
        => new(ArgumentReader.Read(args));
#endif
}
