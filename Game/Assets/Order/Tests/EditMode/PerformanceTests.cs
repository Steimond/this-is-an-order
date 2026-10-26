using System.Diagnostics;
using NUnit.Framework;
using Order.Presentation;
using Order.Simulation;

namespace Order.Tests.EditMode
{
    // A tripwire for the simulation's pace (DESIGN.md, section 10): the stress battle of every fighting unit of both
    // factions — new kinds join it by themselves — must keep its step well inside a wide limit. It catches a gross slip,
    // such as a new search over all units; the real comparison with the baseline is Order → Benchmark Simulation and a
    // Player build of the stress lab, after every new unit or building.
    public sealed class PerformanceTests
    {
        // The editor's baseline for 1000 units over 300 steps is 6 ms a step (2026-10-26); the limit leaves room for a
        // slower machine and a busy editor.
        const double LimitMs = 15;
        [Test] public void StressBattleKeepsItsPace()
        {
            var match = StressBattle.Create(null, 1000);
            for (int i = 0; i < 20; i++) match.Advance();
            const int steps = 200;
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < steps; i++) match.Advance();
            double mean = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / steps;
            Assert.That(mean, Is.LessThan(LimitMs), $"A step of the 1000-unit stress battle took {mean:0.0} ms on average");
        }
    }
}
