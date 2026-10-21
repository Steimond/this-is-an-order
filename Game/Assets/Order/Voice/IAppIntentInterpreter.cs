using System.Threading;
using System.Threading.Tasks;
using Order.Contracts;

namespace Order.Voice
{
    public readonly struct Interpretation
    {
        public readonly bool HasCommand;
        public readonly AppCommand Command;
        public readonly string Message;
        public readonly IntentTiming Timing;
        public Interpretation(AppCommand command) { HasCommand = true; Command = command; Message = ""; Timing = null; }
        public Interpretation(string clarification) { HasCommand = false; Command = default; Message = clarification; Timing = null; }
        private Interpretation(Interpretation result, IntentTiming timing)
        { HasCommand = result.HasCommand; Command = result.Command; Message = result.Message; Timing = timing; }
        public Interpretation WithTiming(IntentTiming timing) => new Interpretation(this, timing);
    }

    public interface IAppIntentInterpreter
    {
        Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken);
    }
}
