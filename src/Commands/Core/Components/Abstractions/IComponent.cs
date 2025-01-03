﻿using Commands.Conditions;

namespace Commands;

/// <summary>
///     Reveals information about a conditional component, needing validation in order to become part of execution.
/// </summary>
public interface IComponent : IScorable, IEquatable<IComponent>
{
    /// <summary>
    ///     Gets the full name of the component, including the names of its parent components.
    /// </summary>
    public string FullName { get; }

    /// <summary>
    ///     Gets an array of aliases for this component.
    /// </summary>
    public string[] Aliases { get; }

    /// <summary>
    ///     Gets all evaluations that this component should do during the execution process, determined by a set of defined <see cref="ICondition"/>'s pointing at the component.
    /// </summary>
    /// <remarks>
    ///     When this property is called by a child component, this property will inherit all evaluations from the component's <see cref="Parent"/> component(s).
    /// </remarks>
    public ConditionEvaluator[] Evaluators { get; }

    /// <summary>
    ///     Gets the invocation target of this component.
    /// </summary>
    public IActivator? Activator { get; }

    /// <summary>
    ///     Gets the parent module of this component. This property can be <see langword="null"/>.
    /// </summary>
    public CommandGroup? Parent { get; }

    /// <summary>
    ///     Gets the score of the component.
    /// </summary>
    /// <remarks>
    ///     Score defines the match priority of a component over another. This score is computed based on complexity, argument length and conversion.
    /// </remarks>
    public float Score { get; }

    /// <summary>
    ///     Gets if the component name is queryable.
    /// </summary>
    public bool IsSearchable { get; }

    /// <summary>
    ///     Gets if the component is the default of a module-layer.
    /// </summary>
    public bool IsDefault { get; }
}
