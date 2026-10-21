using System.Text;
using System.Text.RegularExpressions;

namespace OpenIdentityStack.Architecture.Tests;

internal static partial class SourceText
{
    private static readonly Regex TestSignature = TestSignaturePattern();

    internal readonly record struct TestMethod(string Name, int Line, string Body);

    internal static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenIdentityStack.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    internal static string Relative(string repoRoot, string file) =>
        Path.GetRelativePath(repoRoot, file).Replace('\\', '/');

    internal static int LineAt(string source, int index)
    {
        int line = 1;
        int end = Math.Min(index, source.Length);
        for (int i = 0; i < end; i++)
        {
            if (source[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>
    /// Replaces comments with spaces and keeps every other character, so indexes still
    /// point at the same place in the original file.
    /// </summary>
    internal static string StripComments(string source)
    {
        var builder = new StringBuilder(source.Length);
        int i = 0;
        while (i < source.Length)
        {
            if (StartsString(source, i))
            {
                int end = EndOfString(source, i);
                builder.Append(source, i, end - i);
                i = end;
                continue;
            }

            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    builder.Append(' ');
                    i++;
                }

                continue;
            }

            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
            {
                builder.Append(' ');
                builder.Append(' ');
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    builder.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                if (i + 1 < source.Length)
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i += 2;
                }

                continue;
            }

            builder.Append(source[i]);
            i++;
        }

        return builder.ToString();
    }

    internal static bool TryMethodBody(string source, string methodName, out int line, out string body)
    {
        string stripped = StripComments(source);
        string needle = "bool " + methodName;
        int signature = stripped.IndexOf(needle, StringComparison.Ordinal);
        if (signature < 0)
        {
            line = 0;
            body = string.Empty;
            return false;
        }

        int open = stripped.IndexOf('{', signature);
        if (open < 0)
        {
            line = 0;
            body = string.Empty;
            return false;
        }

        int close = EndOfBlock(stripped, open);
        line = LineAt(source, signature);
        body = stripped[(open + 1)..close];
        return true;
    }

    internal static List<TestMethod> TestMethods(string source)
    {
        string stripped = StripComments(source);
        var methods = new List<TestMethod>();
        int search = 0;
        while (search < stripped.Length)
        {
            int attribute = IndexOfTestAttribute(stripped, search);
            if (attribute < 0)
            {
                break;
            }

            Match signature = TestSignature.Match(stripped, attribute);
            if (!signature.Success)
            {
                search = attribute + 1;
                continue;
            }

            int closeParen = EndOfParen(stripped, signature.Index + signature.Length - 1);
            int cursor = closeParen + 1;
            while (cursor < stripped.Length && char.IsWhiteSpace(stripped[cursor]))
            {
                cursor++;
            }

            string body;
            int next;
            if (cursor < stripped.Length && stripped[cursor] == '{')
            {
                int close = EndOfBlock(stripped, cursor);
                body = stripped[(cursor + 1)..close];
                next = close + 1;
            }
            else if (cursor + 1 < stripped.Length && stripped[cursor] == '=' && stripped[cursor + 1] == '>')
            {
                int end = cursor + 2;
                int paren = 0;
                while (end < stripped.Length)
                {
                    if (StartsString(stripped, end))
                    {
                        end = EndOfString(stripped, end);
                        continue;
                    }

                    char c = stripped[end];
                    if (c == '(')
                    {
                        paren++;
                    }
                    else if (c == ')')
                    {
                        paren--;
                    }
                    else if (c == ';' && paren == 0)
                    {
                        break;
                    }

                    end++;
                }

                body = stripped[(cursor + 2)..Math.Min(end, stripped.Length)];
                next = end + 1;
            }
            else
            {
                search = signature.Index + signature.Length;
                continue;
            }

            methods.Add(new TestMethod(signature.Groups["name"].Value, LineAt(source, signature.Index), body));
            search = next;
        }

        return methods;
    }

    private static int EndOfParen(string text, int openParen)
    {
        int depth = 1;
        int i = openParen + 1;
        while (i < text.Length)
        {
            if (StartsString(text, i))
            {
                i = EndOfString(text, i);
                continue;
            }

            if (text[i] == '(')
            {
                depth++;
            }
            else if (text[i] == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }

            i++;
        }

        return text.Length - 1;
    }

    internal static bool IsUnderSkippedDirectory(string repoRoot, string file)
    {
        string relative = Path.GetRelativePath(repoRoot, file);
        foreach (string segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment is ".git" or "bin" or "obj" or "node_modules" or "dist")
            {
                return true;
            }
        }

        return false;
    }

    private static int IndexOfTestAttribute(string source, int start)
    {
        int fact = source.IndexOf("[Fact", start, StringComparison.Ordinal);
        int theory = source.IndexOf("[Theory", start, StringComparison.Ordinal);
        if (fact < 0)
        {
            return theory;
        }

        if (theory < 0)
        {
            return fact;
        }

        return Math.Min(fact, theory);
    }

    private static int EndOfBlock(string text, int openBrace)
    {
        int depth = 1;
        int i = openBrace + 1;
        while (i < text.Length)
        {
            if (StartsString(text, i))
            {
                i = EndOfString(text, i);
                continue;
            }

            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }

            i++;
        }

        throw new InvalidOperationException("Unbalanced '{'.");
    }

    private static bool StartsString(string text, int index)
    {
        int i = index;
        if (text[i] == '@')
        {
            i++;
        }

        while (i < text.Length && text[i] == '$')
        {
            i++;
        }

        if (i < text.Length && text[i] == '@')
        {
            i++;
        }

        return i < text.Length && text[i] == '"';
    }

    private static int EndOfString(string text, int start)
    {
        int i = start;
        bool verbatim = false;
        if (text[i] == '@')
        {
            verbatim = true;
            i++;
        }

        int dollars = 0;
        while (i < text.Length && text[i] == '$')
        {
            dollars++;
            i++;
        }

        if (i < text.Length && text[i] == '@')
        {
            verbatim = true;
            i++;
        }

        if (i >= text.Length || text[i] != '"')
        {
            return start + 1;
        }

        int quotes = 0;
        while (i < text.Length && text[i] == '"')
        {
            quotes++;
            i++;
        }

        if (quotes >= 3)
        {
            string closer = new('"', quotes);
            int close = text.IndexOf(closer, i, StringComparison.Ordinal);
            return close < 0 ? text.Length : close + closer.Length;
        }

        return ConsumeQuoted(text, i, dollars, verbatim);
    }

    private static int ConsumeQuoted(string text, int i, int dollars, bool verbatim)
    {
        while (i < text.Length)
        {
            if (verbatim)
            {
                if (text[i] == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    return i + 1;
                }
            }
            else if (text[i] == '\\')
            {
                i += 2;
                continue;
            }
            else if (text[i] == '"')
            {
                return i + 1;
            }

            if (dollars > 0 && text[i] == '{')
            {
                int braces = 0;
                while (i + braces < text.Length && text[i + braces] == '{')
                {
                    braces++;
                }

                if (braces >= dollars * 2)
                {
                    i += dollars * 2;
                    continue;
                }

                if (braces >= dollars)
                {
                    i = EndOfInterpolation(text, i + dollars);
                    continue;
                }
            }

            i++;
        }

        return i;
    }

    private static int EndOfInterpolation(string text, int openBrace)
    {
        int depth = 1;
        int i = openBrace + 1;
        while (i < text.Length)
        {
            if (StartsString(text, i))
            {
                i = EndOfString(text, i);
                continue;
            }

            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return i + 1;
                }
            }

            i++;
        }

        return i;
    }

    [GeneratedRegex(
        @"\b(?:async\s+)?(?:void|Task(?:\s*<[^;{]+>)?)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*(?:<[^>()]+>)?\s*\(",
        RegexOptions.CultureInvariant)]
    private static partial Regex TestSignaturePattern();
}
