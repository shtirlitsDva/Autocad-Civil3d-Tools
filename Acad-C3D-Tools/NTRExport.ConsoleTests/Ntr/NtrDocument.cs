using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NTRExport.ConsoleTests.Ntr
{
    internal sealed class NtrDocument
    {
        private NtrDocument(string sourcePath, IReadOnlyList<NtrRecord> records)
        {
            SourcePath = sourcePath;
            Records = records;
        }

        public string SourcePath { get; }
        public IReadOnlyList<NtrRecord> Records { get; }

        public static NtrDocument? Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            var lines = File.ReadAllLines(path);
            var records = new List<NtrRecord>();

            for (int i = 0; i < lines.Length; i++)
            {
                var original = lines[i];
                var trimmed = original.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (IsComment(trimmed))
                {
                    records.Add(NtrRecord.FromComment(original));
                    continue;
                }

                var tokens = NtrTokenizer.Tokenize(trimmed);
                if (tokens.Count == 0) continue;

                records.Add(NtrRecord.FromTokens(original, tokens));
            }

            return new NtrDocument(path, records);
        }

        private static bool IsComment(string trimmed)
        {
            return trimmed.Length > 0
                && trimmed[0] == 'C'
                && (trimmed.Length == 1 || char.IsWhiteSpace(trimmed[1]));
        }
    }

    internal sealed class NtrRecord
    {
        private NtrRecord(string code, string originalLine, string? comment, IReadOnlyList<string> flags, IReadOnlyList<KeyValuePair<string, string>> fields)
        {
            Code = code;
            OriginalLine = originalLine;
            Comment = comment;
            Flags = flags;
            Fields = fields;
        }

        public string Code { get; }
        public string OriginalLine { get; }
        public string? Comment { get; }
        public IReadOnlyList<string> Flags { get; }
        public IReadOnlyList<KeyValuePair<string, string>> Fields { get; }

        public string CanonicalKey
        {
            get
            {
                if (Comment is not null)
                {
                    return $"C|{Comment}";
                }

                var sb = new StringBuilder();
                sb.Append(Code);

                foreach (var flag in Flags.OrderBy(f => f, StringComparer.Ordinal))
                {
                    sb.Append("|FLAG=").Append(flag);
                }

                foreach (var kv in Fields
                             .OrderBy(k => k.Key, StringComparer.Ordinal)
                             .ThenBy(k => k.Value, StringComparer.Ordinal))
                {
                    sb.Append('|').Append(kv.Key).Append('=').Append(kv.Value);
                }

                return sb.ToString();
            }
        }

        public static NtrRecord FromComment(string originalLine)
        {
            var trimmed = originalLine.Trim();
            var content = trimmed.Length > 1 ? trimmed.Substring(1).TrimStart() : string.Empty;
            return new NtrRecord("C", originalLine, content, Array.Empty<string>(), Array.Empty<KeyValuePair<string, string>>());
        }

        public static NtrRecord FromTokens(string originalLine, IReadOnlyList<string> tokens)
        {
            if (tokens.Count == 0) throw new ArgumentException("Token list must not be empty.", nameof(tokens));

            var code = tokens[0];
            var flags = new List<string>();
            var fields = new List<KeyValuePair<string, string>>();

            for (int i = 1; i < tokens.Count; i++)
            {
                var token = tokens[i];
                var separator = token.IndexOf('=');
                if (separator > 0)
                {
                    var key = token.Substring(0, separator);
                    var value = token.Substring(separator + 1);
                    fields.Add(new KeyValuePair<string, string>(key, value));
                }
                else
                {
                    flags.Add(token);
                }
            }

            return new NtrRecord(code, originalLine, null, flags, fields);
        }
    }

    internal static class NtrDocumentComparer
    {
        public static bool AreEquivalent(NtrDocument expected, NtrDocument actual, out string message)
        {
            if (expected is null) throw new ArgumentNullException(nameof(expected));
            if (actual is null) throw new ArgumentNullException(nameof(actual));

            var expectedBag = ToMultiset(expected);
            var actualBag = ToMultiset(actual);

            var missing = new List<string>();
            foreach (var kv in expectedBag)
            {
                actualBag.TryGetValue(kv.Key, out var actualCount);
                if (actualCount < kv.Value)
                {
                    missing.Add($"{kv.Key} (expected {kv.Value}, actual {actualCount})");
                }
            }

            var extra = new List<string>();
            foreach (var kv in actualBag)
            {
                expectedBag.TryGetValue(kv.Key, out var expectedCount);
                if (expectedCount < kv.Value)
                {
                    extra.Add($"{kv.Key} (actual {kv.Value}, expected {expectedCount})");
                }
            }

            if (missing.Count == 0 && extra.Count == 0)
            {
                message = string.Empty;
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine("ASSERT FAIL: NTR content differs.");
            if (missing.Count > 0)
            {
                sb.AppendLine("Missing records:");
                foreach (var item in missing)
                {
                    sb.Append("  ").AppendLine(item);
                }
            }

            if (extra.Count > 0)
            {
                sb.AppendLine("Extra records:");
                foreach (var item in extra)
                {
                    sb.Append("  ").AppendLine(item);
                }
            }

            message = sb.ToString();
            return false;
        }

        private static Dictionary<string, int> ToMultiset(NtrDocument document)
        {
            var bag = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var record in document.Records)
            {
                var key = record.CanonicalKey;
                if (bag.TryGetValue(key, out var count))
                {
                    bag[key] = count + 1;
                }
                else
                {
                    bag[key] = 1;
                }
            }

            return bag;
        }
    }

    internal static class NtrTokenizer
    {
        public static IReadOnlyList<string> Tokenize(string line)
        {
            if (line is null) throw new ArgumentNullException(nameof(line));

            var tokens = new List<string>();
            var sb = new StringBuilder();
            bool inQuote = false;
            char quoteChar = '\0';

            for (int i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (inQuote)
                {
                    sb.Append(ch);
                    if (ch == quoteChar)
                    {
                        inQuote = false;
                    }
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }

                if (ch == '\'' || ch == '"')
                {
                    inQuote = true;
                    quoteChar = ch;
                    sb.Append(ch);
                    continue;
                }

                sb.Append(ch);
            }

            if (sb.Length > 0)
            {
                tokens.Add(sb.ToString());
            }

            return tokens;
        }
    }
}

