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
        // An informational reply (help, current value) rather than a request to clarify.
        public readonly bool IsAnswer;
        // The model's grounded intent, kept so the phrasebook learns meaning (e.g. "-10"), not the executed value.
        public readonly IntentDecision Decision;
        public readonly IntentTiming Timing;
        public Interpretation(AppCommand command) { HasCommand = true; Command = command; Message = ""; IsAnswer = false; Decision = null; Timing = null; }
        public Interpretation(string clarification) { HasCommand = false; Command = default; Message = clarification; IsAnswer = false; Decision = null; Timing = null; }
        private Interpretation(string message, bool answer) { HasCommand = false; Command = default; Message = message; IsAnswer = answer; Decision = null; Timing = null; }
        private Interpretation(Interpretation result, IntentDecision decision, IntentTiming timing)
        { HasCommand = result.HasCommand; Command = result.Command; Message = result.Message; IsAnswer = result.IsAnswer; Decision = decision; Timing = timing; }
        public static Interpretation Answer(string message) => new Interpretation(message, true);
        public Interpretation WithTiming(IntentTiming timing) => new Interpretation(this, Decision, timing);
        public Interpretation WithDecision(IntentDecision decision) => new Interpretation(this, decision, Timing);
    }

    public interface IAppIntentInterpreter
    {
        Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken);
    }
}
