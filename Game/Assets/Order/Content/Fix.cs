using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Order.Content
{
    // A fixed-point number, 32 integer and 32 fraction bits in a long: the simulation's only fractional type. Integer
    // arithmetic gives the same bits on every processor (x64, ARM64) and with every compiler, which float does not
    // promise (fused multiply-add, library sin and cos), and lockstep needs every machine to compute the same match.
    // Products and quotients round toward minus infinity and toward zero; they wrap on overflow, past about ±2·10⁹.
    // Conversions from float and double are exact up to the 2⁻³² step and deterministic too, so balance numbers and the
    // view's points enter the simulation the same way everywhere; the way back to float is for the view only.
    [Serializable]
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>, IFormattable
    {
        public const int FractionBits = 32;
        const long OneRaw = 1L << FractionBits, FractionMask = OneRaw - 1;
        public readonly long Raw;
        Fix(long raw) { Raw = raw; }
        public static Fix FromRaw(long raw) => new Fix(raw);

        public static readonly Fix Zero = new Fix(0), One = new Fix(OneRaw), Half = new Fix(OneRaw / 2);
        public static readonly Fix MaxValue = new Fix(long.MaxValue), MinValue = new Fix(long.MinValue);
        // π rounded to the nearest step.
        public static readonly Fix Pi = new Fix(13493037705L), HalfPi = new Fix(13493037705L / 2), TwoPi = new Fix(13493037705L * 2);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static implicit operator Fix(int value) => new Fix((long)value << FractionBits);
        public static implicit operator Fix(float value) => FromDouble(value);
        public static explicit operator Fix(double value) => FromDouble(value);
        static Fix FromDouble(double value)
        {
            // NaN falls below every point of the map, so a NaN coordinate is simply outside it.
            if (double.IsNaN(value)) return MinValue;
            double scaled = value * OneRaw;
            if (scaled >= 9.2e18) return MaxValue;
            if (scaled <= -9.2e18) return MinValue;
            return new Fix((long)Math.Round(scaled));
        }
        public static explicit operator float(Fix value) => (float)(value.Raw / (double)OneRaw);
        public static explicit operator double(Fix value) => value.Raw / (double)OneRaw;
        // Toward zero, as a cast from float does.
        public static explicit operator int(Fix value) => value.Raw >= 0 ? (int)(value.Raw >> FractionBits) : -(int)(-value.Raw >> FractionBits);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix operator +(Fix a, Fix b) => new Fix(a.Raw + b.Raw);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix operator -(Fix a, Fix b) => new Fix(a.Raw - b.Raw);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix operator -(Fix a) => new Fix(-a.Raw);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix operator %(Fix a, Fix b) => new Fix(a.Raw % b.Raw);
        // The 128-bit product of the raw values, shifted back by 32 bits, from 32-bit halves.
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix operator *(Fix a, Fix b)
        {
            long x = a.Raw, y = b.Raw, xHigh = x >> 32, yHigh = y >> 32;
            ulong xLow = (ulong)(x & FractionMask), yLow = (ulong)(y & FractionMask);
            ulong lowLow = xLow * yLow;
            long lowHigh = (long)xLow * yHigh, highLow = xHigh * (long)yLow, highHigh = xHigh * yHigh;
            return new Fix((long)(lowLow >> 32) + lowHigh + highLow + (highHigh << 32));
        }
        public static Fix operator /(Fix a, Fix b)
        {
            if (b.Raw == 0) throw new DivideByZeroException();
            bool negative = (a.Raw < 0) != (b.Raw < 0);
            ulong n = Magnitude(a.Raw), d = Magnitude(b.Raw), whole = n / d, rest = n - whole * d;
            // The 32 fraction bits of rest / d by long division, as many bits at a time as rest has room for.
            ulong fraction = 0; int bits = FractionBits;
            while (bits > 0 && rest != 0)
            {
                int shift = Math.Min(bits, LeadingZeros(rest));
                if (shift == 0)
                {
                    // rest ≥ 2⁶³ and d > rest: doubled it passes d once.
                    rest -= d - rest; fraction = (fraction << 1) | 1; bits--; continue;
                }
                rest <<= shift; ulong digit = rest / d; fraction = (fraction << shift) | digit; rest -= digit * d; bits -= shift;
            }
            fraction <<= bits;
            long raw = (long)((whole << FractionBits) + fraction);
            return new Fix(negative ? -raw : raw);
        }
        static ulong Magnitude(long raw) => raw < 0 ? (ulong)(-(raw + 1)) + 1 : (ulong)raw;
        static int LeadingZeros(ulong v)
        {
            if (v == 0) return 64;
            int n = 0;
            if (v >> 32 == 0) { n += 32; v <<= 32; }
            if (v >> 48 == 0) { n += 16; v <<= 16; }
            if (v >> 56 == 0) { n += 8; v <<= 8; }
            if (v >> 60 == 0) { n += 4; v <<= 4; }
            if (v >> 62 == 0) { n += 2; v <<= 2; }
            if (v >> 63 == 0) n++;
            return n;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;
        public bool Equals(Fix other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fix other && Raw == other.Raw;
        public override int GetHashCode() => Raw.GetHashCode();
        public int CompareTo(Fix other) => Raw.CompareTo(other.Raw);
        public override string ToString() => ((double)this).ToString(CultureInfo.InvariantCulture);
        public string ToString(string format) => ((double)this).ToString(format);
        public string ToString(string format, IFormatProvider provider) => ((double)this).ToString(format, provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Abs(Fix v) => v.Raw < 0 ? new Fix(-v.Raw) : v;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Min(Fix a, Fix b) => a.Raw <= b.Raw ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Max(Fix a, Fix b) => a.Raw >= b.Raw ? a : b;
        public static Fix Clamp(Fix v, Fix low, Fix high) => v.Raw < low.Raw ? low : v.Raw > high.Raw ? high : v;
        public static Fix Floor(Fix v) => new Fix(v.Raw & ~FractionMask);
        public static Fix Ceiling(Fix v) => new Fix((v.Raw + FractionMask) & ~FractionMask);
        // To the nearest whole number, halves to the even one, as Math.Round does.
        public static Fix Round(Fix v)
        {
            long floor = v.Raw & ~FractionMask, fraction = v.Raw & FractionMask;
            if (fraction > OneRaw / 2 || (fraction == OneRaw / 2 && ((floor >> FractionBits) & 1) != 0)) floor += OneRaw;
            return new Fix(floor);
        }
        // Rounded down: the largest r with r² ≤ raw · 2³². The hardware root of a double only guesses r; whole-number checks
        // of the 128-bit square then step it to the exact answer, so every processor ends on the same bits even if its
        // guess differs (the guess is within a step or two).
        public static Fix Sqrt(Fix v)
        {
            if (v.Raw <= 0) return Zero;
            ulong n = (ulong)v.Raw, high = n >> 32, low = n << 32;
            ulong root = (ulong)(Math.Sqrt(n) * 65536.0);
            while (root > 0 && Above(root, high, low)) root--;
            while (!Above(root + 1, high, low)) root++;
            return new Fix((long)root);
        }
        // Whether r² exceeds the 128-bit number high · 2⁶⁴ + low (r below 2⁵⁰, so r² fits in 128 bits).
        static bool Above(ulong r, ulong high, ulong low)
        {
            ulong a = r >> 32, b = r & 0xFFFFFFFF, cross = 2 * a * b;
            ulong squareLow = b * b, squareHigh = a * a + (cross >> 32);
            ulong shifted = cross << 32;
            squareLow += shifted; if (squareLow < shifted) squareHigh++;
            return squareHigh > high || (squareHigh == high && squareLow > low);
        }
        // Sine by its Taylor series on [0, π/2], where its error stays below 10⁻⁷, after folding the angle there.
        public static Fix Sin(Fix angle)
        {
            var a = angle % TwoPi;
            if (a < Zero) a += TwoPi;
            bool negative = false;
            if (a > Pi) { a -= Pi; negative = true; }
            if (a > HalfPi) a = Pi - a;
            Fix square = a * a, term = a, sum = a;
            for (int k = 1; k <= 6; k++) { term = -term * square / ((2 * k) * (2 * k + 1)); sum += term; }
            return negative ? -sum : sum;
        }
        public static Fix Cos(Fix angle) => Sin(angle + HalfPi);
    }
}
