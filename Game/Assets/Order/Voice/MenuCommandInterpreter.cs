using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Order.Contracts;

namespace Order.Voice
{
    // Explicit menu commands are resolved by MenuCommandGrammar without the model; everything else goes to the inner interpreter.
    public sealed class MenuCommandInterpreter : IAppIntentInterpreter
    {
        private readonly IAppIntentInterpreter fallback;
        public MenuCommandInterpreter(IAppIntentInterpreter fallback) { this.fallback = fallback; }

        public Task<Interpretation> InterpretAsync(string text, AppCommandState state, CancellationToken cancellationToken)
        {
            var watch = Stopwatch.StartNew();
            if (MenuCommandGrammar.TryParse(text, state, out var result))
            {
                var elapsed = watch.Elapsed.TotalMilliseconds;
                return Task.FromResult(result.WithTiming(new IntentTiming { Route = "command", GroundingMs = elapsed, TotalMs = elapsed }));
            }
            return fallback.InterpretAsync(text, state, cancellationToken);
        }
    }
}
