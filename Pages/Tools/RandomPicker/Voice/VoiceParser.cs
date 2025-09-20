using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DragonTools.Pages.Tools.RandomPicker.Voice;

public static class VoiceParser
{
    // Number parsing dictionaries
    private static readonly Dictionary<string, int> Units = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5,
        ["six"] = 6, ["seven"] = 7, ["eight"] = 8, ["nine"] = 9, ["ten"] = 10,
        ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14, ["fifteen"] = 15,
        ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19
    };
    private static readonly Dictionary<string, int> Tens = new(StringComparer.OrdinalIgnoreCase)
    {
        ["twenty"] = 20, ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50,
        ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90
    };
    private static readonly Dictionary<string, int> MultiplierWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["single"] = 1,
        ["once"] = 1,
        ["double"] = 2,
        ["twice"] = 2,
        ["couple"] = 2,
        ["triple"] = 3,
        ["thrice"] = 3,
        ["quadruple"] = 4,
        ["quintuple"] = 5,
        ["sextuple"] = 6,
        ["septuple"] = 7,
        ["octuple"] = 8,
        ["nonuple"] = 9,
        ["decuple"] = 10
    };

    // Allow many natural variants
    private const string WeightPhrase = "(?:with(?:\\s+(?:a|the))?\\s*weight(?:\\s+(?:of|is))?|weight(?:s)?(?:\\s+(?:of|is))?|weigh(?:s|ed|ing)?|weighted(?:\\s+at)?|times|x|×|✕|\\*|by\\s+(?:a\\s+)?factor\\s+of)";

    public static VoiceParseResult ParseSingle(string text)
    {
        var result = new VoiceParseResult();
        if (string.IsNullOrWhiteSpace(text)) return result;
        var input = text.Trim();

        // 0) Parentheses/Braces/Brackets multiplier forms: item (x3), item (3x), (x3) item, (three) item, {x3}, [x3]
        var m = Regex.Match(input, @"^(.+?)\\s*[\u0028\uFF08\[\{]\\s*(?:[x×✕*]\\s*)?(.+?)\\s*[\u0029\uFF09\]\}]\\s*$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var entry = TrimQuotes(m.Groups[1].Value.Trim());
            var tok = m.Groups[2].Value.Trim();
            var n0 = ParseNumberLikeDouble(tok);
            if (n0 >= 1)
            {
                result.Entry = entry;
                result.Weight = (int)Math.Max(1, Math.Round(n0));
                return result;
            }
        }
        m = Regex.Match(input, @"^[\u0028\uFF08\[\{]\\s*(?:[x×✕*]\\s*)?(.+?)\\s*[\u0029\uFF09\]\}]\\s+(.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var tok = m.Groups[1].Value.Trim();
            var n0 = ParseNumberLikeDouble(tok);
            if (n0 >= 1)
            {
                result.Entry = TrimQuotes(m.Groups[2].Value.Trim());
                result.Weight = (int)Math.Max(1, Math.Round(n0));
                return result;
            }
        }

        // 1) Bracket pattern: item [3]
        m = Regex.Match(input, @"^(.+?)\\s*\\[\\s*(\\d+)\\s*\\]\\s*$");
        if (m.Success)
        {
            result.Entry = TrimQuotes(m.Groups[1].Value.Trim());
            result.Weight = Math.Max(1, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture));
            return result;
        }

        // 2) Suffix weight phrase: "item with the weight of 10", "item times three", "item x3", "item weighted at twenty five"
        m = Regex.Match(input, $@"^(.+?)\\s*{WeightPhrase}\\s+(.+?)\\s*$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var entry = TrimQuotes(m.Groups[1].Value.Trim());
            var token = m.Groups[2].Value.Trim();
            var n = ParseNumberToken(token);
            if (n >= 1)
            {
                result.Entry = entry;
                result.Weight = n;
                return result;
            }
        }

        // 3) Prefix weight phrase: "weight 10 item", "with the weight of 10 item", "times three item", "x3 item"
        m = Regex.Match(input, $@"^{WeightPhrase}\\s+(.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var rest = m.Groups[1].Value.Trim();
            if (TrySplitLeadingNumber(rest, out var numPhrase, out var itemPhrase))
            {
                var n = ParseNumberToken(numPhrase);
                if (n >= 1)
                {
                    result.Entry = TrimQuotes(itemPhrase.Trim());
                    result.Weight = n;
                    return result;
                }
            }
        }

        // 4) Multiplier prefix forms: "3x item", "three times item", "2× item", "2* item"
        m = Regex.Match(input, @"^(?<num>.+?)\\s*(?:[x×✕*]|times)\\s+(?<item>.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var token = m.Groups["num"].Value.Trim();
            var n = ParseNumberToken(token);
            if (n >= 1)
            {
                result.Entry = TrimQuotes(m.Groups["item"].Value.Trim());
                result.Weight = n;
                return result;
            }
        }

        // 5) Multiplier suffix forms: "item x 3", "item times twenty five", "item × 2"
        m = Regex.Match(input, @"^(.+?)\\s*(?:[x×✕*]|times)\\s+(.+?)\\s*$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var token = m.Groups[2].Value.Trim();
            var n = ParseNumberToken(token);
            if (n >= 1)
            {
                result.Entry = TrimQuotes(m.Groups[1].Value.Trim());
                result.Weight = n;
                return result;
            }
        }

        // 6) Percentage: suffix "item 25 percent" / "item 25%" / "item twenty five percent" / "item 25 pct"
        m = Regex.Match(input, @"^(.+?)\\s+(.+?)\\s*(?:percent|percentage|%|pct|pc)\\s*$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var entry = TrimQuotes(m.Groups[1].Value.Trim());
            var token = m.Groups[2].Value.Trim();
            if (TryParseDoubleToken(token, out var pct))
            {
                result.Entry = entry;
                result.Percentage = pct;
                return result;
            }
        }
        // Prefix: "25 percent item" / "twenty five percent item" / "25% item" / "25 pct item"
        m = Regex.Match(input, @"^(.+?)\\s*(?:percent|percentage|%|pct|pc)\\s+(.+)$", RegexOptions.IgnoreCase);
        if (m.Success)
        {
            var token = m.Groups[1].Value.Trim();
            if (TryParseDoubleToken(token, out var pct))
            {
                result.Entry = TrimQuotes(m.Groups[2].Value.Trim());
                result.Percentage = pct;
                return result;
            }
        }

        // Default: no explicit attributes, use raw input
        result.Entry = TrimQuotes(input.Trim());
        return result;
    }

    public static IEnumerable<VoiceParseResult> ParseBulk(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        var normalized = text.Trim();

        // Normalize conjunction-only inputs cautiously to comma-separated if there are no clear separators
        var hasSeparators = normalized.Contains(',') || normalized.Contains(';') || normalized.Contains('\n');
        if (!hasSeparators)
        {
            var lower = normalized.ToLowerInvariant();
            var looksLikeNumberPhrase = lower.Contains(" and a ") || lower.Contains(" and one ") || lower.Contains(" and two ") ||
                                        lower.Contains(" and three ") || lower.Contains(" and four ") || lower.Contains(" and five ") ||
                                        lower.Contains(" and six ") || lower.Contains(" and seven ") || lower.Contains(" and eight ") ||
                                        lower.Contains(" and nine ") || lower.Contains(" and ten ") || lower.Contains(" point ") ||
                                        lower.Contains(" half") || lower.Contains(" quarter");
            if (!looksLikeNumberPhrase && lower.Contains(" and "))
                normalized = Regex.Replace(normalized, "(?i)\\sand\\s", ", ");
            else if (normalized.Contains(" & ", StringComparison.Ordinal))
                normalized = normalized.Replace(" & ", ", ", StringComparison.Ordinal);
        }

        foreach (var part in Regex.Split(normalized, @"(?:,|;|\r?\n)+"))
        {
            var token = part.Trim();
            if (string.IsNullOrWhiteSpace(token)) continue;
            yield return ParseSingle(token);
        }
    }

    private static int ParseNumberToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return -1;
        var cleaned = token.Trim().ToLowerInvariant();

        // Handle numeric with x/× prefix/suffix
        if (cleaned.StartsWith('x') && int.TryParse(cleaned.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var xn)) return xn;
        if (cleaned.StartsWith('×') && int.TryParse(cleaned.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var xmult)) return xmult;
        if (cleaned.EndsWith('x') && int.TryParse(cleaned.TrimEnd('x'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nx)) return nx;
        if (cleaned.EndsWith('×') && int.TryParse(cleaned.TrimEnd('×'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nx2)) return nx2;
        if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return n;
        if (MultiplierWords.TryGetValue(cleaned, out var mult)) return mult;

        // Try Microsoft Recognizers (if available at runtime) for natural phrases
        if (TryRecognizersDouble(token, out var rd))
        {
            if (rd >= 0)
            {
                var rounded = (int)Math.Round(rd);
                if (rounded >= 0) return rounded;
            }
        }

        cleaned = cleaned.Replace('-', ' ');
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        long total = 0;
        long current = 0;
        foreach (var p0 in parts)
        {
            var p = p0.Trim();
            if (p is "and" or "a" or "an") continue;
            if (Units.TryGetValue(p, out var u)) { current += u; continue; }
            if (Tens.TryGetValue(p, out var t)) { current += t; continue; }
            if (p == "fourty") { current += 40; continue; }
            if (p == "hundred") { if (current == 0) current = 1; current *= 100; continue; }
            if (p == "thousand") { if (current == 0) current = 1; total += current * 1000; current = 0; continue; }
            if (p == "million") { if (current == 0) current = 1; total += current * 1_000_000; current = 0; continue; }
            if (p == "dozen") { if (current == 0) current = 1; current *= 12; continue; }
            if (p == "score") { if (current == 0) current = 1; current *= 20; continue; }
            return -1;
        }
        var value = total + current;
        return value <= int.MaxValue ? (int)value : -1;
    }

    private static bool TryParseDoubleToken(string token, out double value)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
            if (TryRecognizersDouble(token, out value)) return true;
            if (TryParseDoubleWords(token, out value)) return true;
        }
        value = 0;
        return false;
    }

    private static bool TryParseDoubleWords(string token, out double value)
    {
        value = 0;
        var txt = token.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(txt)) return false;
        txt = txt.Replace('-', ' ');
        txt = Regex.Replace(txt, "\\s+", " ");

        // Fractions
        if (txt is "half" or "a half") { value = 0.5; return true; }
        if (txt is "quarter" or "a quarter") { value = 0.25; return true; }
        if (txt is "three quarters" or "three quarter") { value = 0.75; return true; }

        // X and a half / quarter / three quarters
        var m = Regex.Match(txt, @"^(.+?)\\s+and\\s+a\\s+(half|quarter)$");
        if (m.Success)
        {
            var whole = ParseNumberToken(m.Groups[1].Value);
            if (whole >= 0)
            {
                value = whole + (m.Groups[2].Value == "half" ? 0.5 : 0.25);
                return true;
            }
        }
        m = Regex.Match(txt, @"^(.+?)\\s+and\\s+(three\\s+quarters)$");
        if (m.Success)
        {
            var whole = ParseNumberToken(m.Groups[1].Value);
            if (whole >= 0)
            {
                value = whole + 0.75;
                return true;
            }
        }

        // Decimal point words: "twenty five point five" / "25 point five six"
        var pointIdx = txt.IndexOf(" point ", StringComparison.Ordinal);
        if (pointIdx >= 0)
        {
            var left = txt[..pointIdx].Trim();
            var right = txt[(pointIdx + 7)..].Trim();
            var leftNum = ParseNumberToken(left);
            if (leftNum >= 0)
            {
                var rightDigits = TryMapWordsToDigits(right);
                if (rightDigits != null)
                {
                    if (double.TryParse($"{leftNum}.{rightDigits}", NumberStyles.Float, CultureInfo.InvariantCulture, out var dv))
                    { value = dv; return true; }
                }
                var rightInt = ParseNumberToken(right);
                if (rightInt >= 0)
                {
                    var digits = rightInt == 0 ? 1 : (int)Math.Floor(Math.Log10(rightInt) + 1);
                    var scaled = rightInt / Math.Pow(10, digits);
                    value = leftNum + scaled;
                    return true;
                }
            }
        }

        // Otherwise parse as integer words
        var n = ParseNumberToken(txt);
        if (n >= 0) { value = n; return true; }
        return false;
    }

    private static string? TryMapWordsToDigits(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;
        var digits = new List<char>(parts.Length);
        foreach (var p in parts)
        {
            if (Units.TryGetValue(p, out var u) && u is >= 0 and <= 9)
            {
                digits.Add((char)('0' + u));
                continue;
            }
            if (int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) && num is >= 0 and <= 9)
            {
                digits.Add((char)('0' + num));
                continue;
            }
            return null;
        }
        return new string(digits.ToArray());
    }

    private static double ParseNumberLikeDouble(string token)
    {
        if (TryParseDoubleToken(token, out var d)) return d;
        return -1;
    }

    private static bool TrySplitLeadingNumber(string rest, out string numberPhrase, out string itemPhrase)
    {
        numberPhrase = string.Empty;
        itemPhrase = rest;
        if (string.IsNullOrWhiteSpace(rest)) return false;

        var words = Regex.Split(rest.Trim(), "\\s+");
        var bestLen = 0;
        for (var len = 1; len <= words.Length; len++)
        {
            var candidate = string.Join(' ', words[..len]);
            var n = ParseNumberToken(candidate);
            if (n >= 0) bestLen = len; else break;
        }
        if (bestLen > 0)
        {
            numberPhrase = string.Join(' ', words[..bestLen]);
            itemPhrase = string.Join(' ', words[bestLen..]);
            return !string.IsNullOrWhiteSpace(itemPhrase);
        }
        return false;
    }

    // Optional: use Microsoft.Recognizers.Text.Number at runtime via reflection if present
    private static bool TryRecognizersDouble(string token, out double value)
    {
        value = 0;
        try
        {
            var cultureType = Type.GetType("Microsoft.Recognizers.Text.Culture, Microsoft.Recognizers.Text");
            var english = cultureType?.GetField("English", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string ?? "en-us";

            var nrType = Type.GetType("Microsoft.Recognizers.Text.Number.NumberRecognizer, Microsoft.Recognizers.Text.Number");
            var method = nrType?.GetMethod("RecognizeNumber", new[] { typeof(string), typeof(string) });
            if (method == null) return false;
            var resultsObj = method.Invoke(null, new object[] { token, english });
            if (resultsObj is IEnumerable enumerable)
            {
                foreach (var r in enumerable)
                {
                    var resProp = r.GetType().GetProperty("Resolution", BindingFlags.Public | BindingFlags.Instance);
                    var dictObj = resProp?.GetValue(r) as IDictionary;
                    if (dictObj != null && dictObj.Contains("value"))
                    {
                        var s = dictObj["value"] as string;
                        if (!string.IsNullOrEmpty(s) && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        { value = d; return true; }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }
        return false;
    }

    private static string TrimQuotes(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var left = s[0];
        var right = s[^1];
        if ((left == '"' && right == '"') || (left == '\'' && right == '\'') ||
            (left == '“' && right == '”') || (left == '‘' && right == '’'))
        {
            return s.Length > 2 ? s[1..^1].Trim() : string.Empty;
        }
        return s;
    }
}
