using Order.Contracts;
using Order.Voice;

namespace Order.Input
{
    public sealed class CommandMeasurement
    {
        public double TotalMs;
        public double InterpretationMs;
        public double ExecutionMs;
        public CommandOutcome Outcome;
        public IntentTiming Intent;
    }
}
