namespace Leda.Lang;

public abstract record Diagnostic(Source Source, Range Range)
{
    /// <summary>
    /// The severity of this diagnostic.
    /// </summary>
    public abstract DiagnosticSeverity Severity { get; }

    /// <summary>
    /// A human-readable message describing the problem.
    /// </summary>
    public abstract string Message { get; }

    public record MalformedNumber(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Malformed number.";
    }

    public record HexNumbersNotSupported(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

        public override string Message =>
            "Hex number literals with decimal points/exponents are not yet fully supported.";
    }

    public record InvalidEscapeSequence(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Invalid escape sequence.";
    }

    public record UnfinishedString(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished string.";
    }

    public record InvalidCharacter(Source Source, Range Range, char Character) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Invalid character. (Hex: {(int)Character:X})";
    }

    public record InvalidLongStringDelimiter(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Invalid long string delimiter.";
    }

    public record UnfinishedLongString(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished long string.";
    }

    public record UnfinishedLongComment(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Unfinished long comment.";
    }

    public record ExpectedTokenButGotToken(Source Source, Range Range, Token Expected, Token Got)
        : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected \"{Expected.KindName}\", but got \"{Got.Value}\".";
    }

    public record ExpectedExpressionButGotToken(Source Source, Range Range, Token Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected an expression, but got \"{Got.Value}\".";
    }

    public record DidNotExpectTokenHere(Source Source, Range Range, Token Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Did not expect \"{Got.Value}\" here.";
    }

    public record AmbiguousSyntax(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Ambiguous syntax. Prepend ';' or move characters to same line.";
    }

    public record CannotAssignToThis(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "This expression cannot be assigned to.";
    }

    public record NoImplicitGlobalFunction(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "Function is implicitly global. Prefix 'global' if this is intentional.";
    }

    public record NameNotFound(Source Source, Range Range, Tree.Name Name) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot find name '{Name.Value}'.";
    }

    public record ValueAlreadyDeclared(Source Source, Range Range, string Name, Symbol ExistingSymbol)
        : Diagnostic(Source, Range)
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

    public record TypeAlreadyDeclared(Source Source, Range Range, string Name) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"A type named '{Name}' has already been declared.";
    }

    public record CantGetLength(Source Source, Range Range, Type Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot get the length of a '{Got}' value.";
    }

    public record CantNegate(Source Source, Range Range, Type Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Cannot negate a '{Got}' value.";
    }

    public record ForLoopStartNotNumber(Source Source, Range Range, Type Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Starting value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopLimitNotNumber(Source Source, Range Range, Type Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Limit value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record ForLoopStepNotNumber(Source Source, Range Range, Type Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Step value of `for` loop must be 'number', but is '{Got}'.";
    }

    public record TypeNotAssignableToType(Source Source, Range Range, TypeMismatch Mismatch) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => Mismatch.ToString();
    }

    public record TypeNotCallable(Source Source, Range Range) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => "This expression is not callable.";
    }

    public record NotEnoughArguments(Source Source, Range Range, int Expected, int Got) : Diagnostic(Source, Range)
    {
        public override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        public override string Message => $"Expected {Expected} arguments, but got {Got}.";
    }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}