﻿using Commands.Reflection;

namespace Commands
{
    /// <summary>
    ///     Represents data about a command, as a <see cref="ModuleBase"/> normally would. 
    ///     This context is used for <see langword="static"/> and <see langword="delegate"/> commands.
    /// </summary>
    public sealed class CommandContext<T>(T consumer, CommandInfo command, CommandOptions options)
        where T : ConsumerBase
    {
        /// <summary>
        ///     Gets the consumer of the command currently in scope.
        /// </summary>
        public T Consumer { get; } = consumer;

        /// <summary>
        ///     Gets the options for the command currently in scope.
        /// </summary>
        public CommandOptions Options { get; } = options;

        /// <summary>
        ///     Gets the reflection information about the command currently in scope.
        /// </summary>
        public CommandInfo Command { get; } = command;
    }
}
