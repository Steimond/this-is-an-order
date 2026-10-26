using System.Diagnostics;

namespace Order.Simulation
{
    // Where a step's time goes, by phase, for the stress lab and the simulation benchmark. Off unless a profile is set:
    // then every phase adds the processor ticks it took (Stopwatch ticks, Stopwatch.Frequency a second). It only measures;
    // nothing in the rules reads it, so it cannot change the match.
    public sealed class StepProfile
    {
        // The Fight… phases after Vision are parts of Fight (picking targets, closing in, striking), counted inside it.
        public enum Phase { Network, Buildings, Production, Research, Bot, Fight, Move, Separate, Obstacles, Arrival, Works, Cleanup, Vision, FightTargets, FightApproach, FightStrikes }
        public static readonly string[] Names = System.Enum.GetNames(typeof(Phase));
        public readonly long[] Ticks = new long[Names.Length];
        public int Steps;
        public long Total;
        public void Clear() { System.Array.Clear(Ticks, 0, Ticks.Length); Steps = 0; Total = 0; }
    }
    public sealed partial class PrototypeMatch
    {
        public StepProfile Profile;
        long lap;
        void StartLap() { if (Profile != null) lap = Stopwatch.GetTimestamp(); }
        // The time since the last lap goes to this phase.
        void Lap(StepProfile.Phase phase)
        {
            if (Profile == null) return;
            long now = Stopwatch.GetTimestamp();
            Profile.Ticks[(int)phase] += now - lap; Profile.Total += now - lap; lap = now;
        }
        long Mark() => Profile != null ? Stopwatch.GetTimestamp() : 0;
        // A part of a phase, measured on its own and not added to the total again.
        void Spent(StepProfile.Phase phase, long since) { if (Profile != null) Profile.Ticks[(int)phase] += Stopwatch.GetTimestamp() - since; }
    }
}
