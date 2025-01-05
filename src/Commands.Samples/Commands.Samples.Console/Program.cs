﻿using Commands;
using Commands.Conversion;
using Commands.Samples;

var tree = ComponentManager.CreateBuilder()
    .ConfigureComponents(configure =>
    {
        configure.AddParser(new SystemTypeParser(caseIgnore: true));
        configure.AddParser(new TryParseParser<Version>(Version.TryParse));
    })
    .AddResultHandler((caller, result, services) => caller.Respond(result))
    .WithTypes(typeof(Program).Assembly.GetExportedTypes())
    .AddCommandGroup(x => x
        .WithNames("greet")
        .AddCommand(x => x
            .WithNames("self")
            .WithHandler((CommandContext<ConsoleCallerContext> ctx) => $"Hello, {ctx.Caller.Name}"))
        .AddCommand(x => x
            .WithNames("another")
            .WithHandler((string name) => $"Hello, {name}")))
    .Build();

while (true)
{
    var input = Console.ReadLine()!;

    var consumer = new ConsoleCallerContext(name: "Pete");

    tree.TryExecute(consumer, input);
}