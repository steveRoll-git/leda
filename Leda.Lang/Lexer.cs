using System.Text;

namespace Leda.Lang;

/// <summary>
/// Reads a source file into a stream of tokens.
/// </summary>
public class Lexer
{
    private readonly Source source;

    private string Code => source.Code;

    /// <summary>
    /// The index in the source string the Lexer is currently at.
    /// </summary>
    private int index;

    /// <summary>
    /// The position the Lexer is currently at - where the next token reading will start.
    /// </summary>
    private Position position;

    /// <summary>
    /// The position that the lexer last visited before the last `AdvanceChar` call.
    /// </summary>
    private Position prevCharPosition;

    /// <summary>
    /// Whether the end of the file has been reached.
    /// </summary>
    public bool ReachedEnd { get; private set; }

    private char CurChar => CharAt(index);

    public Lexer(Source source)
    {
        this.source = source;
    }

    /// <summary>
    /// Returns the character at index `i` in the code.
    /// </summary>
    private char CharAt(int i)
    {
        return i < Code.Length ? Code[i] : '\0';
    }

    /// <summary>
    /// Moves the current position by 1 character.
    /// </summary>
    private void AdvanceChar()
    {
        if (ReachedEnd)
        {
            return;
        }

        prevCharPosition = position;

        if (CurChar == '\n')
        {
            position.Character = 0;
            position.Line++;
        }
        else
        {
            position.Character++;
        }

        index++;

        if (index >= Code.Length)
        {
            ReachedEnd = true;
        }
    }

    /// <summary>
    /// Moves the current position by `n` characters.
    /// </summary>
    private void AdvanceChar(int n)
    {
        for (int i = 0; i < n; i++)
        {
            AdvanceChar();
        }
    }

    /// <summary>
    /// Reads the next token from the code.
    /// </summary>
    /// <returns></returns>
    public Token ReadToken()
    {
        // Skip whitespace characters.
        while (!ReachedEnd && char.IsWhiteSpace(CurChar))
        {
            AdvanceChar();
        }

        // Return an EOF token if the end was reached.
        if (ReachedEnd)
        {
            return new Token.Eof(position);
        }

        if (CurChar == '\'' || CurChar == '"')
        {
            return ReadString();
        }

        // If `CurChar` is a digit, or a period with a digit right after it...
        if (char.IsAsciiDigit(CurChar) || (CurChar == '.' && char.IsAsciiDigit(CharAt(index + 1))))
        {
            return ReadNumber();
        }

        // If `CurChar` is an underscore or letter, return the corresponding keyword token or a Name token.
        if (IsNameChar(CurChar))
        {
            return ReadName();
        }

        // Otherwise, see if the current character matches any known tokens.
        if (Token.StringTokenMap.TryGetValue(CurChar.ToString(), out var token))
        {
            var start = position;
            AdvanceChar();
            // As long as the next character still makes a valid token, add it and advance.
            while (Token.StringTokenMap.TryGetValue($"{token.Value}{CurChar}", out var otherToken))
            {
                token = otherToken;
                AdvanceChar();
            }

            return token with { WordRange = start };
        }

        // TODO error: invalid character
        AdvanceChar();

        return new Token(new(prevCharPosition, position));
    }

    private static readonly Dictionary<char, string> EscapeSequences = new()
    {
        { 'a', "\a" },
        { 'b', "\b" },
        { 'f', "\f" },
        { 'n', "\n" },
        { 'r', "\r" },
        { 't', "\t" },
        { 'v', "\v" },
        { '\\', "\\" },
        { '"', "\"" },
        { '\'', "'" },
        { '\n', "\n" },
    };

    /// <summary>
    /// Reads a single-line string literal.
    /// </summary>
    private Token ReadString()
    {
        var start = position;
        char delimiter = CurChar;
        StringBuilder value = new();

        AdvanceChar();

        while (!ReachedEnd && CurChar != delimiter)
        {
            if (CurChar == '\n')
            {
                // TODO error: unfinished string
                AdvanceChar();
                break;
            }

            if (CurChar == '\\')
            {
                AdvanceChar();
                if (char.IsAsciiDigit(CurChar))
                {
                    // A backslash followed by at most 3 digits is a decimal character code.
                    int code = 0;
                    for (int i = 0; i < 3 && char.IsAsciiDigit(CurChar); i++)
                    {
                        code = code * 10 + (CurChar - '0');
                        AdvanceChar();
                    }

                    value.Append((char)code);
                }
                else if (Code.Substring(index, 2) == "\r\n")
                {
                    // A CRLF after a backslash is a newline in the string.
                    AdvanceChar(2);
                    value.Append('\n');
                }
                else if (EscapeSequences.TryGetValue(CurChar, out var escapedChar))
                {
                    // Any of the valid escape characters.
                    AdvanceChar();
                    value.Append(escapedChar);
                }
                else
                {
                    // TODO error: invalid escape sequence
                }
            }
            else
            {
                value.Append(CurChar);
                AdvanceChar();
            }
        }

        if (CurChar == delimiter)
        {
            AdvanceChar();
        }

        return new Token.String(new(start, prevCharPosition), value.ToString());
    }

    /// <summary>
    /// Reads a number token. Checks if the number is properly formed.
    /// </summary>
    /// <returns></returns>
    private Token.Number ReadNumber()
    {
        var startIndex = index;
        var start = position;

        var valid = true;
        var seenDot = false;
        var isHex = false;
        var seenExp = false;
        var expChars = "Ee";

        // If the number starts with 0x, it's a hexadecimal.
        if (CurChar == '0' && CharAt(index + 1) == 'x')
        {
            isHex = true;
            expChars = "Pp"; // Hex numbers use 'p' as the exponent.
            AdvanceChar(2);
        }

        // "0." is valid, but "0x." isn't.
        if (CurChar == '.')
        {
            seenDot = true;
            AdvanceChar();
        }

        do
        {
            if (CurChar == '.')
            {
                if (seenDot || seenExp)
                {
                    valid = false;
                }
                else
                {
                    seenDot = true;
                }
            }
            else if (expChars.Contains(CurChar))
            {
                if (seenExp)
                {
                    valid = false;
                }
                else
                {
                    AdvanceChar();
                    // A '+' or '-' may optionally appear after the exponent character.
                    if (CurChar == '+' || CurChar == '-')
                    {
                        AdvanceChar();
                    }

                    seenExp = true;
                    isHex = false; // Numbers following the exponent are decimal.
                    continue;
                }
            }
            else if (!IsNumberChar(CurChar) || !(isHex ? char.IsAsciiHexDigit(CurChar) : char.IsDigit(CurChar)))
            {
                valid = false;
            }

            AdvanceChar();
        } while (!ReachedEnd && IsNumberChar(CurChar));

        if (!valid)
        {
            // TODO report malformed number
        }

        return new Token.Number(start, Code.Substring(startIndex, index - startIndex));
    }


    /// <summary>
    /// Reads the next name, and returns its corresponding keyword token, or a Name token.
    /// </summary>
    /// <returns></returns>
    private Token ReadName()
    {
        var startIndex = index;
        var start = position;

        while (!ReachedEnd && IsNameChar(CurChar))
        {
            AdvanceChar();
        }

        var value = Code.Substring(startIndex, index - startIndex);

        if (Token.StringTokenMap.TryGetValue(value, out var keyword))
        {
            return keyword with { WordRange = start };
        }

        return new Token.Name(start, value);
    }

    /// <summary>
    /// Returns whether `c` is a character that can appear in names.
    /// </summary>
    private static bool IsNameChar(char c)
    {
        return c == '_' || char.IsLetterOrDigit(c);
    }

    /// <summary>
    /// Returns whether `c` is a character that can appear in numbers.
    /// </summary>
    private static bool IsNumberChar(char c)
    {
        return c == '.' || IsNameChar(c);
    }
}