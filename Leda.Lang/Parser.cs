namespace Leda.Lang;

/// <summary>
/// Parses the token stream from a `Lexer` into a syntax tree.
/// </summary>
public class Parser
{
    private static readonly Token.Name Name = new();
    private static readonly Token.RParen RParen = new();
    private static readonly Token.Assign Assign = new();

    private readonly Source source;

    private IDiagnosticReporter reporter;

    private Lexer lexer;

    /// <summary>
    /// The last token that was read.
    /// </summary>
    private Token token;

    public Parser(Source source, IDiagnosticReporter reporter)
    {
        this.source = source;
        this.reporter = reporter;
        lexer = new Lexer(source, reporter);
        token = lexer.ReadToken();
    }

    /// <summary>
    /// Reads the next token from the lexer.
    /// </summary>
    private void NextToken()
    {
        token = lexer.ReadToken();
    }

    /// <summary>
    /// Returns the current token, and reads the next one.
    /// </summary>
    private Token Consume()
    {
        var consumed = token;
        NextToken();
        return consumed;
    }

    /// <summary>
    /// If the current token is of type `T`, consumes it and returns `true`, otherwise returns `false`.
    /// </summary>
    private bool Accept<T>() where T : Token
    {
        if (token is T)
        {
            NextToken();
            return true;
        }

        return false;
    }

    /// <summary>
    /// If the current token is of type `T`, consumes it and returns it, otherwise reports and error and returns the
    /// template token.<br/>
    /// This method receives a token value because it needs to print information about it in case of an error.
    /// </summary>
    private T Expect<T>(T expected) where T : Token
    {
        var got = token;
        if (got is T gotT)
        {
            NextToken();
            return gotT;
        }

        reporter.Report(new Diagnostic.ExpectedTokenButGotToken(source, expected, got));
        return expected;
    }

    /// <summary>
    /// Parse a block of statements.
    /// </summary>
    public Tree.Block ParseBlock()
    {
        var statements = new List<Tree>();
        while (true)
        {
            if (token is Token.End or Token.Eof)
            {
                // `end` will be consumed by this method's caller.
                break;
            }

            var statement = ParseStatement();
            statements.Add(statement);

            // This behavior models Lua 5.1's syntax - in Lua 5.2+, semicolons may be their own statements.
            Accept<Token.Semicolon>();

            if (statement is Tree.Return or Tree.Break)
            {
                // No more statements can come after `return` or `break`.
                break;
            }
        }

        return new Tree.Block(statements);
    }

    /// <summary>
    /// Parse a statement.
    /// </summary>
    private Tree ParseStatement()
    {
        // 'return' [explist]
        if (Accept<Token.Return>())
        {
            if (token is Token.Semicolon or Token.End or Token.Eof)
            {
                return new Tree.Return(null);
            }
            else
            {
                return new Tree.Return(ParseExpression());
            }
        }

        if (Accept<Token.Break>())
        {
            return new Tree.Break();
        }

        var value = ParsePrefixExpression();

        if (value is Tree.Call)
        {
            return value;
        }

        // TODO parse varlist if there's a comma

        Expect(Assign);

        var expressions = ParseExpressionList();

        return new Tree.Assignment([value], expressions);
    }

    private Tree ParsePrefixExpression()
    {
        // '(' exp ')'
        if (Accept<Token.LParen>())
        {
            var expression = ParseExpression();
            Expect(RParen);
            return ParsePrefixExpression(expression);
        }

        var name = Expect(Name);
        return ParsePrefixExpression(new Tree.Name(name.Value));
    }

    private Tree ParsePrefixExpression(Tree previous)
    {
        // '.' Name
        if (Accept<Token.Dot>())
        {
            var name = Expect(Name);
            return ParsePrefixExpression(new Tree.Access(previous, new Tree.String(name.Value)));
        }

        // '[' exp ']'
        if (Accept<Token.LSquare>())
        {
            var expression = ParseExpression();
            return ParsePrefixExpression(new Tree.Access(previous, expression));
        }

        // '(' explist ')'
        if (Accept<Token.LParen>())
        {
            var parameters = ParseExpressionList();
            return ParsePrefixExpression(new Tree.Call(previous, parameters));
        }

        return previous;
    }

    /// <summary>
    /// Parse a list of expressions separated by commas.
    /// </summary>
    private List<Tree> ParseExpressionList()
    {
        // {exp ','} exp
        List<Tree> values = new();
        while (true)
        {
            values.Add(ParseExpression());
            if (!Accept<Token.Comma>())
            {
                break;
            }
        }

        return values;
    }

    /// <summary>
    /// Parse an expression with binary operators.
    /// </summary>
    private Tree ParseExpression()
    {
        return ParseExpression(ParsePrimary(), 0);
    }

    /// <summary>
    /// Parse an expression using precedence climbing.<br/>
    /// https://en.wikipedia.org/wiki/Operator-precedence_parser
    /// </summary>
    private Tree ParseExpression(Tree left, int minPrecedence)
    {
        while (token.IsBinary && token.Precedence >= minPrecedence)
        {
            var op = Consume();
            var right = ParsePrimary();
            while (token.IsBinary && (token.Precedence > op.Precedence ||
                                      (token.Precedence == op.Precedence && token.RightAssociative)))
            {
                right = ParseExpression(right, op.Precedence + (token.Precedence > op.Precedence ? 1 : 0));
            }

            left = op switch
            {
                Token.Plus => new Tree.Add(left, right),
                Token.Minus => new Tree.Subtract(left, right),
                Token.Multiply => new Tree.Multiply(left, right),
                Token.Divide => new Tree.Divide(left, right),
                Token.Modulo => new Tree.Modulo(left, right),
                Token.Power => new Tree.Power(left, right),
                _ => throw new Exception() // Unreachable.
            };
        }

        return left;
    }

    private Tree ParsePrimary()
    {
        // unop exp
        if (token.IsUnary)
        {
            var op = token;
            var expression = ParseExpression();
            return op switch
            {
                Token.Not => new Tree.Not(expression),
                Token.Length => new Tree.Length(expression),
                Token.Minus => new Tree.Negate(expression),
                _ => throw new Exception() // Unreachable.
            };
        }

        if (Accept<Token.Vararg>())
        {
            return new Tree.Vararg();
        }

        if (Accept<Token.Nil>())
        {
            return new Tree.Nil();
        }

        if (Accept<Token.False>())
        {
            return new Tree.False();
        }

        if (Accept<Token.True>())
        {
            return new Tree.True();
        }

        if (token is Token.Number numberToken)
        {
            NextToken();
            return new Tree.Number();
        }

        if (token is Token.String stringToken)
        {
            NextToken();
            return new Tree.String(stringToken.Value);
        }

        if (token is Token.LongString longString)
        {
            NextToken();
            return new Tree.LongString(longString.Value, longString.Level);
        }

        return ParsePrefixExpression();
    }
}