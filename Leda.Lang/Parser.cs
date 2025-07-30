using System.Diagnostics.CodeAnalysis;

namespace Leda.Lang;

/// <summary>
/// Parses the token stream from a `Lexer` into a syntax tree.
/// </summary>
public class Parser
{
    private static readonly Token.Name Name = new();
    private static readonly Token.LParen LParen = new();
    private static readonly Token.LCurly LCurly = new();
    private static readonly Token.RParen RParen = new();
    private static readonly Token.RSquare RSquare = new();
    private static readonly Token.RCurly RCurly = new();
    private static readonly Token.Assign Assign = new();
    private static readonly Token.Comma Comma = new();
    private static readonly Token.Colon Colon = new();
    private static readonly Token.If If = new();
    private static readonly Token.Then Then = new();
    private static readonly Token.For For = new();
    private static readonly Token.In In = new();
    private static readonly Token.While While = new();
    private static readonly Token.Repeat Repeat = new();
    private static readonly Token.Do Do = new();
    private static readonly Token.Until Until = new();
    private static readonly Token.End End = new();
    private static readonly Token.Function Function = new();
    private static readonly Token.Local Local = new();

    /// <summary>
    /// Returns whether `token` can end a statement.
    /// </summary>
    private static bool IsStatementEndingToken(Token token) =>
        token is Token.End or Token.Else or Token.Elseif or Token.Until or Token.Eof;

    private readonly Source source;

    public List<Diagnostic> Diagnostics { get; } = [];

    private readonly Lexer lexer;

    /// <summary>
    /// The current token on the stream.
    /// </summary>
    private Token token;

    /// <summary>
    /// The last token that was consumed.
    /// </summary>
    private Token lastToken = null!;

    /// <summary>
    /// Stores tokens that were received when Lookahead was called.
    /// </summary>
    private readonly List<Token> lookaheadTokens = [];

    /// <summary>
    /// Stores the positions that were started with `StartTree`.
    /// </summary>
    private readonly Stack<Position> startPositions = new();

    private static bool IsAssignableTo(Tree tree) => tree is Tree.Name or Tree.Access;

    public Parser(Source source)
    {
        this.source = source;
        lexer = new Lexer(source) { Diagnostics = Diagnostics };
        token = lexer.ReadToken();
    }

    private void Report(Diagnostic diagnostic)
    {
        Diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Reads the next token from the lexer.
    /// </summary>
    private void NextToken()
    {
        lastToken = token;
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

    private bool Accept<T>([NotNullWhen(true)] out T? result) where T : Token
    {
        if (token is T correctToken)
        {
            result = correctToken;
            NextToken();
            return true;
        }

        result = null;
        return false;
    }

    /// <summary>
    /// If the current token is of type `T`, consumes it and returns `true`, otherwise returns `false`.
    /// </summary>
    private bool Accept<T>() where T : Token
    {
        return Accept<T>(out _);
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

        Report(new Diagnostic.ExpectedTokenButGotToken(got.Range, expected, got));
        return expected;
    }

    /// <summary>
    /// Makes this position the start position of the currently parsed tree.
    /// </summary>
    private void StartTree(Position position)
    {
        startPositions.Push(position);
    }

    /// <summary>
    /// Makes the current `token` the beginning of the currently parsed tree's range.
    /// </summary>
    private void StartTree()
    {
        StartTree(token.Range.Start);
    }

    /// <summary>
    /// Takes the last start position set by `StartTree`, and sets the tree's range to that start position and the last
    /// token's end position.
    /// </summary>
    /// <returns>The same tree for convenience.</returns>
    private T EndTree<T>(T tree) where T : Tree
    {
        var start = startPositions.Pop();
        tree.Range = new(start, lastToken.Range.End);
        return tree;
    }

    /// <summary>
    /// Sets the tree's range to the last token's range.
    /// </summary>
    /// <returns>The same tree for convenience.</returns>
    private T StartEndTree<T>(T tree) where T : Tree
    {
        tree.Range = lastToken.Range;
        return tree;
    }

    /// <summary>
    /// Consumes the current token, and sets the tree's range to that token's range.
    /// </summary>
    private T ConsumeTree<T>(T tree) where T : Tree
    {
        tree.Range = Consume().Range;
        return tree;
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

        while (lookaheadTokens.Count < index)
        {
            lookaheadTokens.Add(lexer.ReadToken());
        }

        return lookaheadTokens[index - 1];
    }

    private Tree.Name ParseValueName()
    {
        return StartEndTree(new Tree.Name(Expect(Name).Value));
    }

    private Tree.TypeDeclaration.Name ParseTypeName()
    {
        return StartEndTree(new Tree.TypeDeclaration.Name(Expect(Name).Value));
    }

    /// <summary>
    /// Parses a single identifier that represents a string.
    /// </summary>
    private Tree.String ParseStringIdentifier()
    {
        return StartEndTree(new Tree.String(Expect(Name).Value));
    }

    /// <summary>
    /// Parse a block of statements.
    /// </summary>
    private Tree.Block ParseBlock()
    {
        var statements = new List<Tree>();
        while (!IsStatementEndingToken(token))
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
        if (token is Token.Return)
        {
            StartTree();
            NextToken(); // skip 'return'
            return EndTree(IsStatementEndingToken(token)
                ? new Tree.Return(null)
                : new Tree.Return(ParseExpression()));
        }

        if (token is Token.Break)
        {
            return ConsumeTree(new Tree.Break());
        }

        if (token is Token.Do)
        {
            return ParseDo();
        }

        if (token is Token.If)
        {
            return ParseIfStatement();
        }

        if (token is Token.For)
        {
            return ParseForLoop();
        }

        if (token is Token.While)
        {
            return ParseWhileLoop();
        }

        if (token is Token.Repeat)
        {
            return ParseRepeatUntilLoop();
        }

        if (token is Token.Local)
        {
            return ParseLocalDeclaration();
        }

        if (token is Token.Function)
        {
            return ParseFunctionDeclaration();
        }

        StartTree();

        // Parse assignment or function call.
        var value = ParsePrefixExpression();

        if (value is Tree.Call or Tree.MethodCall or Tree.Error)
        {
            return EndTree(value);
        }

        if (IsAssignableTo(value))
        {
            List<Tree> targets = [value];
            while (Accept<Token.Comma>())
            {
                var target = ParsePrefixExpression();
                if (!IsAssignableTo(target))
                {
                    Report(new Diagnostic.CannotAssignToThis(target.Range));
                    targets.Add(new Tree.Error());
                }
                else
                {
                    targets.Add(target);
                }
            }

            Expect(Assign);

            var expressions = ParseExpressionList();

            return EndTree(new Tree.Assignment(targets, expressions));
        }

        var got = Consume();
        Report(new Diagnostic.DidNotExpectTokenHere(got.Range, got));

        return new Tree.Error();
    }

    private Tree.Do ParseDo()
    {
        StartTree();
        // do 'block' end
        Expect(Do);
        var block = ParseBlock();
        Expect(End);
        return EndTree(new Tree.Do(block));
    }

    /// <summary>
    /// Parse an if statement, along with any elseifs and a final else.
    /// </summary>
    private Tree.If ParseIfStatement()
    {
        Tree.IfBranch primary;

        StartTree();

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

        return EndTree(new Tree.If(primary, elseIfs, elseBody));
    }

    private Tree ParseForLoop()
    {
        StartTree();

        Expect(For);
        if (token is not Token.Name)
        {
            Report(new Diagnostic.ExpectedTokenButGotToken(Name.Range, Name, token));
        }

        // 'for' name '=' exp ',' exp [',' exp] 'do' block 'end'
        if (Lookahead(1) is Token.Assign)
        {
            var counter = ParseValueName();
            Expect(Assign);
            var start = ParseExpression();
            Expect(Comma);
            var end = ParseExpression();

            Tree? step = null;
            if (Accept<Token.Comma>())
            {
                step = ParseExpression();
            }

            Expect(Do);
            var body = ParseBlock();
            Expect(End);

            return EndTree(new Tree.NumericalFor(counter, start, end, step, body));
        }

        // 'for' declarations 'in' exp 'do' block 'end'
        {
            var declarations = ParseDeclarationList();
            Expect(In);
            var iterator = ParseExpression();
            Expect(Do);
            var body = ParseBlock();
            return EndTree(new Tree.IteratorFor(declarations, iterator, body));
        }
    }

    private Tree.While ParseWhileLoop()
    {
        StartTree();
        // 'while' exp 'do' block 'end'
        Expect(While);
        var condition = ParseExpression();
        Expect(Do);
        var body = ParseBlock();
        Expect(End);
        return EndTree(new Tree.While(condition, body));
    }

    private Tree.RepeatUntil ParseRepeatUntilLoop()
    {
        StartTree();
        // 'repeat' block 'until' exp
        Expect(Repeat);
        var body = ParseBlock();
        Expect(Until);
        var condition = ParseExpression();
        return EndTree(new Tree.RepeatUntil(body, condition));
    }

    /// <summary>
    /// Parses the declaration of a variable or parameter.
    /// </summary>
    private Tree.Declaration ParseDeclaration()
    {
        StartTree();

        // name [':' type]
        var name = ParseValueName();

        Tree.TypeDeclaration? type = null;
        if (Accept<Token.Colon>())
        {
            type = ParseType();
        }

        return EndTree(new Tree.Declaration(name, type));
    }

    private Tree.TypeDeclaration ParseType()
    {
        StartTree();

        if (Accept<Token.String>(out var str))
        {
            return EndTree(new Tree.TypeDeclaration.StringLiteral(str.Value));
        }

        if (Accept<Token.Function>())
        {
            if (token is Token.LParen)
            {
                return EndTree(ParseFunctionType());
            }

            return EndTree(new Tree.TypeDeclaration.Name("function"));
        }

        if (token is Token.LCurly)
        {
            return EndTree(ParseTableType());
        }

        // TODO incomplete
        return EndTree(ParseTypeName());
    }

    private Tree.TypeDeclaration.Table ParseTableType()
    {
        // '{' [typepair {',' typepair}] '}'

        Expect(LCurly);

        List<Tree.TypeDeclaration.Pair> pairs = [];

        while (!Accept<Token.RCurly>())
        {
            Tree.TypeDeclaration? key = null;
            if (Accept<Token.LSquare>())
            {
                key = ParseType();
                Expect(RSquare);
            }
            else if (Accept<Token.Name>(out var name))
            {
                key = new Tree.TypeDeclaration.StringLiteral(name.Value);
            }
            else
            {
                Report(new Diagnostic.DidNotExpectTokenHere(token.Range, token));
            }

            Expect(Colon);

            var value = ParseType();

            if (key != null)
            {
                pairs.Add(new(key, value));
            }

            if (!Accept<Token.Comma>())
            {
                Expect(RCurly);
                break;
            }
        }

        return new Tree.TypeDeclaration.Table(pairs);
    }

    private List<Tree> ParseTypeList()
    {
        List<Tree> list = [];
        do
        {
            list.Add(ParseType());
        } while (Accept<Token.Comma>());

        return list;
    }

    private List<Tree.Declaration> ParseDeclarationList()
    {
        List<Tree.Declaration> declarations = [];
        // declaration {',' declaration}
        do
        {
            declarations.Add(ParseDeclaration());
        } while (Accept<Token.Comma>());

        return declarations;
    }

    private Tree ParseLocalDeclaration()
    {
        StartTree();

        Expect(Local);

        if (Accept<Token.Function>())
        {
            // 'local' name funcbody
            var name = ParseValueName();
            var function = ParseFunctionBody(false);
            return EndTree(new Tree.LocalFunctionDeclaration(name, function));
        }

        // 'local' declaration {',' declaration} ['=' explist]
        var declarations = ParseDeclarationList();

        List<Tree> values = [];
        if (Accept<Token.Assign>())
        {
            values = ParseExpressionList();
        }

        return EndTree(new Tree.LocalDeclaration(declarations, values));
    }

    private Tree.Assignment ParseFunctionDeclaration()
    {
        StartTree();

        // 'function' name {'.' name} [':' name]
        Expect(Function);
        Tree path = ParseValueName();
        var isMethod = false;
        while (token is Token.Dot or Token.Colon)
        {
            StartTree(path.Range.Start);

            var separator = Consume();
            var nextName = ParseStringIdentifier();

            path = EndTree(new Tree.Access(path, nextName));

            if (separator is Token.Colon)
            {
                isMethod = true;
                break;
            }
        }

        var function = ParseFunctionBody(isMethod);

        return EndTree(new Tree.Assignment([path], [function]));
    }

    /// <summary>
    /// Parses the parameters and return type of a function.
    /// </summary>
    private Tree.TypeDeclaration.Function ParseFunctionType(bool isMethod = false)
    {
        StartTree();

        var lParenRange = token.Range;

        // '(' declarations ')' [':' typelist]
        Expect(LParen);
        List<Tree.Declaration> parameters = [];
        if (isMethod)
        {
            parameters.Add(new(new Tree.Name("self") { Range = lParenRange }, null));
        }

        if (!Accept<Token.RParen>())
        {
            parameters = ParseDeclarationList();
            Expect(RParen);
        }

        List<Tree>? returnTypes = null;
        if (Accept<Token.Colon>())
        {
            returnTypes = ParseTypeList();
        }

        return EndTree(new Tree.TypeDeclaration.Function(parameters, returnTypes));
    }

    /// <summary>
    /// Parses a function body - used in function declarations and in anonymous functions.
    /// </summary>
    private Tree.Function ParseFunctionBody(bool isMethod)
    {
        StartTree();

        // functiontype block 'end'
        var functionType = ParseFunctionType(isMethod);

        var body = ParseBlock();
        Expect(End);

        return EndTree(new Tree.Function(functionType, body, isMethod));
    }

    /// <summary>
    /// Parses either an access, or a function call.
    /// </summary>
    private Tree ParsePrefixExpression()
    {
        StartTree();

        // '(' exp ')'
        if (Accept<Token.LParen>())
        {
            var expression = ParseExpression();
            Expect(RParen);
            return ParsePrefixExpression(EndTree(expression));
        }

        if (token is Token.Name)
        {
            var name = Consume();
            return ParsePrefixExpression(EndTree(new Tree.Name(name.Value)));
        }

        var got = Consume();
        Report(new Diagnostic.DidNotExpectTokenHere(got.Range, got));

        return EndTree(new Tree.Error());
    }

    private Tree ParsePrefixExpression(Tree previous)
    {
        StartTree(previous.Range.Start);

        // Access with a dot: '.' Name
        if (Accept<Token.Dot>())
        {
            var name = ParseStringIdentifier();
            return ParsePrefixExpression(EndTree(new Tree.Access(previous, name)));
        }

        // Method call: ':' Name  '(' [explist] ')'
        if (Accept<Token.Colon>())
        {
            var funcName = ParseStringIdentifier();
            Expect(LParen);

            if (Accept<Token.RParen>())
            {
                return ParsePrefixExpression(EndTree(new Tree.MethodCall(previous, funcName, [])));
            }

            var parameters = ParseExpressionList();
            Expect(RParen);
            return ParsePrefixExpression(EndTree(new Tree.MethodCall(previous, funcName, parameters)));
        }

        // Access with square brackets: '[' exp ']'
        if (Accept<Token.LSquare>())
        {
            var expression = ParseExpression();
            Expect(RSquare);
            return ParsePrefixExpression(EndTree(new Tree.Access(previous, expression)));
        }

        // Function call: '(' [explist] ')'
        if (token is Token.LParen)
        {
            // If the '(' is on a new line, it could be a new statement that starts with it - report ambiguous syntax.
            if (previous.Range.End.Line < token.Range.Start.Line)
            {
                Report(new Diagnostic.AmbiguousSyntax(token.Range));
            }

            NextToken(); // skip '('

            if (Accept<Token.RParen>())
            {
                return ParsePrefixExpression(EndTree(new Tree.Call(previous, [])));
            }

            var parameters = ParseExpressionList();
            Expect(RParen);
            return ParsePrefixExpression(EndTree(new Tree.Call(previous, parameters)));
        }

        return EndTree(previous);
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
            StartTree(left.Range.Start);

            var op = Consume();
            var right = ParsePrimary();
            while (token.IsBinary && (token.Precedence > op.Precedence ||
                                      (token.Precedence == op.Precedence && token.RightAssociative)))
            {
                right = ParseExpression(right, op.Precedence + (token.Precedence > op.Precedence ? 1 : 0));
            }

            left = EndTree<Tree.Binary>(op switch
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
            });
        }

        return left;
    }

    private Tree.Table ParseTableConstructor()
    {
        StartTree();

        Expect(LCurly);

        var lastNumberIndex = 1;
        List<Tree.TableField> fields = [];

        while (!Accept<Token.RCurly>())
        {
            StartTree();

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
                key = ConsumeTree(new Tree.String(name.Value));
                NextToken(); // skip '='
            }
            // Just an expression, will be added at the last number index
            else
            {
                key = new Tree.Number(lastNumberIndex.ToString(), lastNumberIndex);
                lastNumberIndex++;
            }

            var value = ParseExpression();
            fields.Add(EndTree(new Tree.TableField(key, value)));

            if (!Accept<Token.Comma>() && !Accept<Token.Semicolon>())
            {
                Expect(RCurly);
                break;
            }
        }


        return EndTree(new Tree.Table(fields));
    }

    private Tree ParsePrimary()
    {
        // unop exp
        if (token.IsUnary)
        {
            StartTree();
            var op = Consume();
            var expression = ParsePrimary();
            return EndTree<Tree>(op switch
            {
                Token.Not => new Tree.Not(expression),
                Token.Length => new Tree.Length(expression),
                Token.Minus => new Tree.Negate(expression),
                _ => throw new Exception() // Unreachable.
            });
        }

        if (token is Token.Vararg)
        {
            return ConsumeTree(new Tree.Vararg());
        }

        if (token is Token.Nil)
        {
            return ConsumeTree(new Tree.Nil());
        }

        if (token is Token.False)
        {
            return ConsumeTree(new Tree.False());
        }

        if (token is Token.True)
        {
            return ConsumeTree(new Tree.True());
        }

        if (token is Token.Number numberToken)
        {
            return ConsumeTree(new Tree.Number(numberToken.Value, numberToken.NumberValue));
        }

        if (token is Token.String stringToken)
        {
            return ConsumeTree(new Tree.String(stringToken.Value));
        }

        if (token is Token.LongString longString)
        {
            return ConsumeTree(new Tree.LongString(longString.Value, longString.Level));
        }

        if (token is Token.LCurly)
        {
            return ParseTableConstructor();
        }

        if (token is Token.Function)
        {
            StartTree();
            NextToken(); // skip 'function'
            return EndTree(ParseFunctionBody(false));
        }

        return ParsePrefixExpression();
    }

    /// <summary>
    /// Parse this source's contents and return the file's syntax tree.
    /// </summary>
    public static (Tree.Block block, List<Diagnostic> diagnostics) ParseFile(Source source)
    {
        var parser = new Parser(source);
        var block = parser.ParseBlock();
        return (block, parser.Diagnostics);
    }
}