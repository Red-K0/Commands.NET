﻿using Commands.Parsing;

namespace Commands;

/// <summary>
///     Provides a set of helper functions for working with components.
/// </summary>
public static class ComponentUtilities
{
    /// <summary>
    ///     Gets the first entry of the specified type, or <see langword="null"/> if it does not exist.
    /// </summary>
    /// <typeparam name="T">The type to filter.</typeparam>
    /// <param name="values"></param>
    /// <returns>The first occurrence of <typeparamref name="T"/> in the collection if any exists, otherwise <see langword="null"/>.</returns>
    public static T? FirstOrDefault<T>(this IEnumerable values)
        => values.OfType<T>().FirstOrDefault();

    /// <summary>
    ///     Checks if the collection contains an instance of the specified type.
    /// </summary>
    /// <typeparam name="T">The type to filter.</typeparam>
    /// <param name="values"></param>
    /// <returns><see langword="true"/> if a any <typeparamref name="T"/> was found, otherwise <see langword="false"/>.</returns>
    public static bool Contains<T>(this IEnumerable values)
    {
        foreach (var entry in values)
        {
            if (entry is T)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Gets all instances of the specified type, matching the provided predicate.
    /// </summary>
    /// <typeparam name="T">The type to filter.</typeparam>
    /// <param name="values"></param>
    /// <param name="predicate">The predicate which determines whether the component can be returned or not.</param>
    /// <returns>A new <see cref="IEnumerable{T}"/> containing all legible values of <typeparamref name="T"/> in the initial collection.</returns>
    public static IEnumerable<T> OfType<T>(this IEnumerable values, Predicate<T> predicate)
    {
        foreach (var entry in values)
        {
            if (entry is T tEntry && predicate(tEntry))
                yield return tEntry;
        }
    }

    #region Internal Helpers

    #region Execution

    internal static async ValueTask<ParseResult[]> Parse(this IParameterCollection provider, ICallerContext caller, ArgumentArray args, CommandOptions options)
    {
        options.CancellationToken.ThrowIfCancellationRequested();

        var results = new ParseResult[provider.Parameters.Length];

        for (int i = 0; i < provider.Parameters.Length; i++)
        {
            var argument = provider.Parameters[i];

            if (argument.IsRemainder)
            {
                results[i] = await argument.Parse(caller, argument.IsCollection ? args.TakeRemaining(argument.Name!) : args.TakeRemaining(argument.Name!, options.RemainderSeparator), options.Services, options.CancellationToken).ConfigureAwait(false);

                break;
            }

            if (argument is ConstructibleParameter complexParameter)
            {
                var result = await complexParameter.Parse(caller, args, options).ConfigureAwait(false);

                if (result.All(x => x.Success))
                {
                    try
                    {
                        results[i] = ParseResult.FromSuccess(complexParameter.Activator.Invoke(caller, null, result.Select(x => x.Value).ToArray(), options));
                    }
                    catch (Exception ex)
                    {
                        results[i] = ParseResult.FromError(ex);
                    }

                    continue;
                }

                if (complexParameter.IsOptional)
                    results[i] = ParseResult.FromSuccess(Type.Missing);

                continue;
            }

            if (args.TryGetElement(argument.Name!, out var value))
                results[i] = await argument.Parse(caller, value, options.Services, options.CancellationToken).ConfigureAwait(false);
            else if (argument.IsOptional)
                results[i] = ParseResult.FromSuccess(Type.Missing);
            else
                results[i] = ParseResult.FromError(new ArgumentNullException(argument.Name));
        }

        return results;
    }

    #endregion

    #region Building

    internal static IEnumerable<Attribute> GetAttributes(this ICustomAttributeProvider provider, bool inherit)
        => provider.GetCustomAttributes(inherit).OfType<Attribute>();

    internal static bool HasContextProvider(this MethodBase method)
        => method.GetParameters().Length > 0 && method.GetParameters()[0].ParameterType.IsGenericType && method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(CommandContext<>);

    internal static bool IsImplementationOfModule(this Type type)
        => typeof(CommandModule).IsAssignableFrom(type) && !type.IsAbstract && !type.ContainsGenericParameters;

    internal static IEnumerable<CommandGroup> BuildGroups(ComponentConfiguration configuration, IEnumerable<DynamicType> types, CommandGroup? parent, bool isNested)
    {
        Assert.NotNull(types, nameof(types));

        foreach (var definition in types)
        {
            var type = definition.Value;

            if (!isNested && type.IsNested)
                continue;

            CommandGroup? group;

            try
            {
                group = new CommandGroup(type, configuration, parent);
            }
            catch
            {
                // This will throw if the type does not implement CommandModule. We can safely ignore this.
                continue;
            }

            if (group != null && !group.Ignore)
                yield return group;
        }
    }

    internal static IEnumerable<IComponent> BuildCommands(ComponentConfiguration configuration, CommandGroup parent)
    {
        var members = parent.Type!.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);

        foreach (var method in members)
        {
            var command = new Command(method, configuration, parent);

            if (command.Ignore)
                continue;

            yield return command;
        }
    }

#if NET8_0_OR_GREATER
    [UnconditionalSuppressMessage("AotAnalysis", "IL2062", Justification = "The type is propagated from user-facing code, it is up to the user to make it available at compile-time.")]
#endif
    internal static IEnumerable<IComponent> BuildNestedComponents(ComponentConfiguration configuration, CommandGroup parent)
    {
        Assert.NotNull(parent, nameof(parent));

        if (parent.Type == null)
            return [];

        var commands = BuildCommands(configuration, parent);

        try
        {
            var nestedTypes = parent.Type.GetNestedTypes(BindingFlags.Public);

            var groups = BuildGroups(configuration, [.. nestedTypes], parent, true);

            return commands.Concat(groups);
        }
        catch
        {
            // Do nothing, we can't access nested types.
            return commands;
        }
    }

    internal static ICommandParameter[] BuildArguments(IActivator activator, ComponentConfiguration configuration)
    {
        var parameters = activator.Target.GetParameters();

        if (activator.HasContext)
            parameters = parameters.Skip(1).ToArray();

        var arr = new ICommandParameter[parameters.Length];

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].GetCustomAttributes().Contains<DeconstructAttribute>())
                arr[i] = new ConstructibleParameter(parameters[i], configuration);
            else
                arr[i] = new CommandParameter(parameters[i], configuration);
        }

        return arr;
    }

    internal static TypeParser GetParser(this ComponentConfiguration configuration, Type type)
    {
        Assert.NotNull(type, nameof(type));

        if (configuration.Parsers.TryGetValue(type, out var parser))
            return parser;

        if (type.IsEnum)
            return EnumParser.GetOrCreate(type);

        if (type.IsArray)
        {
            type = type.GetElementType()!;

            if (configuration.Parsers.TryGetValue(type, out parser))
                return ArrayParser.GetOrCreate(parser);

            if (type.IsEnum)
                return EnumParser.GetOrCreate(type);
        }

        throw new NotSupportedException($"No parser is known for type {type}.");
    }

    internal static Tuple<int, int> GetLength(this IEnumerable<ICommandParameter> arguments)
    {
        var minLength = 0;
        var maxLength = 0;

        foreach (var parameter in arguments)
        {
            if (parameter is ConstructibleParameter complexArgument)
            {
                maxLength += complexArgument.MaxLength;
                minLength += complexArgument.MinLength;
            }

            if (parameter is CommandParameter defaultArgument)
            {
                maxLength++;
                if (!defaultArgument.IsOptional)
                    minLength++;
            }
        }

        return new(minLength, maxLength);
    }

    internal static IEnumerable<ConstructorInfo> GetAvailableConstructors(
#if NET8_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        this Type type, bool allowMultipleMatches = false)
    {
        var ctors = type.GetConstructors()
            .OrderByDescending(x => x.GetParameters().Length);

        var found = false;
        foreach (var ctor in ctors)
        {
            if (ctor.GetCustomAttributes().Any(attr => attr is IgnoreAttribute))
                continue;

            if (!allowMultipleMatches)
            {
                if (!found)
                {
                    found = true;
                    yield return ctor;
                }
                else
                    yield break;
            }

            yield return ctor;
        }

        if (!found)
            throw new InvalidOperationException($"{type} has no publically available constructors to use in creating instances of this type.");
    }

    #endregion

    #endregion
}
