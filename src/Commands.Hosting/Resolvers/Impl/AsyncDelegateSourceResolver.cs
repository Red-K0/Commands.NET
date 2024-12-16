﻿namespace Commands.Resolvers
{
    internal sealed class AsyncDelegateSourceResolver(
        Func<ValueTask<SourceResult>> func) : SourceResolver
    {
        private readonly Func<ValueTask<SourceResult>> _func = func;

        public override ValueTask<SourceResult> Evaluate(CancellationToken cancellationToken)
        {
            return _func();
        }
    }
}
