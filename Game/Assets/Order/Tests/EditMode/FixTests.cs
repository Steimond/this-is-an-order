using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Order.Content;

namespace Order.Tests.EditMode
{
    // The fixed-point number the simulation runs on: exact rounding rules, checked against wide integers and doubles here,
    // where floating point is allowed.
    public sealed class FixTests
    {
        static readonly BigInteger One = BigInteger.One << Fix.FractionBits;
        static Fix Raw(long raw) => Fix.FromRaw(raw);
        static long[] Samples()
        {
            var random = new System.Random(7); var samples = new long[2000];
            for (int i = 0; i < samples.Length; i++)
            {
                // Magnitudes from 2⁻³² to 2²⁰, both signs: the map's coordinates, distances, squares and timers.
                int bits = random.Next(1, 52); long magnitude = ((long)random.Next() << 31 | (long)random.Next()) & ((1L << bits) - 1);
                samples[i] = random.Next(2) == 0 ? magnitude : -magnitude;
            }
            return samples;
        }
        static BigInteger FloorDiv(BigInteger a, BigInteger b) { var q = BigInteger.DivRem(a, b, out var r); return r.Sign != 0 && (r.Sign < 0) != (b.Sign < 0) ? q - 1 : q; }

        [Test] public void ProductRoundsDown()
        {
            var s = Samples();
            for (int i = 0; i + 1 < s.Length; i += 2)
            {
                var expected = FloorDiv((BigInteger)s[i] * s[i + 1], One);
                if (BigInteger.Abs(expected) > long.MaxValue) continue;
                Assert.That((Raw(s[i]) * Raw(s[i + 1])).Raw, Is.EqualTo((long)expected), $"{s[i]} × {s[i + 1]}");
            }
        }
        [Test] public void QuotientRoundsTowardZero()
        {
            var s = Samples();
            for (int i = 0; i + 1 < s.Length; i += 2)
            {
                if (s[i + 1] == 0) continue;
                var expected = BigInteger.Divide((BigInteger)s[i] * One, s[i + 1]);
                if (BigInteger.Abs(expected) > long.MaxValue) continue;
                Assert.That((Raw(s[i]) / Raw(s[i + 1])).Raw, Is.EqualTo((long)expected), $"{s[i]} ÷ {s[i + 1]}");
            }
            Assert.That((Fix)1 / 3 * 3, Is.Not.EqualTo((Fix)1), "A third is not exact");
            Assert.That(((Fix)7 / 2).Raw, Is.EqualTo(((Fix)3 + Fix.Half).Raw));
        }
        [Test] public void RootRoundsDown()
        {
            foreach (var raw in Samples())
            {
                if (raw < 0) continue;
                var root = (BigInteger)Fix.Sqrt(Raw(raw)).Raw; var square = (BigInteger)raw * One;
                Assert.That(root * root <= square && (root + 1) * (root + 1) > square, Is.True, $"√{raw}");
            }
            Assert.That(Fix.Sqrt(16), Is.EqualTo((Fix)4));
            Assert.That(Fix.Sqrt(-1), Is.EqualTo(Fix.Zero));
        }
        [Test] public void SineAndCosineAreCloseToTheTrueOnes()
        {
            for (int i = -400; i <= 400; i++)
            {
                double a = i * .05;
                Assert.That((double)Fix.Sin((Fix)a), Is.EqualTo(Math.Sin(a)).Within(1e-7), $"sin {a}");
                Assert.That((double)Fix.Cos((Fix)a), Is.EqualTo(Math.Cos(a)).Within(1e-7), $"cos {a}");
            }
        }
        [Test] public void ConversionsAndRounding()
        {
            Assert.That(((Fix).5f).Raw, Is.EqualTo(1L << 31));
            Assert.That((Fix)float.NaN, Is.EqualTo(Fix.MinValue), "NaN lands outside the map");
            Assert.That((int)(Fix)(-2.5f), Is.EqualTo(-2), "Toward zero, as a float cast");
            Assert.That((float)(Fix)13.5f, Is.EqualTo(13.5f));
            Assert.That(Fix.Round((Fix)2.5f), Is.EqualTo((Fix)2)); Assert.That(Fix.Round((Fix)3.5f), Is.EqualTo((Fix)4));
            Assert.That(Fix.Floor((Fix)(-1.25f)), Is.EqualTo((Fix)(-2))); Assert.That(Fix.Ceiling((Fix)1.25f), Is.EqualTo((Fix)2));
            Assert.That(((Fix)(-7.5f) % 2), Is.EqualTo((Fix)(-1.5f)));
        }

        // The rules must never compute in floating point: no float or double, no System.Math, no fractional literal that is
        // not turned into a Fix on the spot (an int times 0.5f would be float arithmetic).
        static readonly Regex Banned = new Regex(@"\b(?:float|double|decimal|Mathf|UnityEngine)\b|System\.Math\b");
        static readonly Regex Literal = new Regex(@"(?<![\w.]|[eE][-+])(?:\d+\.\d*|\.\d+|\d+(?=[eEfFdDmM]))(?:[eE][-+]?\d+)?[fFdDmM]?\b");
        [Test] public void SimulationSourcesHoldNoFloatingPoint()
        {
            var folder = Path.Combine(UnityEngine.Application.dataPath, "Order", "Simulation");
            var files = Directory.GetFiles(folder, "*.cs");
            Assert.That(files, Is.Not.Empty);
            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    // The code of the line, without its comment and string contents.
                    var code = Regex.Replace(lines[i], "\"(?:[^\"\\\\]|\\\\.)*\"", "\"\"");
                    int comment = code.IndexOf("//", StringComparison.Ordinal); if (comment >= 0) code = code.Substring(0, comment);
                    var where = $"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}";
                    Assert.That(Banned.IsMatch(code), Is.False, where);
                    foreach (Match m in Literal.Matches(code))
                        Assert.That(code.Substring(0, m.Index).EndsWith("(Fix)", StringComparison.Ordinal), Is.True, where);
                }
            }
        }
    }
}
