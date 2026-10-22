using System;
using System.Text.RegularExpressions;

namespace Order.Content
{
    // Map coordinates from COMMANDS.md: letters are columns left to right, numbers are rows top to bottom
    // (top = far side, high Z), each square split 3×3 with sub-squares numbered row by row from the top left.
    public static class MapGrid
    {
        public const float SquareSize = 8;
        private const string Letters = "ABCDEFGH";
        private static readonly Regex Pattern = new Regex(@"(?<![a-z])([a-h])\s*-?\s*([1-8])(?:\s*-?\s*([1-9]))?(?![\d])", RegexOptions.CultureInvariant);

        public static int Squares(int mapSize) => Math.Max(1, (int)(mapSize / SquareSize));

        public static string Name(float x, float z, int mapSize, bool sub = true)
        {
            var squares = Squares(mapSize);
            var column = Math.Max(0, Math.Min(squares - 1, (int)(x / SquareSize)));
            var row = Math.Max(0, Math.Min(squares - 1, (int)((mapSize - z) / SquareSize)));
            var name = Letters[Math.Min(column, Letters.Length - 1)] + "-" + (row + 1);
            if (!sub) return name;
            var localX = x - column * SquareSize; var fromTop = mapSize - z - row * SquareSize;
            var subColumn = Math.Max(0, Math.Min(2, (int)(localX / (SquareSize / 3))));
            var subRow = Math.Max(0, Math.Min(2, (int)(fromTop / (SquareSize / 3))));
            return name + "-" + (subRow * 3 + subColumn + 1);
        }

        // Finds the first coordinate in lowercase text ("d-2-9", "d2", "в d 2 9") and returns the centre of that square.
        public static bool TryFind(string text, int mapSize, out float x, out float z, out string name)
        {
            x = z = 0; name = null;
            var match = Pattern.Match(text ?? "");
            if (!match.Success) return false;
            var column = match.Groups[1].Value[0] - 'a';
            var row = match.Groups[2].Value[0] - '1';
            if (column >= Squares(mapSize) || row >= Squares(mapSize)) return false;
            x = column * SquareSize + SquareSize / 2;
            var top = mapSize - row * SquareSize;
            z = top - SquareSize / 2;
            name = char.ToUpperInvariant(match.Groups[1].Value[0]) + "-" + (row + 1);
            if (!match.Groups[3].Success) return true;
            var sub = match.Groups[3].Value[0] - '1';
            x = column * SquareSize + (sub % 3 + .5f) * SquareSize / 3;
            z = top - (sub / 3 + .5f) * SquareSize / 3;
            name += "-" + (sub + 1);
            return true;
        }

        public static string Letter(int column) => column >= 0 && column < Letters.Length ? Letters[column].ToString() : "?";
    }
}
