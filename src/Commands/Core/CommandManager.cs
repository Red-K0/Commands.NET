﻿using Commands.Exceptions;
using Commands.Helpers;
using Commands.Reflection;
using Commands.TypeConverters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;

[assembly: CLSCompliant(true)]

namespace Commands.Core
{
    /// <summary>
    ///     The root type serving as a basis for all operations and functionality as provided by Commands.NET.
    /// </summary>
    /// <remarks>
    ///     To learn more about use of this type and other features of Commands.NET, check out the README on GitHub: <see href="https://github.com/csmir/Commands.NET"/>
    /// </remarks>
    /// <param name="services"></param>
    /// <param name="logFactory"></param>
    /// <param name="finalizer"></param>
    /// <param name="converters"></param>
    /// <param name="options"></param>
    [DebuggerDisplay("Commands = {Commands}")]
    public class CommandManager(
        IServiceProvider services, ILoggerFactory logFactory, CommandFinalizer finalizer, IEnumerable<TypeConverterBase> converters, BuildOptions options)
    {
        private readonly object s_lock = new();

        private readonly CommandFinalizer _finalizer = finalizer;
        private readonly IServiceProvider _services = services;
        private readonly ILoggerFactory _logFactory = logFactory;

        /// <summary>
        ///     Gets the collection containing all commands, groups and subcommands as implemented by the assemblies that were registered in the <see cref="BuildOptions"/> provided when creating the manager.
        /// </summary>
        public HashSet<IConditional> Commands { get; } =
        [
            .. ReflectionHelpers.BuildComponents(converters, options)
                        .Concat(options.Commands)
                        .OrderByDescending(x => x.Score)
        ];

        /// <summary>
        ///     Makes an attempt at executing a command from provided <paramref name="args"/>.
        /// </summary>
        /// <remarks>
        ///     The arguments intended for searching for a target need to be <see cref="string"/>, as <see cref="ModuleInfo"/> and <see cref="CommandInfo"/> store their aliases this way also.
        /// </remarks>
        /// <param name="consumer">A command context that persist for the duration of the execution pipeline, serving as a metadata and logging container.</param>
        /// <param name="args">A set of arguments that are expected to discover, populate and invoke a target command.</param>
        /// <param name="options">A collection of options that determines pipeline logic.</param>
        /// <returns>An awaitable <see cref="Task"/> hosting the state of execution. This task should be awaited, even if <see cref="CommandOptions.AsyncMode"/> is set to <see cref="AsyncMode.Discard"/>.</returns>
        public virtual async Task TryExecuteAsync<T>(
            T consumer, object[] args, CommandOptions? options = default)
            where T : ConsumerBase
        {
            options ??= new CommandOptions();

            switch (options.AsyncMode)
            {
                case AsyncMode.Await:
                    {
                        await ExecuteAsync(consumer, args, options);
                    }
                    return;
                case AsyncMode.Discard:
                    {
                        _ = ExecuteAsync(consumer, args, options);
                    }
                    return;
            }
        }

        /// <summary>
        ///     Runs a thread safe search operation over all commands for any matches of <paramref name="args"/>.
        /// </summary>
        /// <param name="args">A set of arguments intended to discover commands as a query.</param>
        /// <returns>A lazily evaluated <see cref="IEnumerable{T}"/> that holds the results of the search query.</returns>
        public virtual IEnumerable<SearchResult> Search(object[] args)
        {
            if (args == null)
            {
                ThrowHelpers.ThrowInvalidArgument(args);
            }

            lock (s_lock)
            {
                return Commands.SearchMany(args, 0, false);
            }
        }

        /// <summary>
        ///     Steps through the pipeline in order to execute a command based on the provided <paramref name="args"/>.
        /// </summary>
        /// <param name="consumer">A command context that persist for the duration of the execution pipeline, serving as a metadata and logging container.</param>
        /// <param name="args">A set of arguments that are expected to discover, populate and invoke a target command.</param>
        /// <param name="options">A collection of options that determines pipeline logic.</param>
        /// <returns>An awaitable <see cref="Task"/> hosting the state of execution.</returns>
        protected virtual async Task ExecuteAsync<T>(
            T consumer, object[] args, CommandOptions options)
            where T : ConsumerBase
        {
            ICommandResult? result = null;

            var searches = Search(args);

            options.Scope ??= _services.CreateAsyncScope();

            foreach (var search in searches)
            {
                if (search.Component is CommandInfo command)
                {
                    result = await RunAsync(consumer, command, search.SearchHeight, args, options);
                    
                    if (!result.Success)
                    {
                        continue;
                    }

                    break;
                }

                result ??= search;
                continue;
            }

            result ??= SearchResult.FromError();

            await _finalizer.FinalizeAsync(consumer, result, options);
        }

        /// <summary>
        ///     Invokes the provided <paramref name="command"/> and returns the result.
        /// </summary>
        /// <param name="consumer">A command context that persist for the duration of the execution pipeline, serving as a metadata and logging container.</param>
        /// <param name="command">The result of the match intended to be ran.</param>
        /// <param name="argHeight">The height at which the command name ends and argument input starts.</param>
        /// <param name="args">A set of arguments that are expected to discover, populate and invoke a target command.</param>
        /// <param name="options">A collection of options that determines pipeline logic.</param>
        /// <returns>An awaitable <see cref="ValueTask"/> holding the result of the invocation process.</returns>
        protected virtual async ValueTask<ICommandResult> RunAsync<T>(
            T consumer, CommandInfo command, int argHeight, object[] args, CommandOptions options)
            where T : ConsumerBase
        {
            var conversion = await ConvertAsync(consumer, command, argHeight, args, options);

            var arguments = new object[conversion.Length];

            for (int i = 0; i < conversion.Length; i++)
            {
                if (!conversion[i].Success)
                    return MatchResult.FromError(command, conversion[i].Exception!);

                arguments[i] = conversion[i].Value!;
            }

            try
            {
                var preCheckResult = await CheckPreconditionsAsync(consumer, command, options);

                if (!preCheckResult.Success)
                {
                    return preCheckResult;
                }

                var result = await command.Invoker.InvokeAsync(consumer, command, arguments, options);

                var postCheckResult = await CheckPostconditionsAsync(consumer, result, options);

                if (!postCheckResult.Success)
                {
                    return postCheckResult;
                }

                return result;
            }
            catch (Exception exception)
            {
                return InvokeResult.FromError(command, exception);
            }
        }

        private async ValueTask<ConvertResult[]> ConvertAsync<T>(
            T consumer, CommandInfo command, int argHeight, object[] args, CommandOptions options)
            where T : ConsumerBase
        {
            if (!command.HasArguments)
            {
                return [];
            }

            var length = args.Length - argHeight;

            if (command.MaxLength == length)
            {
                return await command.Arguments.ConvertManyAsync(consumer, args[^length..], 0, options);
            }

            if (command.MaxLength <= length && command.HasRemainder)
            {
                return await command.Arguments.ConvertManyAsync(consumer, args[^length..], 0, options);
            }

            if (command.MaxLength > length && command.MinLength <= length)
            {
                return await command.Arguments.ConvertManyAsync(consumer, args[^length..], 0, options);
            }

            return [ConvertResult.FromError(ConvertException.ArgumentMismatch())];
        }

        private async ValueTask<ConditionResult> CheckPreconditionsAsync<T>(
            T consumer, CommandInfo command, CommandOptions options)
            where T : ConsumerBase
        {
            if (options.SkipPreconditions)
            {
                return ConditionResult.FromSuccess();
            }

            foreach (var precon in command.Preconditions)
            {
                var checkResult = await precon.EvaluateAsync(consumer, command, options.Scope!.ServiceProvider, options.CancellationToken);

                if (!checkResult.Success)
                {
                    return checkResult;
                }
            }

            return ConditionResult.FromSuccess();
        }

        private async ValueTask<ConditionResult> CheckPostconditionsAsync<T>(
            T consumer, InvokeResult result, CommandOptions options)
            where T : ConsumerBase
        {
            if (options.SkipPostconditions)
            {
                return ConditionResult.FromSuccess();
            }

            foreach (var postcon in result.Command.PostConditions)
            {
                var checkResult = await postcon.EvaluateAsync(consumer, result, options.Scope.ServiceProvider, options.CancellationToken);

                if (!checkResult.Success)
                {
                    return checkResult;
                }
            }

            return ConditionResult.FromSuccess();
        }
    }
}
