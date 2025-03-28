﻿
using Commands;
using Commands.Samples;

var exit = Command.From("exit")
    .Delegate(() => Environment.Exit(0));

var mathCommands = CommandGroup.From("math")
    .Components(
        Command.From(Sum, "sum", "add"),
        Command.From(Subtract, "subtract", "sub"),
        Command.From(Multiply, "multiply", "mul"),
        Command.From(Divide, "divide", "div")
    );

var manager = ComponentCollection.From(exit, mathCommands)
    .Type<HelpModule>()
    .Create();

await manager.Execute(new ConsoleContext(args));

static double Sum(double number, int sumBy)
    => number + sumBy;
static double Subtract(double number, int subtractBy)
    => number - subtractBy;
static double Multiply(double number, int multiplyBy)
    => number * multiplyBy;
static double Divide(double number, int divideBy)
    => number / divideBy;
