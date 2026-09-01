using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace IntersectUtilities.MPE.MatchBBR
{
    // Address normalization and key generation.
    //
    // The original code normalized only on the Excel side and only in one word order, which is
    // why MATCHBBR and COMPAREBBR could disagree about the same address: MATCHBBR registered both
    // "Hovedgaden 12" and "12 Hovedgaden" as lookup keys, while COMPAREBBR built only the first.
    // Here both orders are generated for *both* sides, so the two directions cannot diverge.
    //
    // Danish characters are deliberately preserved: æ/ø/å are not folded to ae/oe/aa, because
    // folding them would merge genuinely distinct street names.
    internal static class MatchBbrAddressKey
    {
        // A house number is digits with an optional single letter suffix: 12, 12A.
        private static readonly Regex HouseNumberPattern =
            new Regex(@"^\d+[A-ZÆØÅ]?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // "12-A" and "12 A" both mean 12A.
        private static readonly Regex HyphenatedHouseNumberPattern =
            new Regex(@"^(\d+)-([A-ZÆØÅ])$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex DigitsOnlyPattern =
            new Regex(@"^\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex SingleLetterPattern =
            new Regex(@"^[A-ZÆØÅ]$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // The baseline normalization the original code used: trim, split on spaces and commas,
        // collapse runs of whitespace, uppercase. Kept identical so existing drawings behave the
        // same, with the house-number handling layered on top in BuildKeys.
        public static string Normalize(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return string.Empty;
            }

            return string.Join(" ", Tokenize(address!));
        }

        // Every key an address should be findable under: the canonical "street number" form and,
        // when the number can be identified, the reversed "number street" form.
        public static IReadOnlyList<string> BuildKeys(string? address)
        {
            List<string> tokens = Tokenize(address).ToList();
            if (tokens.Count == 0)
            {
                return Array.Empty<string>();
            }

            if (tokens.Count == 1)
            {
                return new[] { tokens[0] };
            }

            // Decide which end carries the house number. Checking only the ends keeps this
            // predictable: an address with a number in the middle is left as a single key
            // rather than guessed at.
            string number;
            List<string> street;
            if (HouseNumberPattern.IsMatch(tokens[tokens.Count - 1]))
            {
                number = tokens[tokens.Count - 1];
                street = tokens.Take(tokens.Count - 1).ToList();
            }
            else if (HouseNumberPattern.IsMatch(tokens[0]))
            {
                number = tokens[0];
                street = tokens.Skip(1).ToList();
            }
            else
            {
                return new[] { string.Join(" ", tokens) };
            }

            if (street.Count == 0)
            {
                return new[] { number };
            }

            string streetPart = string.Join(" ", street);
            string canonical = $"{streetPart} {number}";
            string reversed = $"{number} {streetPart}";

            return string.Equals(canonical, reversed, StringComparison.Ordinal)
                ? new[] { canonical }
                : new[] { canonical, reversed };
        }

        // The key used for display and for grouping. Always the canonical "street number" form
        // when one can be identified, so two rows that matched each other read the same.
        public static string PrimaryKey(string? address)
        {
            IReadOnlyList<string> keys = BuildKeys(address);
            return keys.Count == 0 ? string.Empty : keys[0];
        }

        // Joins a street name and a house number the same way a single-column address would read,
        // so a split Excel source and a combined one produce identical keys.
        public static string Join(string? streetName, string? houseNumber)
        {
            string a = (streetName ?? string.Empty).Trim();
            string b = (houseNumber ?? string.Empty).Trim();
            if (a.Length == 0)
            {
                return b;
            }

            return b.Length == 0 ? a : $"{a} {b}";
        }

        private static IEnumerable<string> Tokenize(string? address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                yield break;
            }

            string[] rawTokens = address!
                .Trim()
                .ToUpperInvariant()
                .Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < rawTokens.Length; i++)
            {
                string token = rawTokens[i];

                Match hyphenated = HyphenatedHouseNumberPattern.Match(token);
                if (hyphenated.Success)
                {
                    yield return hyphenated.Groups[1].Value + hyphenated.Groups[2].Value;
                    continue;
                }

                // "12 A" arrives as two tokens; fold the letter back onto the number so it
                // matches a drawing that spells the same address "12A".
                if (DigitsOnlyPattern.IsMatch(token)
                    && i + 1 < rawTokens.Length
                    && SingleLetterPattern.IsMatch(rawTokens[i + 1]))
                {
                    yield return token + rawTokens[i + 1];
                    i++;
                    continue;
                }

                yield return token;
            }
        }
    }
}
