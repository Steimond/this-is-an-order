using System;
using Order.Contracts;
using Order.Voice;

namespace Order.Input
{
    public sealed class AppCommandInput
    {
        private readonly IAppCommandSink sink;
        private readonly RussianAppInterpreter interpreter;
        public AppCommandInput(IAppCommandSink sink, RussianAppInterpreter interpreter)
        { this.sink = sink ?? throw new ArgumentNullException(nameof(sink)); this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter)); }
        public CommandResult SubmitManual(AppCommand command) => sink.Execute(command, new CommandContext(sink.Session, CommandSource.Manual));
        public CommandResult SubmitTranscript(string text, CommandContext capturedContext)
        {
            if (capturedContext.Session != sink.Session)
                return new CommandResult(CommandOutcome.StaleContext, "Команда относится к предыдущему сеансу.");
            var parsed = interpreter.Interpret(text);
            return parsed.HasCommand ? sink.Execute(parsed.Command, capturedContext)
                : new CommandResult(CommandOutcome.NeedsClarification, parsed.Message);
        }
    }
}
