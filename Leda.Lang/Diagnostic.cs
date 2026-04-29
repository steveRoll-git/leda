namespace Leda.Lang;

public abstract record Diagnostic(Range Range)
{
    /// <summary>
    /// The severity of this diagnostic.
    /// </summary>
    public abstract DiagnosticSeverity Severity { get; }

    /// <summary>
    /// A human-readable message describing the problem.
    /// </summary>
    public abstract string Message { get; }

    /// <summary>
    /// Whether the language server should mark this diagnostic with the "unnecessary" tag (appears faded out in most
    /// editors.)
    /// </summary>
    public virtual bool Unnecessary => false;

    private static string NameContextNoun(Tree.NameContext context) => context switch
    {
        Tree.NameContext.Value => "value",
        Tree.NameContext.Type => "type",
        Tree.NameContext.Label => "label",
        _ => throw new ArgumentOutOfRangeException(nameof(context))
    };

    public record MalformedNumber(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Malformed number.";
    }

    public record HexNumbersNotSupported(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

        public override string Message =>
            "Hex number literals with decimal points/exponents are not yet fully supported.";
    }

    public record InvalidEscapeSequence(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Invalid escape sequence.";
    }

    public record UnfinishedString(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished string.";
    }

    public record InvalidCharacter(Range Range, char Character) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Invalid character. (Hex: {(int)Character:X})";
    }

    public record InvalidLongStringDelimiter(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Invalid long string delimiter.";
    }

    public record UnfinishedLongString(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished long string.";
    }

    public record UnfinishedLongComment(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished long comment.";
    }

    public record ExpectedToken(Range Range, TokenKind Expected) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected {Token.GetKindName(Expected)}.";
    }

    public record DidNotExpectTokenHere(Range Range, TokenKind Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Did not expect {Token.GetKindName(Got)} here.";
    }

    public record AmbiguousSyntax(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Ambiguous syntax. Prepend ';' or move characters to same line.";
    }

    public record CannotAssignToThis(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "This expression cannot be assigned to.";
    }

    public record NoImplicitGlobalFunction(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Function is implicitly global. Prefix 'global' if this is intentional.";
    }

    public record NameNotFound(Range Range, string Name, Tree.NameContext Context) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        public override string Message => $"Cannot find {NameContextNoun(Context)} named '{Name}'.";
    }

    public record NameAlreadyDeclared(Range Range, Tree.NameContext Context, string Name) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        public override string Message => $"A {NameContextNoun(Context)} named '{Name}' has already been declared.";
    }

    public record BreakOutsideOfLoop(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "`break` cannot be used outside of loops.";
    }

    public record CantGetLength(Range Range, string Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot get the length of a '{Got}' value.";
    }

    public record CantNegate(Range Range, string Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot negate a '{Got}' value.";
    }

    public record ForLoopStartNotNumber(Range Range, string Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Starting value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopLimitNotNumber(Range Range, string Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Limit value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopStepNotNumber(Range Range, string Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Step value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record TypeMismatch(Range Range, Lang.TypeMismatch Mismatch) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => Mismatch.ToString();
    }

    public record TypeNotCallable(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "This expression is not callable.";
    }

    public record TypeNotIndexable(Range Range, string Type) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Type '{Type}' cannot be indexed.";
    }

    public record TypeDoesntHaveKey(Range Range, string Target, string Key) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Type '{Target}' doesn't have key of type '{Key}'.";
    }

    public record TableLiteralOnlyKnownKeys(Range Range, string Target, string Key) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        public override string Message =>
            $"Table literal may only specify known keys, and '{Key}' does not exist in type '{Target}'.";
    }

    public record MissingStringKeys(Range Range, string Target, string Source, List<string> Keys) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        public override string Message => Keys.Count == 1
            ? $"Key \"{Keys[0]}\" is missing in type '{Source}' but required in type '{Target}'."
            : $"Type '{Source}' is missing the following keys from type '{Target}': {string.Join(", ", Keys.Select(k => $"\"{k}\""))}";
    }

    public record ImplicitAnyType(Range Range, string Name) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Parameter '{Name}' implicitly has 'any' type.";
    }

    public record EmptyTypeParameterList(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Type parameter list cannot be empty.";
    }

    public record ValueNotAssigned(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;
        public override string Message => "This value is not assigned to any variable.";
    }

    public record TooManyValues(Range Range, TypeListKind Kind, int Maximum, int Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected at most {Maximum} {TypeList.ItemNoun(Kind)}s, but got {Got}.";
    }

    public record FunctionDoesntReturnValue(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Functions with a return type annotation must return a value.";
    }

    public record NotAllPathsReturn(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Not all code paths return a value.";
    }

    public record UnreachableCode(Range Range) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;
        public override string Message => "Unreachable code.";
        public override bool Unnecessary => true;
    }

    public record BinaryOperatorCantBeUsed(Range Range, TokenKind Operator, string Left, string Right)
        : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        public override string Message =>
            $"Operator {Token.GetKindName(Operator)} cannot be used on types '{Left}' and '{Right}'.";
    }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}