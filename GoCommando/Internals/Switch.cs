// ReSharper disable LoopCanBeConvertedToQuery

using System.Collections.Generic;
using System.Linq;

namespace GoCommando.Internals
{
    internal class Switch
    {
        private static readonly char[] AcceptedQuoteCharacters = { '"', '\'' };

        internal Switch(string key, string value)
        {
            Name = key;

            IsMultiValue = false;
            IsFlag = string.IsNullOrEmpty(value);

            Values = IsFlag ? new string[] { } : new[] { Unquote(value) };

        }

        internal Switch(string key, IEnumerable<string> values)
        {
            Name = key;

            var unquotedValues = values.Select(Unquote).ToArray();
            IsFlag = unquotedValues.Length == 0;

            if (IsFlag)
                return;
            IsMultiValue = unquotedValues.Length > 1;
            Values = unquotedValues;
        }

        public bool IsFlag { get; }

        public string Name { get; }
        public IEnumerable<string> Values { get; } = new string[] { };
        public bool IsMultiValue { get; }

        private static string Unquote(string value)
        {
            if (value == null) return null;

            // can't be quoted
            if (value.Length < 2) return value;

            foreach (var quoteChar in AcceptedQuoteCharacters)
            {
                var quote = quoteChar.ToString();

                if (value.StartsWith(quote)
                    && value.EndsWith(quote))
                    return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}