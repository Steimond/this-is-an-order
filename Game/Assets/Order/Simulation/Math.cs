using System.Runtime.CompilerServices;
using Order.Content;

namespace Order.Simulation
{
    // Inside the simulation `Math` means this class, not System.Math: integers and Fix only, so nothing in the rules can
    // reach for floating-point functions. A test keeps float out of the simulation's sources (DeterminismTests).
    static class Math
    {
        public static readonly Fix PI = Fix.Pi;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Min(int a, int b) => a <= b ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Max(int a, int b) => a >= b ? a : b;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static int Abs(int v) => v < 0 ? -v : v;
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Min(Fix a, Fix b) => Fix.Min(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Max(Fix a, Fix b) => Fix.Max(a, b);
        [MethodImpl(MethodImplOptions.AggressiveInlining)] public static Fix Abs(Fix v) => Fix.Abs(v);
        public static Fix Sqrt(Fix v) => Fix.Sqrt(v);
        public static Fix Sin(Fix v) => Fix.Sin(v);
        public static Fix Cos(Fix v) => Fix.Cos(v);
        public static Fix Round(Fix v) => Fix.Round(v);
        public static Fix Ceiling(Fix v) => Fix.Ceiling(v);
        public static Fix Floor(Fix v) => Fix.Floor(v);
    }
}
