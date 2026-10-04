// Minimal dependency-free JSON reader shared by level loading, tools and tests.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TankerJam.Core
{
    /// <summary>
    /// Minimal JSON reader so Core needs no packages. Objects become Dictionary&lt;string, object&gt;,
    /// arrays List&lt;object&gt;, numbers double. Load-time use only (allocates).
    /// </summary>
    public sealed class MiniJson
    {
        readonly string s;
        int i;

        MiniJson(string text) { s = text; }

        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) throw new FormatException("JSON text is empty.");
            return new MiniJson(json).ParseValue();
        }

        void Ws() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        bool Match(string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        object ParseValue()
        {
            Ws();
            char ch = s[i];
            if (ch == '{') return ParseObject();
            if (ch == '[') return ParseArray();
            if (ch == '"') return ParseString();
            if (Match("true")) return true;
            if (Match("false")) return false;
            if (Match("null")) return null;
            return ParseNumber();
        }

        Dictionary<string, object> ParseObject()
        {
            var d = new Dictionary<string, object>();
            i++; Ws();
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                Ws(); string key = ParseString(); Ws(); i++; // ':'
                d[key] = ParseValue(); Ws();
                if (s[i] == ',') { i++; continue; }
                i++; return d; // '}'
            }
        }

        List<object> ParseArray()
        {
            var l = new List<object>();
            i++; Ws();
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ParseValue()); Ws();
                if (s[i] == ',') { i++; continue; }
                i++; return l; // ']'
            }
        }

        string ParseString()
        {
            var sb = new StringBuilder();
            i++;
            while (s[i] != '"')
            {
                if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                else sb.Append(s[i]);
                i++;
            }
            i++;
            return sb.ToString();
        }

        double ParseNumber()
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == start) throw new FormatException($"Unexpected character '{s[i]}' at {i}.");
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }
    }
}
