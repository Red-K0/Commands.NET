﻿using Commands.Core;
using Commands.Exceptions;
using Commands.Helpers;
using Commands.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace Commands.TypeConverters
{
    /// <inheritdoc />
    /// <typeparam name="T">The type this <see cref="TypeConverterBase{T}"/> should convert into.</typeparam>
    public abstract class TypeConverterBase<T> : TypeConverterBase
    {
        /// <summary>
        ///     Gets the type that should be converted to.
        /// </summary>
        public override Type Type { get; } = typeof(T);

        /// <summary>
        ///     Creates a new <see cref="ConvertResult"/> representing a successful evaluation.
        /// </summary>
        /// <param name="value">The value converted from a raw argument into the target type of this converter.</param>
        /// <returns>A <see cref="ConvertResult"/> representing the successful evaluation.</returns>
        public virtual ConvertResult Success(T value)
        {
            return base.Success(value);
        }
    }

    /// <summary>
    ///     An abstract type that can be implemented to create custom type conversion from a command query argument.
    /// </summary>
    /// <remarks>
    ///     Registering custom <see cref="TypeConverterBase"/>'s is not an automated process. 
    ///     To register them for the <see cref="CommandManager"/> to use, add them to your <see cref="IServiceProvider"/> using <see cref="ServiceHelpers.TryAddConverter(Microsoft.Extensions.DependencyInjection.IServiceCollection, TypeConverterBase)"/>
    /// </remarks>
    public abstract class TypeConverterBase
    {
        const string _exHeader = "TypeConverter failed to parse provided value as '{0}'. View inner exception for more details.";

        /// <summary>
        ///     Gets the type that should be converted to. This value determines what command arguments will use this converter.
        /// </summary>
        /// <remarks>
        ///     It is important to ensure this converter actually returns the specified type in <see cref="Success(object)"/>. If this is not the case, a critical exception will occur in runtime when the command is attempted to be executed.
        /// </remarks>
        public abstract Type Type { get; }

        /// <summary>
        ///     Evaluates the known data about the argument to be converted into, as well as the raw value it should convert into a valid invocation parameter.
        /// </summary>
        /// <param name="consumer">Context of the current execution.</param>
        /// <param name="services">The provider used to register modules and inject services.</param>
        /// <param name="argument">Information about the invocation argument this evaluation converts for.</param>
        /// <param name="value">The raw command query argument to convert.</param>
        /// <param name="cancellationToken">The token to cancel the operation.</param>
        /// <returns>An awaitable <see cref="ValueTask"/> that contains the result of the evaluation.</returns>
        public abstract ValueTask<ConvertResult> EvaluateAsync(
            ConsumerBase consumer, IArgument argument, string value, IServiceProvider services, CancellationToken cancellationToken);

        internal ValueTask<ConvertResult> ObjectEvaluateAsync(
            ConsumerBase consumer, IArgument argument, object value, IServiceProvider services, CancellationToken cancellationToken)
        {
            if (value is string str)
                return EvaluateAsync(consumer, argument, str, services, cancellationToken);

            return EvaluateAsync(consumer, argument, value.ToString(), services, cancellationToken);
        }

        /// <summary>
        ///     Creates a new <see cref="ConvertResult"/> representing a failed evaluation.
        /// </summary>
        /// <param name="exception">The exception that caused the evaluation to fail.</param>
        /// <returns>A <see cref="ConvertResult"/> representing the failed evaluation.</returns>
        protected ConvertResult Error([DisallowNull] Exception exception)
        {
            if (exception == null)
            {
                ThrowHelpers.ThrowInvalidArgument(exception);
            }

            if (exception is ConvertException convertEx)
            {
                return new(convertEx);
            }
            return new(new ConvertException(string.Format(_exHeader, Type.Name), exception));
        }

        /// <summary>
        ///     Creates a new <see cref="ConvertResult"/> representing a failed evaluation.
        /// </summary>
        /// <param name="error">The error that caused the evaluation to fail.</param>
        /// <returns>A <see cref="ConvertResult"/> representing the failed evaluation.</returns>
        protected ConvertResult Error([DisallowNull] string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                ThrowHelpers.ThrowInvalidArgument(error);
            }

            return new(new ConvertException(error));
        }

        /// <summary>
        ///     Creates a new <see cref="ConvertResult"/> representing a successful evaluation.
        /// </summary>
        /// <param name="value">The value converted from a raw argument into the target type of this converter.</param>
        /// <returns>A <see cref="ConvertResult"/> representing the successful evaluation.</returns>
        protected ConvertResult Success(object value)
        {
            return new(value);
        }

        internal static TypeConverterBase[] BuildDefaults()
        {
            var arr = ValueTypeConverter.CreateBaseConverters();

            new TimeSpanConverter().AddTo(ref arr);

            return arr;
        }

        internal class EqualityComparer : IEqualityComparer<TypeConverterBase>
        {
            private static readonly Lazy<EqualityComparer> _i = new();

            public bool Equals(TypeConverterBase x, TypeConverterBase y)
            {
                if (x == y)
                    return true;

                if (x.Type == y.Type)
                    return true;

                return false;
            }

            public int GetHashCode([DisallowNull] TypeConverterBase obj)
            {
                return obj.GetHashCode();
            }

            public static EqualityComparer Default
            {
                get
                {
                    return _i.Value;
                }
            }
        }
    }
}
