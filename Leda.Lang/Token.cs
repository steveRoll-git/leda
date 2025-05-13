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
    public virtual string Value => "";

    /// <summary>
    /// The name of this kind of token. (May be the same as the token's value.)
    /// </summary>
    public virtual string KindName => $"\"{Value}\"";

    public virtual bool IsBinary => false;

    /// <summary>
    /// The token's binary precedence, if it's a binary operator.
    /// </summary>
    public virtual int Precedence => -1;

    /// <summary>
    /// Whether this operator is right associative, if it's a binary operator.
    /// </summary>
    public virtual bool RightAssociative => false;

    public virtual bool IsUnary => false;

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
        public Name() { }

        public Name(Position position, string value) : base(FromWordRange(position, value))
        {
            Value = value;
        }

        public override string Value { get; }
        public override string KindName => "name";
        public override string ToString() => Value;
    }

    /// <summary>
    /// A number token.
    /// </summary>
    public sealed record Number : Token
    {
        public Number(Position position, string value, double numberValue) : base(FromWordRange(position, value))
        {
            Value = value;
            NumberValue = numberValue;
        }

        public override string Value { get; }
        public double NumberValue { get; }
        public override string KindName => "number";
    }

    /// <summary>
    /// A single-line string literal.
    /// </summary>
    public sealed record String : Token
    {
        public String(Range range, string value) : base(range)
        {
            Value = value;
        }

        public override string Value { get; }
        public override string KindName => "string";
    }

    /// <summary>
    /// A multiline string literal surrounded with long brackets.
    /// </summary>
    public sealed record LongString : Token
    {
        public LongString(int level, Range range, string value) : base(range)
        {
            Level = level;
            Value = value;
        }

        /// <summary>
        /// The number of equal signs in the long brackets.
        /// </summary>
        public int Level { get; }

        public override string Value { get; }
        public override string KindName => "string";
    }

    #region Keyword Tokens

    /// <summary>
    /// The `and` keyword.
    /// </summary>
    public sealed record And : Token
    {
        public const string Keyword = "and";
        public override string Value => Keyword;
        public override bool IsBinary => true;
        public override int Precedence => 1;
    }

    /// <summary>
    /// The `break` keyword.
    /// </summary>
    public sealed record Break : Token
    {
        public const string Keyword = "break";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `do` keyword.
    /// </summary>
    public sealed record Do : Token
    {
        public const string Keyword = "do";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `else` keyword.
    /// </summary>
    public sealed record Else : Token
    {
        public const string Keyword = "else";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `elseif` keyword.
    /// </summary>
    public sealed record Elseif : Token
    {
        public const string Keyword = "elseif";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `end` keyword.
    /// </summary>
    public sealed record End : Token
    {
        public const string Keyword = "end";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `false` keyword.
    /// </summary>
    public sealed record False : Token
    {
        public const string Keyword = "false";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `for` keyword.
    /// </summary>
    public sealed record For : Token
    {
        public const string Keyword = "for";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `function` keyword.
    /// </summary>
    public sealed record Function : Token
    {
        public const string Keyword = "function";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `if` keyword.
    /// </summary>
    public sealed record If : Token
    {
        public const string Keyword = "if";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `in` keyword.
    /// </summary>
    public sealed record In : Token
    {
        public const string Keyword = "in";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `local` keyword.
    /// </summary>
    public sealed record Local : Token
    {
        public const string Keyword = "local";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `nil` keyword.
    /// </summary>
    public sealed record Nil : Token
    {
        public const string Keyword = "nil";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `not` keyword.
    /// </summary>
    public sealed record Not : Token
    {
        public const string Keyword = "not";
        public override string Value => Keyword;
        public override bool IsUnary => true;
    }

    /// <summary>
    /// The `or` keyword.
    /// </summary>
    public sealed record Or : Token
    {
        public const string Keyword = "or";
        public override string Value => Keyword;
        public override bool IsBinary => true;
        public override int Precedence => 0;
    }

    /// <summary>
    /// The `repeat` keyword.
    /// </summary>
    public sealed record Repeat : Token
    {
        public const string Keyword = "repeat";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `return` keyword.
    /// </summary>
    public sealed record Return : Token
    {
        public const string Keyword = "return";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `then` keyword.
    /// </summary>
    public sealed record Then : Token
    {
        public const string Keyword = "then";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `true` keyword.
    /// </summary>
    public sealed record True : Token
    {
        public const string Keyword = "true";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `until` keyword.
    /// </summary>
    public sealed record Until : Token
    {
        public const string Keyword = "until";
        public override string Value => Keyword;
    }

    /// <summary>
    /// The `while` keyword.
    /// </summary>
    public sealed record While : Token
    {
        public const string Keyword = "while";
        public override string Value => Keyword;
    }

    #endregion

    #region Punctuation Tokens

    public sealed record Plus : Token
    {
        public const string Punctuation = "+";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 4;
    }

    public sealed record Minus : Token
    {
        public const string Punctuation = "-";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 4;
        public override bool IsUnary => true;
    }

    public sealed record Multiply : Token
    {
        public const string Punctuation = "*";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 5;
    }

    public sealed record Divide : Token
    {
        public const string Punctuation = "/";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 5;
    }

    public sealed record Modulo : Token
    {
        public const string Punctuation = "%";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 5;
    }

    public sealed record Power : Token
    {
        public const string Punctuation = "^";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 6;
        public override bool RightAssociative => true;
    }

    public sealed record Length : Token
    {
        public const string Punctuation = "#";
        public override string Value => Punctuation;
        public override bool IsUnary => true;
    }

    public sealed record Equal : Token
    {
        public const string Punctuation = "==";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record NotEqual : Token
    {
        public const string Punctuation = "~=";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record LessEqual : Token
    {
        public const string Punctuation = "<=";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record GreaterEqual : Token
    {
        public const string Punctuation = ">=";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record Less : Token
    {
        public const string Punctuation = "<";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record Greater : Token
    {
        public const string Punctuation = ">";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 2;
    }

    public sealed record Assign : Token
    {
        public const string Punctuation = "=";
        public override string Value => Punctuation;
    }

    public sealed record LParen : Token
    {
        public const string Punctuation = "(";
        public override string Value => Punctuation;
    }

    public sealed record RParen : Token
    {
        public const string Punctuation = ")";
        public override string Value => Punctuation;
    }

    public sealed record LCurly : Token
    {
        public const string Punctuation = "{";
        public override string Value => Punctuation;
    }

    public sealed record RCurly : Token
    {
        public const string Punctuation = "}";
        public override string Value => Punctuation;
    }

    public sealed record LSquare : Token
    {
        public const string Punctuation = "[";
        public override string Value => Punctuation;
    }

    public sealed record RSquare : Token
    {
        public const string Punctuation = "]";
        public override string Value => Punctuation;
    }

    public sealed record Semicolon : Token
    {
        public const string Punctuation = ";";
        public override string Value => Punctuation;
    }

    public sealed record Colon : Token
    {
        public const string Punctuation = ":";
        public override string Value => Punctuation;
    }

    public sealed record Comma : Token
    {
        public const string Punctuation = ",";
        public override string Value => Punctuation;
    }

    public sealed record Dot : Token
    {
        public const string Punctuation = ".";
        public override string Value => Punctuation;
    }

    public sealed record Concat : Token
    {
        public const string Punctuation = "..";
        public override string Value => Punctuation;
        public override bool IsBinary => true;
        public override int Precedence => 3;
        public override bool RightAssociative => true;
    }

    public sealed record Vararg : Token
    {
        public const string Punctuation = "...";
        public override string Value => Punctuation;
    }

    #endregion

    /// <summary>
    /// A map of strings to their respective tokens.
    /// </summary>
    public static readonly Dictionary<string, Token> StringTokenMap = new()
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
        { Plus.Punctuation, new Plus() },
        { Minus.Punctuation, new Minus() },
        { Multiply.Punctuation, new Multiply() },
        { Divide.Punctuation, new Divide() },
        { Modulo.Punctuation, new Modulo() },
        { Power.Punctuation, new Power() },
        { Length.Punctuation, new Length() },
        { Equal.Punctuation, new Equal() },
        { NotEqual.Punctuation, new NotEqual() },
        { LessEqual.Punctuation, new LessEqual() },
        { GreaterEqual.Punctuation, new GreaterEqual() },
        { Less.Punctuation, new Less() },
        { Greater.Punctuation, new Greater() },
        { Assign.Punctuation, new Assign() },
        { LParen.Punctuation, new LParen() },
        { RParen.Punctuation, new RParen() },
        { LCurly.Punctuation, new LCurly() },
        { RCurly.Punctuation, new RCurly() },
        { LSquare.Punctuation, new LSquare() },
        { RSquare.Punctuation, new RSquare() },
        { Semicolon.Punctuation, new Semicolon() },
        { Colon.Punctuation, new Colon() },
        { Comma.Punctuation, new Comma() },
        { Dot.Punctuation, new Dot() },
        { Concat.Punctuation, new Concat() },
        { Vararg.Punctuation, new Vararg() },
    };
}