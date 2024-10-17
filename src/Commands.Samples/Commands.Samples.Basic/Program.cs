﻿using Commands;
using Commands.Console;
using Commands.Parsing;
using Spectre.Console;

var app = CommandManager.CreateBuilder()
    .AddCommand("command", () =>
    {
        return "This is my first command!";
    })
    .AddCommand("help", (CommandContext<ConsoleConsumerBase> ctx) =>
    {
        var commands = ctx.Manager.GetCommands();

        foreach (var command in commands)
        {
            var description = command.GetAttribute<DescriptionAttribute>()?.Description ?? "No description available.";

            ctx.SendAsync($"[yellow]{command.ToString().EscapeMarkup()}[/]");
            ctx.SendAsync($"[blue]{description}[/]");
        }
    })
    .AddResultResolver((consumer, result, services) =>
    {
        if (!result.Success)
        {
            consumer.SendAsync(result.Exception!);
        }
    })
    .Build();

while (true)
{
    var input = StringParser.Parse(Console.ReadLine());

    var consumer = new ConsoleConsumerBase();

    await app.TryExecuteAsync(consumer, input);
}