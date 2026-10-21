namespace Order.Voice
{
    // Diagnostics only: these values never affect command decisions.
    public sealed class IntentTiming
    {
        public string Route;
        public double TotalMs;
        public double PreprocessMs;
        public double QueueMs;
        public double StartupMs;
        public double RequestBuildMs;
        public double HttpMs;
        public double DecodeMs;
        public double GroundingMs;
        public bool ProcessColdStart;
        public double? ServerPromptMs;
        public double? ServerGenerationMs;
        public int? PromptTokens;
        public int? CachedTokens;
        public int? EvaluatedPromptTokens;
        public int? GeneratedTokens;
    }
}
