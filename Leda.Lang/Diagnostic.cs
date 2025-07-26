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

    public record ExpectedTokenButGotToken(Range Range, Token Expected, Token Got)
        : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected \"{Expected.KindName}\", but got \"{Got.Value}\".";
    }

    public record ExpectedExpressionButGotToken(Range Range, Token Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected an expression, but got \"{Got.Value}\".";
    }

    public record DidNotExpectTokenHere(Range Range, Token Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Did not expect \"{Got.Value}\" here.";
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

    public record NameNotFound(Range Range, Tree.Name Name) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot find name '{Name.Value}'.";
    }

    public record ValueAlreadyDeclared(Range Range, string Name, Symbol ExistingSymbol)
        : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;


        public override string Message
        {
            get
            {
                var noun = ExistingSymbol switch
                {
                    Symbol.LocalVariable => "local variable",
                    _ => "value"
                };
                return $"A {noun} named '{Name}' has already been declared.";
            }
        }
    }

    public record TypeAlreadyDeclared(Range Range, string Name) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"A type named '{Name}' has already been declared.";
    }

    public record CantGetLength(Range Range, Type Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot get the length of a '{Got}' value.";
    }

    public record CantNegate(Range Range, Type Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot negate a '{Got}' value.";
    }

    public record ForLoopStartNotNumber(Range Range, Type Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Starting value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopLimitNotNumber(Range Range, Type Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Limit value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopStepNotNumber(Range Range, Type Got) : Diagnostic(Range)
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

    public record NotEnoughArguments(Range Range, int Expected, int Got) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected {Expected} arguments, but got {Got}.";
    }

    public record TypeNotIndexable(Range Range, Type Type) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Type '{Type}' cannot be indexed.";
    }

    public record TypeDoesntHaveKey(Range Range, Type Target, Type Key) : Diagnostic(Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Type '{Target}' doesn't have key of type '{Key}'.";
    }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}