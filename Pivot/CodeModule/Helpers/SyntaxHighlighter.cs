using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Pivot.CodeModule.Helpers
{
    public enum TokenType
    {
        Keyword,
        String,
        Comment,
        Number,
        Text
    }

    public class Token
    {
        public TokenType Type { get; set; }
        public int Start { get; set; }
        public int Length { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public static class SyntaxHighlighter
    {
        public static List<Token> Tokenize(string text, string language)
        {
            return language?.ToLowerInvariant() switch
            {
                "csharp" => TokenizeCSharp(text),
                "python" => TokenizePython(text),
                "javascript" => TokenizeJavaScript(text),
                "typescript" => TokenizeJavaScript(text),
                "cpp" or "c" => TokenizeCpp(text),
                "sql" => TokenizeSql(text),
                "json" => TokenizeJson(text),
                "xml" or "html" => TokenizeXml(text),
                "css" or "scss" => TokenizeCss(text),
                _ => new List<Token>()
            };
        }

        private static List<Token> TokenizeCSharp(string text)
        {
            var tokens = new List<Token>();
            
            // Keywords
            var keywords = @"\b(abstract|as|base|bool|break|byte|case|catch|char|checked|class|const|continue|decimal|default|delegate|do|double|else|enum|event|explicit|extern|false|finally|fixed|float|for|foreach|goto|if|implicit|in|int|interface|internal|is|lock|long|namespace|new|null|object|operator|out|override|params|private|protected|public|readonly|ref|return|sbyte|sealed|short|sizeof|stackalloc|static|string|struct|switch|this|throw|true|try|typeof|uint|ulong|unchecked|unsafe|ushort|using|virtual|void|volatile|while|async|await|var|dynamic|from|select|where|orderby|join|let|into|on|equals|by|ascending|descending|group)\b";
            
            // Strings
            var stringPattern = @"@?""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'";
            
            // Comments
            var commentPattern = @"//.*?$|/\*[\s\S]*?\*/";
            
            // Numbers
            var numberPattern = @"\b\d+\.?\d*[fFdDmM]?\b|0[xX][0-9a-fA-F]+";

            // Combine all patterns
            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizePython(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(False|None|True|and|as|assert|async|await|break|class|continue|def|del|elif|else|except|finally|for|from|global|if|import|in|is|lambda|nonlocal|not|or|pass|raise|return|try|while|with|yield)\b";
            var stringPattern = @"(?:""""""[\s\S]*?""""""|'''[\s\S]*?'''|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*')";
            var commentPattern = @"#.*?$";
            var numberPattern = @"\b\d+\.?\d*\b";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeJavaScript(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(abstract|arguments|await|boolean|break|byte|case|catch|char|class|const|continue|debugger|default|delete|do|double|else|enum|eval|export|extends|false|final|finally|float|for|function|goto|if|implements|import|in|instanceof|int|interface|let|long|native|new|null|package|private|protected|public|return|short|static|super|switch|synchronized|this|throw|throws|transient|true|try|typeof|var|void|volatile|while|with|yield)\b";
            var stringPattern = @"`(?:[^`\\]|\\.)*`|""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'";
            var commentPattern = @"//.*?$|/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*\b";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeCpp(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(alignas|alignof|and|and_eq|asm|auto|bitand|bitor|bool|break|case|catch|char|char8_t|char16_t|char32_t|class|compl|concept|const|consteval|constexpr|constinit|const_cast|continue|co_await|co_return|co_yield|decltype|default|delete|do|double|dynamic_cast|else|enum|explicit|export|extern|false|float|for|friend|goto|if|inline|int|long|mutable|namespace|new|noexcept|not|not_eq|nullptr|operator|or|or_eq|private|protected|public|register|reinterpret_cast|requires|return|short|signed|sizeof|static|static_assert|static_cast|struct|switch|template|this|thread_local|throw|true|try|typedef|typeid|typename|union|unsigned|using|virtual|void|volatile|wchar_t|while|xor|xor_eq)\b";
            var stringPattern = @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'";
            var commentPattern = @"//.*?$|/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*[fFlLuU]?\b|0[xX][0-9a-fA-F]+";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeSql(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(SELECT|FROM|WHERE|INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TABLE|INDEX|VIEW|JOIN|INNER|LEFT|RIGHT|OUTER|ON|AND|OR|NOT|NULL|IS|AS|ORDER|BY|GROUP|HAVING|DISTINCT|COUNT|SUM|AVG|MIN|MAX|UNION|ALL|CASE|WHEN|THEN|ELSE|END|BEGIN|COMMIT|ROLLBACK|TRANSACTION)\b";
            var stringPattern = @"'(?:[^'\\]|\\.)*'";
            var commentPattern = @"--.*?$|/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*\b";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeJson(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(true|false|null)\b";
            var stringPattern = @"""(?:[^""\\]|\\.)*""";
            var numberPattern = @"-?\d+\.?\d*([eE][+-]?\d+)?";

            var pattern = $"(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeXml(string text)
        {
            var tokens = new List<Token>();
            
            var commentPattern = @"<!--[\s\S]*?-->";
            var stringPattern = @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'";
            var tagPattern = @"</?[\w:]+|/?>|=";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{tagPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }

        private static List<Token> TokenizeCss(string text)
        {
            var tokens = new List<Token>();
            
            var keywords = @"\b(color|background|border|margin|padding|width|height|display|position|top|left|right|bottom|font|text|flex|grid|important)\b";
            var stringPattern = @"""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)*'";
            var commentPattern = @"/\*[\s\S]*?\*/";
            var numberPattern = @"\b\d+\.?\d*(px|em|rem|%|vh|vw)?\b";

            var pattern = $"(?<comment>{commentPattern})|(?<string>{stringPattern})|(?<keyword>{keywords})|(?<number>{numberPattern})";
            var regex = new Regex(pattern, RegexOptions.Multiline);

            foreach (Match match in regex.Matches(text))
            {
                TokenType type = TokenType.Text;
                
                if (match.Groups["comment"].Success)
                    type = TokenType.Comment;
                else if (match.Groups["string"].Success)
                    type = TokenType.String;
                else if (match.Groups["keyword"].Success)
                    type = TokenType.Keyword;
                else if (match.Groups["number"].Success)
                    type = TokenType.Number;

                tokens.Add(new Token
                {
                    Type = type,
                    Start = match.Index,
                    Length = match.Length,
                    Text = match.Value
                });
            }

            return tokens;
        }
    }
}
