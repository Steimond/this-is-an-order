using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Order.Contracts;
using Order.Voice;

namespace Order.Input
{
    public sealed class AppCommandInput
    {
        private readonly IAppCommandSink sink;
        private readonly IAppIntentInterpreter interpreter;
        private long latestRequest;
        public CommandMeasurement LastMeasurement { get; private set; }
        public AppCommandInput(IAppCommandSink sink, IAppIntentInterpreter interpreter)
        { this.sink = sink ?? throw new ArgumentNullException(nameof(sink)); this.interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter)); }
        public CommandResult SubmitManual(AppCommand command) => sink.Execute(command, new CommandContext(sink.Session, CommandSource.Manual));
        public async Task<CommandResult> SubmitTranscriptAsync(string text, CommandContext capturedContext, CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            var measurement = new CommandMeasurement();
            CommandResult Finish(CommandResult result)
            {
                measurement.TotalMs = watch.Elapsed.TotalMilliseconds;
                measurement.Outcome = result.Outcome;
                LastMeasurement = measurement;
                return result;
            }
            var request = ++latestRequest;
            if (capturedContext.Session != sink.Session)
                return Finish(new CommandResult(CommandOutcome.StaleContext, "Команда относится к предыдущему сеансу."));
            if (string.IsNullOrWhiteSpace(text)) return Finish(new CommandResult(CommandOutcome.NeedsClarification, "Введите команду."));
            var state = sink.State;
            if (capturedContext.Revision >= 0 && capturedContext.Revision != state.Revision)
                return Finish(new CommandResult(CommandOutcome.StaleContext, "Состояние изменилось. Повторите команду."));
            var interpretationWatch = Stopwatch.StartNew();
            var interpreting = true;
            try
            {
                var stage = Stopwatch.StartNew();
                var parsed = await interpreter.InterpretAsync(text, state, cancellationToken);
                measurement.InterpretationMs = stage.Elapsed.TotalMilliseconds;
                interpreting = false;
                measurement.Intent = parsed.Timing;
                cancellationToken.ThrowIfCancellationRequested();
                if (request != latestRequest || state.Session != sink.Session || state.Revision != sink.State.Revision)
                    return Finish(new CommandResult(CommandOutcome.StaleContext, "Состояние изменилось. Повторите команду."));
                stage.Restart();
                var result = parsed.HasCommand ? sink.Execute(parsed.Command, new CommandContext(state.Session, capturedContext.Source, state.Revision))
                    : new CommandResult(parsed.IsAnswer ? CommandOutcome.Answered : CommandOutcome.NeedsClarification, parsed.Message);
                measurement.ExecutionMs = stage.Elapsed.TotalMilliseconds;
                return Finish(result);
            }
            catch (OperationCanceledException)
            {
                if (interpreting) measurement.InterpretationMs = interpretationWatch.Elapsed.TotalMilliseconds;
                return Finish(new CommandResult(CommandOutcome.Rejected, "Обработка команды отменена."));
            }
            catch (Exception)
            {
                if (interpreting) measurement.InterpretationMs = interpretationWatch.Elapsed.TotalMilliseconds;
                return Finish(new CommandResult(CommandOutcome.Unavailable, "Ассистент сейчас недоступен. Используйте кнопки меню."));
            }
        }
    }
}
