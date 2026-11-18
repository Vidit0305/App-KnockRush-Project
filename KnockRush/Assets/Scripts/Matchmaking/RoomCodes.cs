using System;
using System.Text;

namespace KnockRush.Matchmaking
{
    /// <summary>
    /// Private-room codes: 6 characters from an alphabet without look-alikes (no 0/O, 1/I/L),
    /// easy to read aloud and type on a phone. 31^6 ≈ 887 million combinations.
    /// </summary>
    public static class RoomCodes
    {
        public const int Length = 6;
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        public const string SessionPrefix = "PR-";

        public static string Generate(Random random)
        {
            var builder = new StringBuilder(Length);
            for (var i = 0; i < Length; i++)
            {
                builder.Append(Alphabet[random.Next(Alphabet.Length)]);
            }

            return builder.ToString();
        }

        /// <summary>Uppercases and removes spaces/dashes ("ab3-k7m" → "AB3K7M").</summary>
        public static string Normalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(input.Length);
            foreach (var c in input.Trim().ToUpperInvariant())
            {
                if (c != ' ' && c != '-')
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != Length)
            {
                return false;
            }

            foreach (var c in code)
            {
                if (Alphabet.IndexOf(c) < 0)
                {
                    return false;
                }
            }

            return true;
        }

        public static string ToSessionName(string code) => SessionPrefix + code;
    }
}
