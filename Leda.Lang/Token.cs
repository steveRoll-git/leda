namespace Leda.Lang;

/// <summary>
/// Source code is separated into tokens before being turned into a syntax tree.
/// </summary>
public record Token
{
    /// <summary>
    /// The range in the source code that this token occupies.
    /// </summary>
    public Range Range { get; set; }

    /// <summary>
    /// Sets this token's `Range` based on this position and the token's `Value`.
    /// </summary>
    public Position WordRange
    {
        set => Range = FromWordRange(value, Value);
    }

    /// <summary>
    /// This token's contents as a string.
    /// </summary>
    public virtual string Value { get; } = "";

    public Token() { }

    public Token(Range range)
    {
        Range = range;
    }

    /// <summary>
    /// Given a starting position and a word, returns the range this word occupies.
    /// </summary>
    private static Range FromWordRange(Position position, string word) =>
        new(position, new Position(position.Line, position.Character + word.Length));

    /// <summary>
    /// An end of file token.
    /// </summary>
    public sealed record Eof : Token
    {
        public Eof(Position position) : base(new Range(position, position)) { }

        public override string Value => "<EOF>";
    }

    /// <summary>
    /// Any name that isn't a keyword.
    /// </summary>
    public sealed record Name : Token
    {
        public Name(Position position, string value) : base(FromWordRange(position, value))
        {
            Value = value;
        }

        public override string Value { get; }
    }

    /// <summary>
    /// A number token.
    /// </summary>
    public sealed record Number : Token
    {
        public Number(Position position, string value) : base(FromWordRange(position, value))
        {
            Value = value;
        }

        public override string Value { get; }
    }

    #region Keyword Types

    /// <summary>
    /// The `and` keyword.
    /// </summary>
    public sealed record And : Token
    {
        public const string Keyword = "and";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `break` keyword.
    /// </summary>
    public sealed record Break : Token
    {
        public const string Keyword = "break";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `do` keyword.
    /// </summary>
    public sealed record Do : Token
    {
        public const string Keyword = "do";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `else` keyword.
    /// </summary>
    public sealed record Else : Token
    {
        public const string Keyword = "else";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `elseif` keyword.
    /// </summary>
    public sealed record Elseif : Token
    {
        public const string Keyword = "elseif";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `end` keyword.
    /// </summary>
    public sealed record End : Token
    {
        public const string Keyword = "end";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `false` keyword.
    /// </summary>
    public sealed record False : Token
    {
        public const string Keyword = "false";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `for` keyword.
    /// </summary>
    public sealed record For : Token
    {
        public const string Keyword = "for";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `function` keyword.
    /// </summary>
    public sealed record Function : Token
    {
        public const string Keyword = "function";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `if` keyword.
    /// </summary>
    public sealed record If : Token
    {
        public const string Keyword = "if";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `in` keyword.
    /// </summary>
    public sealed record In : Token
    {
        public const string Keyword = "in";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `local` keyword.
    /// </summary>
    public sealed record Local : Token
    {
        public const string Keyword = "local";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `nil` keyword.
    /// </summary>
    public sealed record Nil : Token
    {
        public const string Keyword = "nil";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `not` keyword.
    /// </summary>
    public sealed record Not : Token
    {
        public const string Keyword = "not";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `or` keyword.
    /// </summary>
    public sealed record Or : Token
    {
        public const string Keyword = "or";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `repeat` keyword.
    /// </summary>
    public sealed record Repeat : Token
    {
        public const string Keyword = "repeat";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `return` keyword.
    /// </summary>
    public sealed record Return : Token
    {
        public const string Keyword = "return";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `then` keyword.
    /// </summary>
    public sealed record Then : Token
    {
        public const string Keyword = "then";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `true` keyword.
    /// </summary>
    public sealed record True : Token
    {
        public const string Keyword = "true";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `until` keyword.
    /// </summary>
    public sealed record Until : Token
    {
        public const string Keyword = "until";
        public override string Value { get; } = Keyword;
    }

    /// <summary>
    /// The `while` keyword.
    /// </summary>
    public sealed record While : Token
    {
        public const string Keyword = "while";
        public override string Value { get; } = Keyword;
    }

    #endregion

    /// <summary>
    /// A map of strings to their respective keyword tokens.
    /// </summary>
    public static Dictionary<string, Token> Keywords = new()
    {
        { And.Keyword, new And() },
        { Break.Keyword, new Break() },
        { Do.Keyword, new Do() },
        { Else.Keyword, new Else() },
        { Elseif.Keyword, new Elseif() },
        { End.Keyword, new End() },
        { False.Keyword, new False() },
        { For.Keyword, new For() },
        { Function.Keyword, new Function() },
        { If.Keyword, new If() },
        { In.Keyword, new In() },
        { Local.Keyword, new Local() },
        { Nil.Keyword, new Nil() },
        { Not.Keyword, new Not() },
        { Or.Keyword, new Or() },
        { Repeat.Keyword, new Repeat() },
        { Return.Keyword, new Return() },
        { Then.Keyword, new Then() },
        { True.Keyword, new True() },
        { Until.Keyword, new Until() },
        { While.Keyword, new While() },
    };
}