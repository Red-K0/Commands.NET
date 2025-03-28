﻿namespace Commands.Testing;

/// <summary>
///     A set of properties for a test provider.
/// </summary>
public sealed class TestProviderProperties
{
    private string? _arguments;
    private Command? _command;
    private TestResultType _result;

    /// <summary>
    ///     Creates a new instance of <see cref="TestProviderProperties"/>.
    /// </summary>
    public TestProviderProperties()
    {
        _command = null;
        _result = TestResultType.Success;
    }

    /// <summary>
    ///     Sets the command this provider should test.
    /// </summary>
    /// <param name="command">The command to set.</param>
    /// <returns>The same <see cref="TestProviderProperties"/> for call-chaining.</returns>
    public TestProviderProperties Command(Command command)
    {
        Assert.NotNull(command, nameof(command));

        _command = command;

        return this;
    }

    /// <summary>
    ///     Sets the arguments this provider should test with.
    /// </summary>
    /// <param name="arguments">The arguments to set.</param>
    /// <returns>The same <see cref="TestProviderProperties"/> for call-chaining.</returns>
    public TestProviderProperties Arguments(string? arguments)
    {
        _arguments = arguments;

        return this;
    }

    /// <summary>
    ///     Sets the result this provider should return.
    /// </summary>
    /// <param name="result">The result to set.</param>
    /// <returns>The same <see cref="TestProviderProperties"/> for call-chaining.</returns>
    public TestProviderProperties Result(TestResultType result)
    {
        _result = result;

        return this;
    }

    /// <summary>
    ///     Converts the properties to a new instance of <see cref="TestProvider"/>.
    /// </summary>
    /// <returns>A new instance of <see cref="TestProvider"/>.</returns>
    public TestProvider Create()
    {
        Assert.NotNull(_command, nameof(_command));

        return new TestProvider(_command!, _arguments ?? string.Empty, _result);
    }
}
