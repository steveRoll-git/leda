using System.Diagnostics;

namespace Leda.Lang;

/// <summary>
/// Parses the token stream from a `Lexer` into a syntax tree.
/// </summary>
public class Parser
{
    /// <summary>
    /// A context that the parser could be in.
    /// Stores the kinds of tokens that act as delimiters, and ones that close the context.
    /// Used mainly for deciding whether to skip a token on a syntax error.
    /// </summary>
    private class ParseContext
    {
        /// <summary>
        /// The token that this context starts with.
        /// </summary>
        internal TokenKind? Open { get; private init; }

        /// <summary>
        /// The token that this context ends with.
        /// </summary>
        internal TokenKind? Close { get; private init; }

        /// <summary>
        /// Tokens that delimit sibling tree nodes in this context.
        /// </summary>
        internal List<TokenKind> Delimiters { get; private init; } = [];

        /// <summary>
        /// Whether to allow one extra trailing delimiter at the end.
        /// </summary>
        internal bool AllowTrailingDelimiter { get; private init; }

        internal static readonly ParseContext Block = new()
        {
            Delimiters = [TokenKind.Semicolon],
        };

        internal static readonly ParseContext DoEnd = new()
        {
            Open = TokenKind.Do,
            Delimiters = [TokenKind.Semicolon],
            Close = TokenKind.End,
        };

        internal static readonly ParseContext IfThen = new()
        {
            Open = TokenKind.If,
            Close = TokenKind.Then,
        };

        internal static readonly ParseContext ElseifThen = new()
        {
            Open = TokenKind.Elseif,
            Close = TokenKind.Then,
        };

        internal static readonly ParseContext ThenEnd = new()
        {
            Open = TokenKind.Then,
            Delimiters = [TokenKind.Semicolon, TokenKind.Else, TokenKind.Elseif],
            Close = TokenKind.End,
        };

        internal static readonly ParseContext ElseEnd = new()
        {
            Open = TokenKind.Else,
            Delimiters = [TokenKind.Semicolon],
            Close = TokenKind.End,
        };

        internal static readonly ParseContext WhileDo = new()
        {
            Open = TokenKind.While,
            Close = TokenKind.Do,
        };

        internal static readonly ParseContext ForDo = new()
        {
            Open = TokenKind.For,
            Delimiters = [TokenKind.Comma],
            Close = TokenKind.Do,
        };

        internal static readonly ParseContext RepeatUntil = new()
        {
            Open = TokenKind.Repeat,
            Delimiters = [TokenKind.Semicolon],
            Close = TokenKind.Until,
        };

        internal static readonly ParseContext FunctionBody = new()
        {
            Delimiters = [TokenKind.Semicolon],
            Close = TokenKind.End,
        };

        internal static readonly ParseContext CommaSeparatedList = new()
        {
            Delimiters = [TokenKind.Comma],
        };

        internal static readonly ParseContext Parens = new()
        {
            Open = TokenKind.LParen,
            Close = TokenKind.RParen,
        };

        internal static readonly ParseContext SquareBrackets = new()
        {
            Open = TokenKind.LSquare,
            Close = TokenKind.RSquare,
        };

        internal static readonly ParseContext AngleBrackets = new()
        {
            Open = TokenKind.Less,
            Delimiters = [TokenKind.Comma],
            Close = TokenKind.Greater,
        };

        internal static readonly ParseContext TableConstructor = new()
        {
            Open = TokenKind.LCurly,
            Delimiters = [TokenKind.Comma, TokenKind.Semicolon],
            Close = TokenKind.RCurly,
            AllowTrailingDelimiter = true,
        };
    }

    private List<Diagnostic> Diagnostics { get; } = [];

    private readonly Lexer lexer;

    /// <summary>
    /// The current token on the stream.
    /// </summary>
    private Token token;

    /// <summary>
    /// The last token that was consumed.
    /// </summary>
    private Token? lastToken;

    /// <summary>
    /// Stores tokens that were received when Lookahead was called.
    /// </summary>
    private readonly List<Token> lookaheadTokens = [];

    private readonly List<ParseContext> parseContextStack = [];

    /// <summary>
    /// Stores the positions that were started with `StartTree`.
    /// </summary>
    private readonly Stack<(Position position, Position preSpacePosition)> startPositions = new();

    private record ChunkInfo(List<Tree.Statement.Return> ReturnStatements);

    private record FileInfo(List<Tree.Statement.GlobalDeclaration> GlobalDeclarations);

    private readonly Stack<ChunkInfo> chunkStack = new();

    private static bool IsAssignableTo(Tree.Expression tree)
    {
        return tree is Tree.Expression.Name or Tree.Expression.Access;
    }

    private Parser(Source source)
    {
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

    /// <summary>
    /// If the current token is of the given kind, consumes it and returns `true`, otherwise returns `false`.
    /// </summary>
    private bool Accept(TokenKind kind)
    {
        if (token.Kind == kind)
        {
            NextToken();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Keeps or consumes the current token, for when it causes a syntax error.
    /// </summary>
    private void HandleErrorToken()
    {
        if (!IsTokenDelimiter())
        {
            NextToken();
        }
    }

    /// <summary>
    /// Consumes and returns the current token. If it isn't of the given kind, reports an error.<br/>
    /// </summary>
    private Token Expect(TokenKind kind)
    {
        var got = token;
        if (got.Kind != kind)
        {
            Report(new Diagnostic.ExpectedToken(got.Range, kind));
            HandleErrorToken();
        }
        else
        {
            NextToken();
        }

        return got;
    }

    private bool AcceptOneOf(List<TokenKind> kinds)
    {
        foreach (var tokenKind in kinds)
        {
            if (token.Kind == tokenKind)
            {
                NextToken();
                return true;
            }
        }

        return false;
    }

    private void ReportUnexpectedToken()
    {
        if (token.Kind != TokenKind.Unknown)
        {
            Report(new Diagnostic.DidNotExpectTokenHere(token.Range, token.Kind));
        }

        HandleErrorToken();
    }

    /// <summary>
    /// Makes this position the start position of the currently parsed tree.
    /// </summary>
    private void StartTree(Position position, Position preSpacePosition)
    {
        startPositions.Push((position, preSpacePosition));
    }

    /// <summary>
    /// Sets the next tree's start position to the same one as an existing tree.
    /// Useful for when a new tree is composed of an already parsed tree inside of it.
    /// </summary>
    private void StartTree(Tree existingTree)
    {
        StartTree(existingTree.Range.Start, existingTree.FullRange.Start);
    }

    /// <summary>
    /// Makes the current `token` the beginning of the currently parsed tree's range.
    /// </summary>
    private void StartTree()
    {
        StartTree(token.Range.Start, lastToken?.Range.End ?? new(0, 0));
    }

    /// <summary>
    /// Takes the last start position set by `StartTree`, and sets the tree's range to that start position and the last
    /// token's end position.
    /// </summary>
    /// <returns>The same tree for convenience.</returns>
    private T EndTree<T>(T tree) where T : Tree
    {
        var (start, preSpaceStart) = startPositions.Pop();
        tree.Range = new(start, lastToken!.Range.End);
        tree.FullRange = new(preSpaceStart, token.Range.Start);
        return tree;
    }

    /// <summary>
    /// Consumes the current token, and sets the tree's range to that token's range.
    /// </summary>
    private T ConsumeTree<T>(T tree) where T : Tree
    {
        var previous = lastToken!.Range.End;
        tree.Range = Consume().Range;
        tree.FullRange = new(previous, token.Range.Start);
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

    /// <summary>
    /// Expects a Name token and creates a tree with its value.
    /// </summary>
    private T ExpectName<T>(Func<string, T> func) where T : Tree
    {
        StartTree();
        var name = Expect(TokenKind.Name);
        return EndTree(func(name.Value));
    }

    private Tree.Expression.Name ParseValueName()
    {
        return ExpectName(name => new Tree.Expression.Name(name));
    }

    private Tree.Type.Name ParseTypeName()
    {
        return ExpectName(name => new Tree.Type.Name(name));
    }

    private Tree.LabelName ParseLabelName()
    {
        return ExpectName(name => new Tree.LabelName(name));
    }

    /// <summary>
    /// Parses a single identifier that represents a string.
    /// </summary>
    private Tree.Expression.String ParseStringIdentifier()
    {
        return ExpectName(name => new Tree.Expression.String(name));
    }

    private Tree.Type.StringLiteral ParseTypeStringIdentifier()
    {
        return ExpectName(name => new Tree.Type.StringLiteral(name));
    }

    /// <summary>
    /// Starts the given parse context, and expects its opening token if it has one.
    /// </summary>
    private void StartContext(ParseContext parseContext)
    {
        parseContextStack.Add(parseContext);
        if (parseContext.Open is { } open)
        {
            Expect(open);
        }
    }

    /// <summary>
    /// Ends the current parse context, and expects its closing token if it has one.
    /// </summary>
    private void EndContext()
    {
        var parseContext = parseContextStack[^1];
        parseContextStack.RemoveAt(parseContextStack.Count - 1);
        if (parseContext.Close is { } close)
        {
            Expect(close);
        }
    }

    /// <summary>
    /// Starts a new parse context without expecting the previous context's close token. Used when starting a context
    /// whose close token is the same as the previous context's start token.
    /// </summary>
    private void ReplaceContext(ParseContext parseContext)
    {
        parseContextStack.RemoveAt(parseContextStack.Count - 1);
        StartContext(parseContext);
    }

    /// <summary>
    /// Returns whether the current token delimits or ends the given parse context.
    /// </summary>
    private bool IsTokenDelimiter(ParseContext parseContext)
    {
        return token.Kind == TokenKind.Eof || token.Kind == parseContext.Close || parseContext.Delimiters.Contains(token.Kind);
    }

    /// <summary>
    /// Returns whether the current token delimits or ends the current parse context, or the one before it.
    /// </summary>
    private bool IsTokenDelimiter()
    {
        return IsTokenDelimiter(parseContextStack[^1]) ||
               (parseContextStack.Count > 1 && IsTokenDelimiter(parseContextStack[^2]));
    }

    /// <summary>
    /// Parses a list of items that are delimited by the given context's delimiters.
    /// </summary>
    /// <param name="context">The context to parse in.</param>
    /// <param name="parseFunc">A function that parses a single item in the list.</param>
    /// <param name="allowEmpty">Whether to allow parsing an empty list.</param>
    private List<T> ParseDelimitedList<T>(ParseContext context, Func<T> parseFunc, bool allowEmpty) where T : Tree
    {
        List<T> items = [];

        StartContext(context);

        if (allowEmpty && (token.Kind == context.Close || IsTokenDelimiter(parseContextStack[^2])))
        {
            EndContext();
            return items;
        }

        do
        {
            items.Add(parseFunc());
        } while (AcceptOneOf(context.Delimiters) && (token.Kind != context.Close || !context.AllowTrailingDelimiter));

        EndContext();

        return items;
    }

    /// <summary>
    /// Parse a block of statements.
    /// </summary>
    private Tree.Block ParseBlock(FileInfo? fileInfo = null)
    {
        var statements = new List<Tree.Statement>();
        var typeDeclarations = new List<Tree.TypeAliasDeclaration>();
        var labels = new List<Tree.Statement.LabelDefinition>();

        while (!IsTokenDelimiter())
        {
            if (token is { Kind: TokenKind.Name, Value: "type" } && Lookahead(1).Kind == TokenKind.Name)
            {
                typeDeclarations.Add(ParseTypeAlias());
            }
            else if (token is { Kind: TokenKind.Name, Value: "global" } &&
                     Lookahead(1).Kind is TokenKind.Name or TokenKind.Function)
            {
                var globalDeclaration = ParseGlobalDeclaration();
                if (fileInfo != null)
                {
                    fileInfo.GlobalDeclarations.Add(globalDeclaration);
                }
                else
                {
                    Report(new Diagnostic.GlobalNotDeclaredInFile(globalDeclaration.Range));
                }

                statements.Add(globalDeclaration);
            }
            else
            {
                var statement = ParseStatement();
                statements.Add(statement);

                if (statement is Tree.Statement.LabelDefinition label)
                {
                    labels.Add(label);
                }

                // This behavior models Lua 5.1's syntax - in Lua 5.2+, semicolons may be their own statements.
                Accept(TokenKind.Semicolon);

                if (statement is Tree.Statement.Return or Tree.Statement.Break)
                {
                    // No more statements can come after `return` or `break`.
                    break;
                }
            }
        }

        return new Tree.Block(statements, typeDeclarations, labels);
    }

    private Tree.Chunk ParseChunk(FileInfo? fileInfo)
    {
        var chunkInfo = new ChunkInfo([]);

        chunkStack.Push(chunkInfo);
        var block = ParseBlock(fileInfo);
        chunkStack.Pop();

        return new Tree.Chunk(block.Statements, block.TypeDeclarations, block.Labels, chunkInfo.ReturnStatements);
    }

    private Tree.File ParseFile()
    {
        var fileInfo = new FileInfo([]);

        StartContext(ParseContext.Block);
        var chunk = ParseChunk(fileInfo);
        EndContext();

        return new Tree.File(chunk, fileInfo.GlobalDeclarations);
    }

    /// <summary>
    /// Parse a statement.
    /// </summary>
    private Tree.Statement ParseStatement()
    {
        if (token.Kind == TokenKind.Return)
        {
            return ParseReturn();
        }

        if (token.Kind == TokenKind.Break)
        {
            return ConsumeTree(new Tree.Statement.Break());
        }

        if (token.Kind == TokenKind.Do)
        {
            return ParseDo();
        }

        if (token.Kind == TokenKind.If)
        {
            return ParseIfStatement();
        }

        if (token.Kind == TokenKind.For)
        {
            return ParseForLoop();
        }

        if (token.Kind == TokenKind.While)
        {
            return ParseWhileLoop();
        }

        if (token.Kind == TokenKind.Repeat)
        {
            return ParseRepeatUntilLoop();
        }

        if (token.Kind == TokenKind.Local)
        {
            return ParseLocalDeclaration();
        }

        if (token.Kind == TokenKind.Function)
        {
            return ParseFunctionDeclaration();
        }

        if (token.Kind == TokenKind.ColonColon)
        {
            return ParseLabelDefinition();
        }

        if (token.Kind == TokenKind.Goto)
        {
            return ParseGoto();
        }

        StartTree();

        // Parse assignment or function call.
        var value = ParsePrefixExpression();

        if (value is Tree.Expression.Call call)
        {
            return EndTree(new Tree.Statement.Call(call));
        }

        if (value is Tree.Expression.MethodCall methodCall)
        {
            return EndTree(new Tree.Statement.MethodCall(methodCall));
        }

        if (value is Tree.Expression.Error)
        {
            return EndTree(new Tree.Statement.Error());
        }

        if (IsAssignableTo(value))
        {
            List<Tree.Expression> targets = [value];
            while (Accept(TokenKind.Comma))
            {
                var target = ParsePrefixExpression();
                if (!IsAssignableTo(target))
                {
                    Report(new Diagnostic.CannotAssignToThis(target.Range));
                    targets.Add(new Tree.Expression.Error());
                }
                else
                {
                    targets.Add(target);
                }
            }

            Expect(TokenKind.Equal);

            var values = ParseExpressionList(false);

            return EndTree(new Tree.Statement.Assignment(targets, values));
        }

        ReportUnexpectedToken();

        return EndTree(new Tree.Statement.Error());
    }

    private Tree.Statement.Return ParseReturn()
    {
        StartTree();

        // 'return' [explist]
        Expect(TokenKind.Return);

        var returnStatement = new Tree.Statement.Return(ParseExpressionList(true));
        chunkStack.Peek().ReturnStatements.Add(returnStatement);

        return EndTree(returnStatement);
    }

    private Tree.Statement.Do ParseDo()
    {
        StartTree();

        // do 'block' end
        StartContext(ParseContext.DoEnd);
        var block = ParseBlock();
        EndContext();

        return EndTree(new Tree.Statement.Do(block));
    }

    /// <summary>
    /// Parse an if statement, along with any elseifs and a final else.
    /// </summary>
    private Tree.Statement.If ParseIfStatement()
    {
        Tree.IfBranch primary;

        StartTree();

        // 'if' exp 'then' block
        {
            StartContext(ParseContext.IfThen);
            var condition = ParseExpression();
            ReplaceContext(ParseContext.ThenEnd);
            var body = ParseBlock();
            primary = new Tree.IfBranch(condition, body);
        }

        List<Tree.IfBranch> elseIfs = [];

        // 'elseif' exp 'then' block
        while (token.Kind == TokenKind.Elseif)
        {
            ReplaceContext(ParseContext.ElseifThen);
            var condition = ParseExpression();
            ReplaceContext(ParseContext.ThenEnd);
            var body = ParseBlock();
            elseIfs.Add(new Tree.IfBranch(condition, body));
        }

        Tree.Block? elseBody = null;

        // 'else' block
        if (token.Kind == TokenKind.Else)
        {
            ReplaceContext(ParseContext.ElseEnd);
            elseBody = ParseBlock();
        }

        EndContext();

        return EndTree(new Tree.Statement.If(primary, elseIfs, elseBody));
    }

    private Tree.Statement ParseForLoop()
    {
        StartTree();

        StartContext(ParseContext.ForDo);

        // 'for' name '=' exp ',' exp [',' exp] 'do' block 'end'
        if (Lookahead(1).Kind == TokenKind.Equal)
        {
            var counter = ParseValueName();
            Expect(TokenKind.Equal);
            var start = ParseExpression();
            Expect(TokenKind.Comma);
            var end = ParseExpression();

            Tree.Expression? step = null;
            if (Accept(TokenKind.Comma))
            {
                step = ParseExpression();
            }

            ReplaceContext(ParseContext.DoEnd);
            var body = ParseBlock();
            EndContext();

            return EndTree(new Tree.Statement.NumericalFor(counter, start, end, step, body));
        }

        // 'for' declarations 'in' exp 'do' block 'end'
        {
            var variables = ParseNameList();
            Expect(TokenKind.In);
            var iterator = ParseExpressionList(false);

            ReplaceContext(ParseContext.DoEnd);
            var body = ParseBlock();
            EndContext();

            return EndTree(new Tree.Statement.IteratorFor(variables, iterator, body));
        }
    }

    private Tree.Statement.While ParseWhileLoop()
    {
        StartTree();

        // 'while' exp 'do' block 'end'
        StartContext(ParseContext.WhileDo);
        var condition = ParseExpression();
        ReplaceContext(ParseContext.DoEnd);
        var body = ParseBlock();
        EndContext();

        return EndTree(new Tree.Statement.While(condition, body));
    }

    private Tree.Statement.RepeatUntil ParseRepeatUntilLoop()
    {
        StartTree();

        // 'repeat' block 'until' exp
        StartContext(ParseContext.RepeatUntil);
        var body = ParseBlock();
        EndContext();

        var condition = ParseExpression();
        return EndTree(new Tree.Statement.RepeatUntil(body, condition));
    }

    /// <summary>
    /// Parses the declaration of a variable or parameter.
    /// </summary>
    private Tree.Declaration ParseDeclaration()
    {
        StartTree();

        // name [':' type]
        var name = ParseValueName();

        Tree.Type? type = null;
        if (Accept(TokenKind.Colon))
        {
            type = ParseType();
        }

        return EndTree(new Tree.Declaration(name, type));
    }

    private Tree.Type ParseType()
    {
        Tree.Type type;

        if (token.Kind == TokenKind.Nil)
        {
            type = ConsumeTree(new Tree.Type.Name("nil"));
        }
        else if (token is { Kind: TokenKind.String } str)
        {
            type = ConsumeTree(new Tree.Type.StringLiteral(str.Value));
        }
        else if (token.Kind == TokenKind.Function)
        {
            StartTree();
            NextToken(); // skip 'function'
            if (token.Kind == TokenKind.LParen)
            {
                type = EndTree(ParseFunctionType());
            }
            else
            {
                type = EndTree(new Tree.Type.Name("function"));
            }
        }
        else if (token.Kind == TokenKind.LCurly)
        {
            type = ParseTableType();
        }
        else
        {
            StartTree();
            var name = ParseTypeName();
            if (token.Kind == TokenKind.Less && ParseTypeArgumentList() is { } typeArguments)
            {
                type = EndTree(new Tree.Type.Instantiation(name, typeArguments));
            }
            else
            {
                type = EndTree(name);
            }
        }

        return ParseTypePostfix(type);
    }

    private Tree.Type ParseTypePostfix(Tree.Type prev)
    {
        StartTree(prev);

        // type '?'
        if (Accept(TokenKind.QuestionMark))
        {
            return ParseTypePostfix(EndTree(new Tree.Type.Nillable(prev)));
        }

        // type '[' ']'
        if (Accept(TokenKind.LSquare))
        {
            Expect(TokenKind.RSquare);
            return ParseTypePostfix(EndTree(new Tree.Type.Array(prev)));
        }

        return EndTree(prev);
    }

    private Tree.Type.Table.Field ParseTableTypeField()
    {
        // (name | '[' type ']') ['?'] ':' type

        StartTree();

        Tree.Type key;
        if (token.Kind == TokenKind.LSquare)
        {
            StartContext(ParseContext.SquareBrackets);
            key = ParseType();
            EndContext();
        }
        else
        {
            key = ParseTypeStringIdentifier();
        }

        var optional = Accept(TokenKind.QuestionMark);

        Expect(TokenKind.Colon);

        var value = ParseType();

        return EndTree(new Tree.Type.Table.Field(key, value, optional));
    }

    private Tree.Type.Table ParseTableType()
    {
        // '{' [typepair {',' typepair}] '}'

        StartTree();

        var fields = ParseDelimitedList(ParseContext.TableConstructor, ParseTableTypeField, true);

        return EndTree(new Tree.Type.Table(fields));
    }

    private List<Tree.Type> ParseTypeList(bool allowEmpty)
    {
        return ParseDelimitedList(ParseContext.CommaSeparatedList, ParseType, allowEmpty);
    }

    private List<Tree.Type>? ParseTypeArgumentList()
    {
        var less = token;
        StartContext(ParseContext.AngleBrackets);

        var list = ParseTypeList(true);

        var greater = token;
        EndContext();

        if (list.Count == 0)
        {
            Report(new Diagnostic.EmptyTypeParameterList(less.Range.Union(greater.Range)));
            return null;
        }

        return list;
    }

    private List<Tree.Expression.Name> ParseNameList()
    {
        return ParseDelimitedList(ParseContext.CommaSeparatedList, ParseValueName, false);
    }

    private List<Tree.Declaration> ParseDeclarationList(bool allowEmpty)
    {
        return ParseDelimitedList(ParseContext.CommaSeparatedList, ParseDeclaration, allowEmpty);
    }

    private Tree.Statement ParseLocalDeclaration()
    {
        StartTree();

        Expect(TokenKind.Local);

        if (Accept(TokenKind.Function))
        {
            // 'local' 'function' name funcbody
            var name = ParseValueName();
            var function = ParseFunctionBody(name.Range, false);
            return EndTree(new Tree.Statement.LocalFunctionDeclaration(name, function));
        }

        // 'local' declaration {',' declaration} ['=' explist]
        var declarations = ParseDeclarationList(false);

        List<Tree.Expression> values = [];
        if (Accept(TokenKind.Equal))
        {
            values = ParseExpressionList(false);
        }

        return EndTree(new Tree.Statement.LocalDeclaration(declarations, values));
    }

    private Tree.Statement.GlobalDeclaration ParseGlobalDeclaration()
    {
        StartTree();

        Expect(TokenKind.Name); // The token expected here is `global`, but this is already caught by `ParseBlock`.

        if (Accept(TokenKind.Function))
        {
            // 'global' 'function' name funcbody
            var name = ParseValueName();
            var function = ParseFunctionBody(name.Range, false);
            // A global function declaration is equivalent to a global declaration that assigns a function to one
            // variable.
            return EndTree(new Tree.Statement.GlobalDeclaration([new(name, null)], [function]));
        }

        // 'global' declaration {',' declaration} ['=' explist]
        var declarations = ParseDeclarationList(false);

        List<Tree.Expression> values = [];
        if (Accept(TokenKind.Equal))
        {
            values = ParseExpressionList(false);
        }

        return EndTree(new Tree.Statement.GlobalDeclaration(declarations, values));
    }

    private Tree.Statement.Assignment ParseFunctionDeclaration()
    {
        StartTree();

        // 'function' name {'.' name} [':' name]
        Expect(TokenKind.Function);

        Tree.Expression path = ParseValueName();
        var isMethod = false;
        while (token.Kind is TokenKind.Dot or TokenKind.Colon)
        {
            StartTree(path);

            var separator = Consume();
            var nextName = ParseStringIdentifier();

            path = EndTree(new Tree.Expression.Access(path, nextName));

            if (separator.Kind == TokenKind.Colon)
            {
                isMethod = true;
                break;
            }
        }

        var function = ParseFunctionBody(path.Range, isMethod);

        return EndTree(new Tree.Statement.Assignment([path], [function]));
    }

    private Tree.Statement.LabelDefinition ParseLabelDefinition()
    {
        // '::' name '::'
        StartTree();

        Expect(TokenKind.ColonColon);
        var name = ParseLabelName();
        Expect(TokenKind.ColonColon);

        return EndTree(new Tree.Statement.LabelDefinition(name));
    }

    private Tree.Statement.Goto ParseGoto()
    {
        // 'goto' name
        StartTree();

        Expect(TokenKind.Goto);
        var name = ParseLabelName();
        return EndTree(new Tree.Statement.Goto(name));
    }

    private List<Tree.Type.Name> ParseTypeParameterList()
    {
        var less = token;
        StartContext(ParseContext.AngleBrackets);

        var list = ParseDelimitedList(ParseContext.CommaSeparatedList, ParseTypeName, true);

        var greater = token;
        EndContext();

        if (list.Count == 0)
        {
            Report(new Diagnostic.EmptyTypeParameterList(less.Range.Union(greater.Range)));
        }

        return list;
    }

    private Tree.Parameter ParseParameter()
    {
        StartTree();

        // name ['?'] [':' type]
        var name = ParseValueName();

        var optional = Accept(TokenKind.QuestionMark);

        Tree.Type? type = null;
        if (Accept(TokenKind.Colon))
        {
            type = ParseType();
        }

        return EndTree(new Tree.Parameter(name, type, optional));
    }

    private List<Tree.Parameter> ParseParameterList()
    {
        return ParseDelimitedList(ParseContext.CommaSeparatedList, ParseParameter, true);
    }

    /// <summary>
    /// Parses the parameters and return type of a function.
    /// </summary>
    private Tree.Type.Function ParseFunctionType(bool isMethod = false)
    {
        StartTree();

        // ['<' typeparams '>'] '(' paramlist ')' [':' typelist]

        List<Tree.Type.Name>? typeParameters = null;
        if (token.Kind == TokenKind.Less)
        {
            typeParameters = ParseTypeParameterList();
        }

        if (isMethod)
        {
            // TODO 
        }

        StartContext(ParseContext.Parens);
        var parameters = ParseParameterList();
        EndContext();

        List<Tree.Type>? returnTypes = null;
        if (Accept(TokenKind.Colon))
        {
            returnTypes = ParseTypeList(false);
        }

        return EndTree(new Tree.Type.Function(parameters, returnTypes, typeParameters));
    }

    /// <summary>
    /// Parses a function body - used in function declarations and in anonymous functions.
    /// </summary>
    private Tree.Expression.Function ParseFunctionBody(Range nameRange, bool isMethod)
    {
        StartTree();

        // functiontype block 'end'
        var functionType = ParseFunctionType(isMethod);

        StartContext(ParseContext.FunctionBody);
        var chunk = ParseChunk(null);
        EndContext();

        return EndTree(new Tree.Expression.Function(functionType, chunk, nameRange, isMethod));
    }

    /// <summary>
    /// Parses either an access, or a function call.
    /// </summary>
    private Tree.Expression ParsePrefixExpression()
    {
        StartTree();

        // '(' exp ')'
        if (token.Kind == TokenKind.LParen)
        {
            StartContext(ParseContext.Parens);
            var expression = ParseExpression();
            EndContext();
            return ParsePrefixExpression(EndTree(expression));
        }

        if (token.Kind == TokenKind.Name)
        {
            var name = Consume();
            return ParsePrefixExpression(EndTree(new Tree.Expression.Name(name.Value)));
        }

        ReportUnexpectedToken();

        return EndTree(new Tree.Expression.Error());
    }

    private Tree.Expression ParsePrefixExpression(Tree.Expression previous)
    {
        StartTree(previous);

        if (Accept(TokenKind.Dot))
        {
            if (token.Kind == TokenKind.Less)
            {
                // Function call with type arguments: '.' '<' typelist '>' '(' [explist] ')'
                var typeArguments = ParseTypeArgumentList();
                return ParsePrefixExpression(EndTree(ParseCall(previous, typeArguments)));
            }

            // Access with a dot: '.' Name
            var name = ParseStringIdentifier();
            return ParsePrefixExpression(EndTree(new Tree.Expression.Access(previous, name)));
        }

        // Method call: ':' Name  '(' [explist] ')'
        if (Accept(TokenKind.Colon))
        {
            var funcName = ParseStringIdentifier();
            StartContext(ParseContext.Parens);

            // TODO parse optional type parameters

            var arguments = ParseExpressionList(true);
            EndContext();
            return ParsePrefixExpression(EndTree(new Tree.Expression.MethodCall(previous, funcName, arguments)));
        }

        // Access with square brackets: '[' exp ']'
        if (token.Kind == TokenKind.LSquare)
        {
            StartContext(ParseContext.SquareBrackets);
            var expression = ParseExpression();
            EndContext();
            return ParsePrefixExpression(EndTree(new Tree.Expression.Access(previous, expression)));
        }

        if (token.Kind == TokenKind.LParen)
        {
            return ParsePrefixExpression(EndTree(ParseCall(previous, null)));
        }

        return EndTree(previous);
    }

    /// <summary>
    /// Parses a function call. Expects to begin on the left paren.
    ///
    /// Unlike other parsing functions, expects the caller to start and end the tree.
    /// </summary>
    private Tree.Expression.Call ParseCall(Tree.Expression previous, List<Tree.Type>? typeArguments)
    {
        // Function call: '(' [explist] ')'

        var lParen = token;

        // If the '(' is on a new line, it could be a new statement that starts with it - report ambiguous syntax.
        if (previous.Range.End.Line < lParen.Range.Start.Line)
        {
            Report(new Diagnostic.AmbiguousSyntax(lParen.Range));
        }

        StartContext(ParseContext.Parens);
        var arguments = ParseExpressionList(true);
        var rParen = token;
        EndContext();

        return new Tree.Expression.Call(previous, arguments, typeArguments, new(lParen.Range.End, rParen.Range.Start));
    }

    /// <summary>
    /// Parse a list of expressions separated by commas.
    /// </summary>
    private List<Tree.Expression> ParseExpressionList(bool allowEmpty)
    {
        // {exp ','} exp
        return ParseDelimitedList(ParseContext.CommaSeparatedList, ParseExpression, allowEmpty);
    }

    /// <summary>
    /// Parse an expression with binary operators.
    /// </summary>
    private Tree.Expression ParseExpression()
    {
        return ParseExpression(ParsePrimary(), 0);
    }

    /// <summary>
    /// Parse an expression using precedence climbing.<br/>
    /// https://en.wikipedia.org/wiki/Operator-precedence_parser
    /// </summary>
    private Tree.Expression ParseExpression(Tree.Expression left, int minPrecedence)
    {
        while (Token.IsBinary(token, out var opPrecedence) && opPrecedence >= minPrecedence)
        {
            StartTree(left);

            var op = Consume();
            var right = ParsePrimary();
            while (Token.IsBinary(token, out var tokenPrecedence) && (tokenPrecedence > opPrecedence ||
                                                                      (tokenPrecedence == opPrecedence &&
                                                                       Token.IsRightAssociative(token))))
            {
                right = ParseExpression(right, opPrecedence + (tokenPrecedence > opPrecedence ? 1 : 0));
            }

            left = EndTree(new Tree.Expression.Binary(left, right, op));
        }

        return left;
    }

    private Tree.Expression.Table ParseTableConstructor()
    {
        StartTree();

        var lastNumberIndex = 1;
        var fields = ParseDelimitedList(ParseContext.TableConstructor, () =>
        {
            StartTree();

            var isListElement = false;

            Tree.Expression key;
            // '[' exp ']' '=' exp
            if (token.Kind == TokenKind.LSquare)
            {
                StartContext(ParseContext.SquareBrackets);
                key = ParseExpression();
                EndContext();
                Expect(TokenKind.Equal);
            }
            // name '=' exp
            else if (token.Kind == TokenKind.Name && Lookahead(1).Kind == TokenKind.Equal)
            {
                key = ConsumeTree(new Tree.Expression.String(token.Value));
                NextToken(); // skip '='
            }
            // Just an expression, will be added at the last number index
            else
            {
                key = new Tree.Expression.Number(lastNumberIndex.ToString(), lastNumberIndex);
                lastNumberIndex++;
                isListElement = true;
            }

            var value = ParseExpression();

            if (isListElement)
            {
                // Errors regarding the key will highlight the value.
                key.Range = value.Range;
            }

            return EndTree(new Tree.Expression.Table.Field(key, value));
        }, true);

        return EndTree(new Tree.Expression.Table(fields, fields.Count > 0 && lastNumberIndex - 1 == fields.Count));
    }

    private Tree.Expression ParsePrimary()
    {
        // unop exp
        if (Token.IsUnary(token))
        {
            StartTree();
            var op = Consume();
            var expression = ParsePrimary();
            return EndTree(new Tree.Expression.Unary(expression, op));
        }

        if (token.Kind == TokenKind.DotDotDot)
        {
            return ConsumeTree(new Tree.Expression.Vararg());
        }

        if (token.Kind == TokenKind.Nil)
        {
            return ConsumeTree(new Tree.Expression.Nil());
        }

        if (token.Kind == TokenKind.False)
        {
            return ConsumeTree(new Tree.Expression.False());
        }

        if (token.Kind == TokenKind.True)
        {
            return ConsumeTree(new Tree.Expression.True());
        }

        if (token is Token.Number numberToken)
        {
            return ConsumeTree(new Tree.Expression.Number(numberToken.Value, numberToken.NumberValue));
        }

        if (token.Kind == TokenKind.String)
        {
            return ConsumeTree(new Tree.Expression.String(token.Value));
        }

        if (token is Token.LongString longString)
        {
            return ConsumeTree(new Tree.Expression.LongString(longString.Value, longString.Level));
        }

        if (token.Kind == TokenKind.LCurly)
        {
            return ParseTableConstructor();
        }

        if (token.Kind == TokenKind.Function)
        {
            var functionToken = token;
            StartTree();
            NextToken(); // skip 'function'
            return EndTree(ParseFunctionBody(functionToken.Range, false));
        }

        return ParsePrefixExpression();
    }

    private Tree.TypeAliasDeclaration ParseTypeAlias()
    {
        // 'type' name '=' type

        StartTree();

        Expect(TokenKind.Name); // The token expected here is `type`, but this is already caught by `ParseBlock`.

        var name = ParseTypeName();

        List<Tree.Type.Name> typeParameters = [];
        if (token.Kind == TokenKind.Less)
        {
            typeParameters = ParseTypeParameterList();
        }

        Expect(TokenKind.Equal);

        var type = ParseType();

        return EndTree(new Tree.TypeAliasDeclaration(name, typeParameters, type));
    }

    /// <summary>
    /// Parse this source's contents and return the file's syntax tree.
    /// </summary>
    public static (Tree.File file, List<Diagnostic> diagnostics) ParseFile(Source source)
    {
        var parser = new Parser(source);
        var file = parser.ParseFile();
        Debug.Assert(parser.chunkStack.Count == 0);
        Debug.Assert(parser.parseContextStack.Count == 0);
        return (file, parser.Diagnostics);
    }
}