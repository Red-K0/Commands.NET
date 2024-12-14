﻿using Commands.Resolvers;

namespace Commands.Tests
{
    internal class CustomSourceResolver : SourceResolverBase
    {
        public override ValueTask<SourceResult> Evaluate(CancellationToken cancellationToken)
        {
            if (Ready())
            {
                Console.CursorVisible = true;
                Console.Write("> ");

                var src = Console.ReadLine()!;

                Console.CursorVisible = false;

                return ValueTask.FromResult(Success(new ConsumerBase(), CommandParser.ParseKeyCollection(src)));
            }

            return ValueTask.FromResult(Error(new InvalidOperationException("The application failed to start.")));
        }
    }
}
