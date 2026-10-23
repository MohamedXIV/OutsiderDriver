using System;
using System.Collections.Generic;
using System.Text;

namespace OutsiderDriver.Localization
{
    /// <summary>
    /// Utility for contextual Arabic glyph shaping and bidirectional (BiDi) reordering.
    /// Converts raw Arabic Unicode text into connected presentation forms (Unicode Range U+FE70-U+FEFF)
    /// and mirrors sentence flow so text displays properly in UI Toolkit elements without broken isolated letters.
    /// </summary>
    public static class ArabicRtlShaper
    {
        private struct ArabicGlyph
        {
            public char Raw;
            public char Isolated;
            public char Initial;
            public char Medial;
            public char Final;
            public bool ConnectsNext; // True if can connect to the following letter

            public ArabicGlyph(char raw, char isolated, char initial, char medial, char final, bool connectsNext)
            {
                Raw = raw;
                Isolated = isolated;
                Initial = initial;
                Medial = medial;
                Final = final;
                ConnectsNext = connectsNext;
            }
        }

        private static readonly Dictionary<char, ArabicGlyph> Glyphs = new Dictionary<char, ArabicGlyph>
        {
            // Hamza & Alif variants
            { '\u0621', new ArabicGlyph('\u0621', '\uFE80', '\uFE80', '\uFE80', '\uFE80', false) }, // ء
            { '\u0622', new ArabicGlyph('\u0622', '\uFE81', '\uFE81', '\uFE82', '\uFE82', false) }, // آ
            { '\u0623', new ArabicGlyph('\u0623', '\uFE83', '\uFE83', '\uFE84', '\uFE84', false) }, // أ
            { '\u0624', new ArabicGlyph('\u0624', '\uFE85', '\uFE85', '\uFE86', '\uFE86', false) }, // ؤ
            { '\u0625', new ArabicGlyph('\u0625', '\uFE87', '\uFE87', '\uFE88', '\uFE88', false) }, // إ
            { '\u0626', new ArabicGlyph('\u0626', '\uFE89', '\uFE8B', '\uFE8C', '\uFE8A', true) },  // ئ
            { '\u0627', new ArabicGlyph('\u0627', '\uFE8D', '\uFE8D', '\uFE8E', '\uFE8E', false) }, // ا

            // Consonants
            { '\u0628', new ArabicGlyph('\u0628', '\uFE8F', '\uFE91', '\uFE92', '\uFE90', true) },  // ب
            { '\u0629', new ArabicGlyph('\u0629', '\uFE93', '\uFE93', '\uFE94', '\uFE94', false) }, // ة
            { '\u062A', new ArabicGlyph('\u062A', '\uFE95', '\uFE97', '\uFE98', '\uFE96', true) },  // ت
            { '\u062B', new ArabicGlyph('\u062B', '\uFE99', '\uFE9B', '\uFE9C', '\uFE9A', true) },  // ث
            { '\u062C', new ArabicGlyph('\u062C', '\uFE9D', '\uFE9F', '\uFEA0', '\uFE9E', true) },  // ج
            { '\u062D', new ArabicGlyph('\u062D', '\uFEA1', '\uFEA3', '\uFEA4', '\uFEA2', true) },  // ح
            { '\u062E', new ArabicGlyph('\u062E', '\uFEA5', '\uFEA7', '\uFEA8', '\uFEA6', true) },  // خ
            { '\u062F', new ArabicGlyph('\u062F', '\uFEA9', '\uFEA9', '\uFEAA', '\uFEAA', false) }, // د
            { '\u0630', new ArabicGlyph('\u0630', '\uFEAB', '\uFEAB', '\uFEAC', '\uFEAC', false) }, // ذ
            { '\u0631', new ArabicGlyph('\u0631', '\uFEAD', '\uFEAD', '\uFEAE', '\uFEAE', false) }, // ر
            { '\u0632', new ArabicGlyph('\u0632', '\uFEAF', '\uFEAF', '\uFEB0', '\uFEB0', false) }, // ز
            { '\u0633', new ArabicGlyph('\u0633', '\uFEB1', '\uFEB3', '\uFEB4', '\uFEB2', true) },  // س
            { '\u0634', new ArabicGlyph('\u0634', '\uFEB5', '\uFEB7', '\uFEB8', '\uFEB6', true) },  // ش
            { '\u0635', new ArabicGlyph('\u0635', '\uFEB9', '\uFEBB', '\uFEBC', '\uFEBA', true) },  // ص
            { '\u0636', new ArabicGlyph('\u0636', '\uFEBD', '\uFEBF', '\uFEC0', '\uFEBE', true) },  // ض
            { '\u0637', new ArabicGlyph('\u0637', '\uFEC1', '\uFEC3', '\uFEC4', '\uFEC2', true) },  // ط
            { '\u0638', new ArabicGlyph('\u0638', '\uFEC5', '\uFEC7', '\uFEC8', '\uFEC6', true) },  // ظ
            { '\u0639', new ArabicGlyph('\u0639', '\uFEC9', '\uFECB', '\uFECC', '\uFECA', true) },  // ع
            { '\u063A', new ArabicGlyph('\u063A', '\uFECD', '\uFECF', '\uFED0', '\uFECE', true) },  // غ
            { '\u0641', new ArabicGlyph('\u0641', '\uFED1', '\uFED3', '\uFED4', '\uFED2', true) },  // ف
            { '\u0642', new ArabicGlyph('\u0642', '\uFED5', '\uFED7', '\uFED8', '\uFED6', true) },  // ق
            { '\u0643', new ArabicGlyph('\u0643', '\uFED9', '\uFEDB', '\uFEDC', '\uFEDA', true) },  // ك
            { '\u0644', new ArabicGlyph('\u0644', '\uFEDD', '\uFEDF', '\uFEE0', '\uFEDE', true) },  // ل
            { '\u0645', new ArabicGlyph('\u0645', '\uFEE1', '\uFEE3', '\uFEE4', '\uFEE2', true) },  // م
            { '\u0646', new ArabicGlyph('\u0646', '\uFEE5', '\uFEE7', '\uFEE8', '\uFEE6', true) },  // ن
            { '\u0647', new ArabicGlyph('\u0647', '\uFEE9', '\uFEEB', '\uFEEC', '\uFEEA', true) },  // ه
            { '\u0648', new ArabicGlyph('\u0648', '\uFEED', '\uFEED', '\uFEEE', '\uFEEE', false) }, // و
            { '\u0649', new ArabicGlyph('\u0649', '\uFEEF', '\uFEEF', '\uFEF0', '\uFEF0', false) }, // ى
            { '\u064A', new ArabicGlyph('\u064A', '\uFEF1', '\uFEF3', '\uFEF4', '\uFEF2', true) }   // ي
        };

        /// <summary>
        /// Determines whether a character is an Arabic letter.
        /// </summary>
        public static bool IsArabicChar(char c)
        {
            return (c >= '\u0600' && c <= '\u06FF') || (c >= '\uFE70' && c <= '\uFEFF');
        }

        /// <summary>
        /// Checks if a string contains any Arabic characters.
        /// </summary>
        public static bool ContainsArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsArabicChar(text[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// Shapes Arabic letters into their contextual presentation forms and reverses
        /// the text stream for visual RTL presentation in LTR UI frameworks.
        /// Leaves pure English / numeric text intact.
        /// </summary>
        public static string ShapeAndReorder(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            if (!ContainsArabic(input)) return input;

            // 1. Process line by line to preserve newlines
            string[] lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int l = 0; l < lines.Length; l++)
            {
                lines[l] = ShapeLine(lines[l]);
            }

            return string.Join("\n", lines);
        }

        private static string ShapeLine(string line)
        {
            if (string.IsNullOrEmpty(line) || !ContainsArabic(line))
            {
                return line;
            }

            // 1. Contextual glyph shaping (including Laam-Alif ligatures)
            string shaped = ShapeArabicCharacters(line);

            // 2. Tokenize into Arabic runs vs Non-Arabic runs (numbers, Latin, symbols)
            // and perform visual bidirectional reordering
            return ReorderBidirectional(shaped);
        }

        private static string ShapeArabicCharacters(string text)
        {
            var sb = new StringBuilder(text.Length);
            int len = text.Length;

            for (int i = 0; i < len; i++)
            {
                char c = text[i];

                if (!Glyphs.TryGetValue(c, out ArabicGlyph current))
                {
                    sb.Append(c);
                    continue;
                }

                // Check for Laam-Alif ligature: 'ل' followed by 'آ' or 'أ' or 'إ' or 'ا'
                if (c == '\u0644' && i + 1 < len)
                {
                    char nextC = text[i + 1];
                    char ligature = '\0';
                    bool prevConnects = i > 0 && ConnectsToNext(text[i - 1]);

                    switch (nextC)
                    {
                        case '\u0622': // Laam + Alif Madda
                            ligature = prevConnects ? '\uFEF6' : '\uFEF5';
                            break;
                        case '\u0623': // Laam + Alif Hamza Above
                            ligature = prevConnects ? '\uFEF8' : '\uFEF7';
                            break;
                        case '\u0625': // Laam + Alif Hamza Below
                            ligature = prevConnects ? '\uFEFA' : '\uFEF9';
                            break;
                        case '\u0627': // Laam + Alif Plain
                            ligature = prevConnects ? '\uFEFC' : '\uFEFB';
                            break;
                    }

                    if (ligature != '\0')
                    {
                        sb.Append(ligature);
                        i++; // Skip the combined Alif
                        continue;
                    }
                }

                bool connectedBefore = (i > 0) && ConnectsToNext(text[i - 1]);
                bool connectedAfter = (i + 1 < len) && ConnectsFromPrev(text[i + 1]);

                if (connectedBefore && connectedAfter)
                {
                    sb.Append(current.Medial);
                }
                else if (connectedBefore)
                {
                    sb.Append(current.Final);
                }
                else if (connectedAfter)
                {
                    sb.Append(current.Initial);
                }
                else
                {
                    sb.Append(current.Isolated);
                }
            }

            return sb.ToString();
        }

        private static bool ConnectsToNext(char c)
        {
            if (Glyphs.TryGetValue(c, out ArabicGlyph glyph))
            {
                return glyph.ConnectsNext;
            }
            return false;
        }

        private static bool ConnectsFromPrev(char c)
        {
            return Glyphs.ContainsKey(c);
        }

        private static string ReorderBidirectional(string text)
        {
            // Tokenize text into runs of (Arabic/Punctuation) vs (Latin/Numbers)
            var tokens = new List<string>();
            var currentToken = new StringBuilder();
            bool inArabic = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool isArabic = IsArabicChar(c);
                bool isDigit = char.IsDigit(c);
                bool isWhitespace = char.IsWhiteSpace(c);

                if (isArabic)
                {
                    if (!inArabic && currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    inArabic = true;
                    currentToken.Append(c);
                }
                else if (isDigit)
                {
                    if (inArabic && currentToken.Length > 0)
                    {
                        tokens.Add(currentToken.ToString());
                        currentToken.Clear();
                    }
                    inArabic = false;
                    currentToken.Append(c);
                }
                else if (isWhitespace)
                {
                    currentToken.Append(c);
                }
                else
                {
                    // Latin, punctuation, or symbol
                    bool isLatin = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                    if (isLatin)
                    {
                        if (inArabic && currentToken.Length > 0)
                        {
                            tokens.Add(currentToken.ToString());
                            currentToken.Clear();
                        }
                        inArabic = false;
                    }
                    currentToken.Append(c);
                }
            }

            if (currentToken.Length > 0)
            {
                tokens.Add(currentToken.ToString());
            }

            // Reverse the order of tokens, and for Arabic tokens reverse their characters
            var result = new StringBuilder(text.Length);
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                string token = tokens[i];
                if (ContainsArabic(token))
                {
                    result.Append(ReverseString(token));
                }
                else
                {
                    result.Append(token);
                }
            }

            return MirrorBrackets(result.ToString());
        }

        private static string ReverseString(string s)
        {
            char[] chars = s.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        private static string MirrorBrackets(string text)
        {
            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                switch (chars[i])
                {
                    case '(': chars[i] = ')'; break;
                    case ')': chars[i] = '('; break;
                    case '[': chars[i] = ']'; break;
                    case ']': chars[i] = '['; break;
                    case '{': chars[i] = '}'; break;
                    case '}': chars[i] = '{'; break;
                    case '<': chars[i] = '>'; break;
                    case '>': chars[i] = '<'; break;
                    case '«': chars[i] = '»'; break;
                    case '»': chars[i] = '«'; break;
                    case '؟': chars[i] = '?'; break;
                }
            }
            return new string(chars);
        }
    }
}
