namespace Leda.Lang;

public enum TokenKind
{
    Unknown,
    Eof,
    Name,
    Number,
    String,
    LongString,
    And,
    Break,
    Do,
    Else,
    Elseif,
    End,
    False,
    For,
    Function,
    If,
    In,
    Local,
    Nil,
    Not,
    Or,
    Repeat,
    Return,
    Then,
    True,
    Until,
    While,
    Goto,
    Plus,
    Minus,
    Multiply,
    Divide,
    Modulo,
    Power,
    Length,
    Equal,
    NotEqual,
    LessEqual,
    GreaterEqual,
    Less,
    Greater,
    Assign,
    LParen,
    RParen,
    LCurly,
    RCurly,
    LSquare,
    RSquare,
    Semicolon,
    Colon,
    DoubleColon,
    Comma,
    Dot,
    Concat,
    Vararg,
}

/// <summary>
/// The pieces that source code is separated into before being parsed into a syntax tree.
/// </summary>
/// <param name="Kind">The kind of token this is.</param>
/// <param name="Range">The range in the source code that this token occupies.</param>
/// <param name="Value">The contents of this token.</param>
/// <remarks>
/// For most tokens, the Value property contains the token's contents as they appeared in the source.<br/>
/// String and LongString tokens are an exception. Their Value contains the string's contents as they would appear in
/// memory, after being parsed by Lua.
/// </remarks>
public record Token(TokenKind Kind, Range Range, string Value)
{
    /// <summary>
    /// A number token.
    /// </summary>
    /// <param name="Range">The range in the source code that this token occupies.</param>
    /// <param name="Value">The number's value as it appeared in the source.</param>
    /// <param name="NumberValue">The number's value after being parsed into a double.</param>
    public sealed record Number(Range Range, string Value, double NumberValue) : Token(TokenKind.Number, Range, Value);

    /// <summary>
    /// A multiline string literal surrounded with long brackets.
    /// </summary>
    /// <param name="Range">The range in the source code that this token occupies.</param>
    /// <param name="Level">The number of equal signs in the long brackets.</param>
    /// <param name="Value">The contents of the string.</param>
    public sealed record LongString(Range Range, int Level, string Value) : Token(TokenKind.LongString, Range, Value);

    /// <summary>
    /// A map of strings to their respective token kinds.
    /// </summary>
    public static readonly Dictionary<string, TokenKind> StringTokenMap = new()
    {
        { "and", TokenKind.And },
        { "break", TokenKind.Break },
        { "do", TokenKind.Do },
        { "else", TokenKind.Else },
        { "elseif", TokenKind.Elseif },
        { "end", TokenKind.End },
        { "false", TokenKind.False },
        { "for", TokenKind.For },
        { "function", TokenKind.Function },
        { "if", TokenKind.If },
        { "in", TokenKind.In },
        { "local", TokenKind.Local },
        { "nil", TokenKind.Nil },
        { "not", TokenKind.Not },
        { "or", TokenKind.Or },
        { "repeat", TokenKind.Repeat },
        { "return", TokenKind.Return },
        { "then", TokenKind.Then },
        { "true", TokenKind.True },
        { "until", TokenKind.Until },
        { "while", TokenKind.While },
        { "goto", TokenKind.Goto },
        { "+", TokenKind.Plus },
        { "-", TokenKind.Minus },
        { "*", TokenKind.Multiply },
        { "/", TokenKind.Divide },
        { "%", TokenKind.Modulo },
        { "^", TokenKind.Power },
        { "#", TokenKind.Length },
        { "==", TokenKind.Equal },
        { "~=", TokenKind.NotEqual },
        { "<=", TokenKind.LessEqual },
        { ">=", TokenKind.GreaterEqual },
        { "<", TokenKind.Less },
        { ">", TokenKind.Greater },
        { "=", TokenKind.Assign },
        { "(", TokenKind.LParen },
        { ")", TokenKind.RParen },
        { "{", TokenKind.LCurly },
        { "}", TokenKind.RCurly },
        { "[", TokenKind.LSquare },
        { "]", TokenKind.RSquare },
        { ";", TokenKind.Semicolon },
        { ":", TokenKind.Colon },
        { "::", TokenKind.DoubleColon },
        { ",", TokenKind.Comma },
        { ".", TokenKind.Dot },
        { "..", TokenKind.Concat },
        { "...", TokenKind.Vararg },
    };

    private static readonly Dictionary<TokenKind, string> TokenStringMap =
        new(StringTokenMap.Select(pair => new KeyValuePair<TokenKind, string>(pair.Value, pair.Key)));

    public static string KindName(TokenKind kind) =>
        kind switch
        {
            TokenKind.Unknown => "unknown",
            TokenKind.Eof => "EOF",
            TokenKind.Name => "name",
            TokenKind.Number => "number",
            TokenKind.String => "string",
            TokenKind.LongString => "long string",
            _ => '"' + TokenStringMap[kind] + '"'
        };

    /// <summary>
    /// Returns whether the token is a unary operator.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    internal static bool IsUnary(Token token) => token.Kind is TokenKind.Minus or TokenKind.Length or TokenKind.Not;

    /// <summary>
    /// Returns the binary precedence of the token if it's a binary operator, or -1 if it isn't.
    /// </summary>
    public static int Precedence(Token token) => token.Kind switch
    {
        TokenKind.Or => 0,
        TokenKind.And => 1,
        TokenKind.Less or TokenKind.Greater or TokenKind.LessEqual or TokenKind.GreaterEqual or TokenKind.NotEqual
            or TokenKind.Equal => 2,
        TokenKind.Concat => 3,
        TokenKind.Plus or TokenKind.Minus => 4,
        TokenKind.Multiply or TokenKind.Divide or TokenKind.Modulo => 5,
        TokenKind.Power => 6,
        _ => -1
    };

    /// <summary>
    /// Returns whether the token is a binary operator, along with its binary precedence.
    /// </summary>
    internal static bool IsBinary(Token token, out int precedence)
    {
        precedence = Precedence(token);
        return precedence > -1;
    }

    /// <summary>
    /// If the token is a binary operator, returns whether it's right associative.
    /// </summary>
    internal static bool IsRightAssociative(Token token) => token.Kind is TokenKind.Concat or TokenKind.Power;
}