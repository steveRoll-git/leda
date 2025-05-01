namespace Leda.Lang;

/// <summary>
/// Parses the token stream from a `Lexer` into a syntax tree.
/// </summary>
public class Parser
{
    private static readonly Token.Name Name = new();
    private static readonly Token.LCurly LCurly = new();
    private static readonly Token.RParen RParen = new();
    private static readonly Token.RSquare RSquare = new();
    private static readonly Token.RCurly RCurly = new();
    private static readonly Token.Assign Assign = new();
    private static readonly Token.If If = new();
    private static readonly Token.Then Then = new();
    private static readonly Token.End End = new();
    private static readonly Token.Local Local = new();

    private readonly Source source;

    private IDiagnosticReporter reporter;

    private readonly Lexer lexer;

    /// <summary>
    /// The last token that was read.
    /// </summary>
    private Token token;

    /// <summary>
    /// Stores tokens that were received when Lookahead was called.
    /// </summary>
    private List<Token> lookaheadTokens = [];

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
        if (lookaheadTokens.Count > 0)
        {
            token = lookaheadTokens[0];
            lookaheadTokens.RemoveAt(0);
        }
        else
        {
            token = lexer.ReadToken();
        }
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
        if (token is not T)
        {
            return false;
        }

        NextToken();
        return true;
    }

    /// <summary>
    /// If the current token is of type `T`, consumes it and returns it, otherwise reports an error and returns the
    /// template token.<br/>
    /// This method receives a token value because it needs to print information about it in case of an error.
    /// </summary>
    private T Expect<T>(T expected) where T : Token
    {
        var got = Consume();
        if (got is T gotT)
        {
            return gotT;
        }

        reporter.Report(new Diagnostic.ExpectedTokenButGotToken(source, expected, got));
        return expected;
    }

    /// <summary>
    /// Looks ahead in the token stream and returns the `i`th next token. (0 is the current token, 1 is the token after
    /// that, etc.)
    /// </summary>
    private Token Lookahead(int index)
    {
        if (index == 0)
        {
            return token;
        }

        var listStart = lookaheadTokens.Count;

        for (var i = 0; i < index; i++)
        {
            lookaheadTokens.Add(lexer.ReadToken());
        }

        return lookaheadTokens[listStart + index - 1];
    }

    /// <summary>
    /// Parse a block of statements.
    /// </summary>
    public Tree.Block ParseBlock()
    {
        var statements = new List<Tree>();
        while (token is not (Token.End or Token.Else or Token.Elseif or Token.Eof))
        {
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

        return new Tree.Block(statements, []);
    }

    /// <summary>
    /// Parse a statement.
    /// </summary>
    private Tree ParseStatement()
    {
        // 'return' [explist]
        if (Accept<Token.Return>())
        {
            return token is Token.Semicolon or Token.End or Token.Eof
                ? new Tree.Return(null)
                : new Tree.Return(ParseExpression());
        }

        if (Accept<Token.Break>())
        {
            return new Tree.Break();
        }

        if (token is Token.If)
        {
            return ParseIfStatement();
        }

        if (token is Token.Local)
        {
            return ParseLocalDeclaration();
        }

        // Parse assignment or function call.
        var value = ParsePrefixExpression();

        if (value is Tree.Call or Tree.Error)
        {
            return value;
        }

        // TODO parse varlist if there's a comma

        Expect(Assign);

        var expressions = ParseExpressionList();

        return new Tree.Assignment([value], expressions);
    }

    /// <summary>
    /// Parse an if statement, along with any elseifs and a final else.
    /// </summary>
    private Tree.If ParseIfStatement()
    {
        Tree.IfBranch primary;

        // 'if' exp 'then' block
        {
            Expect(If);
            var condition = ParseExpression();
            Expect(Then);
            var body = ParseBlock();
            primary = new Tree.IfBranch(condition, body);
        }

        List<Tree.IfBranch> elseIfs = [];

        // 'elseif' exp 'then' block
        while (Accept<Token.Elseif>())
        {
            var condition = ParseExpression();
            Expect(Then);
            var body = ParseBlock();
            elseIfs.Add(new Tree.IfBranch(condition, body));
        }

        Tree.Block? elseBody = null;

        // 'else' block
        if (Accept<Token.Else>())
        {
            elseBody = ParseBlock();
        }

        Expect(End);

        return new Tree.If(primary, elseIfs, elseBody);
    }

    /// <summary>
    /// Parses the declaration of a variable or parameter.
    /// </summary>
    private Tree.Declaration ParseDeclaration()
    {
        // name [':' type]
        var name = Expect(Name).Value;

        Tree.TypeDeclaration? type = null;
        if (Accept<Token.Colon>())
        {
            type = ParseType();
        }

        return new Tree.Declaration(name, type);
    }

    private Tree.TypeDeclaration ParseType()
    {
        // TODO incomplete
        return new Tree.TypeDeclaration.Name(Expect(Name).Value);
    }

    private Tree.LocalDeclaration ParseLocalDeclaration()
    {
        // 'local' declaration [',' declaration] = explist
        Expect(Local);

        List<Tree.Declaration> declarations = [];
        do
        {
            declarations.Add(ParseDeclaration());
        } while (Accept<Token.Comma>());

        List<Tree> values = [];
        if (Accept<Token.Assign>())
        {
            values = ParseExpressionList();
        }

        return new Tree.LocalDeclaration(declarations, values);
    }

    /// <summary>
    /// Parses either an access, or a function call.
    /// </summary>
    /// <returns></returns>
    private Tree ParsePrefixExpression()
    {
        // '(' exp ')'
        if (Accept<Token.LParen>())
        {
            var expression = ParseExpression();
            Expect(RParen);
            return ParsePrefixExpression(expression);
        }

        if (token is Token.Name)
        {
            var name = Consume();
            return ParsePrefixExpression(new Tree.Name(name.Value));
        }

        reporter.Report(new Diagnostic.DidNotExpectTokenHere(source, Consume()));

        return new Tree.Error();
    }

    private Tree ParsePrefixExpression(Tree previous)
    {
        // Access with a dot: '.' Name
        if (Accept<Token.Dot>())
        {
            var name = Expect(Name);
            return ParsePrefixExpression(new Tree.Access(previous, new Tree.String(name.Value)));
        }

        // Access with square brackets: '[' exp ']'
        if (Accept<Token.LSquare>())
        {
            var expression = ParseExpression();
            Expect(RSquare);
            return ParsePrefixExpression(new Tree.Access(previous, expression));
        }

        // Function call: '(' [explist] ')'
        if (Accept<Token.LParen>())
        {
            if (Accept<Token.RParen>())
            {
                return ParsePrefixExpression(new Tree.Call(previous, []));
            }

            var parameters = ParseExpressionList();
            Expect(RParen);
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
        List<Tree> values = [];
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
                Token.Concat => new Tree.Concat(left, right),
                Token.Equal => new Tree.Equal(left, right),
                Token.NotEqual => new Tree.NotEqual(left, right),
                Token.LessEqual => new Tree.LessEqual(left, right),
                Token.GreaterEqual => new Tree.GreaterEqual(left, right),
                Token.Less => new Tree.Less(left, right),
                Token.Greater => new Tree.Greater(left, right),
                Token.And => new Tree.And(left, right),
                Token.Or => new Tree.Or(left, right),
                _ => throw new Exception() // Unreachable.
            };
        }

        return left;
    }

    private Tree.Table ParseTableConstructor()
    {
        Expect(LCurly);

        var lastNumberIndex = 1;
        List<Tree.TableField> fields = [];

        while (!Accept<Token.RCurly>())
        {
            Tree key;
            // '[' exp ']' '=' exp
            if (Accept<Token.LSquare>())
            {
                key = ParseExpression();
                Expect(RSquare);
                Expect(Assign);
            }
            // name '=' exp
            else if (token is Token.Name name && Lookahead(1) is Token.Assign)
            {
                key = new Tree.String(name.Value);
                NextToken();
                NextToken();
            }
            // Just an expression, will be added at the last number index
            else
            {
                key = new Tree.Number(lastNumberIndex.ToString(), lastNumberIndex);
                lastNumberIndex++;
            }

            var value = ParseExpression();
            fields.Add(new(key, value));

            if (!Accept<Token.Comma>())
            {
                Expect(RCurly);
                break;
            }
        }


        return new(fields);
    }

    private Tree ParsePrimary()
    {
        // unop exp
        if (token.IsUnary)
        {
            var op = Consume();
            var expression = ParsePrimary();
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
            return new Tree.Number(numberToken.Value, numberToken.NumberValue);
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

        if (token is Token.LCurly)
        {
            return ParseTableConstructor();
        }

        return ParsePrefixExpression();
    }
}