namespace Leda.Lang;

public class Diagnostic
{
    /// <summary>
    /// The source where the diagnostic applies.
    /// </summary>
    public Source Source { get; init; }

    /// <summary>
    /// The range where the diagnostic applies.
    /// </summary>
    public Range Range { get; }

    /// <summary>
    /// The severity of this diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }

    /// <summary>
    /// A human-readable message describing the problem.
    /// </summary>
    public string Message { get; init; } = "";

    public Diagnostic(Source source, Range range)
    {
        Source = source;
        Range = range;
    }

    public class MalformedNumber : Diagnostic
    {
        public MalformedNumber(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Malformed number.";
        }
    }

    public class HexNumbersNotSupported : Diagnostic
    {
        public HexNumbersNotSupported(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Warning;
            Message = "Hex number literals with decimal points/exponents are not yet fully supported.";
        }
    }

    public class InvalidEscapeSequence : Diagnostic
    {
        public InvalidEscapeSequence(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Invalid escape sequence.";
        }
    }

    public class UnfinishedString : Diagnostic
    {
        public UnfinishedString(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Unfinished string.";
        }
    }

    public class InvalidCharacter : Diagnostic
    {
        public InvalidCharacter(Source source, Range range, char character) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Invalid character. (Hex: {(int)character:X})";
        }
    }

    public class InvalidLongStringDelimiter : Diagnostic
    {
        public InvalidLongStringDelimiter(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Invalid long string delimiter.";
        }
    }

    public class UnfinishedLongString : Diagnostic
    {
        public UnfinishedLongString(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Unfinished long string.";
        }
    }

    public class UnfinishedLongComment : Diagnostic
    {
        public UnfinishedLongComment(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Unfinished long comment.";
        }
    }

    public class ExpectedTokenButGotToken : Diagnostic
    {
        public ExpectedTokenButGotToken(Source source, Token expected, Token got) : base(source, got.Range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Expected \"{expected.KindName}\", but got \"{got.Value}\".";
        }
    }

    public class ExpectedExpressionButGotToken : Diagnostic
    {
        public ExpectedExpressionButGotToken(Source source, Token got) : base(source, got.Range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Expected an expression, but got \"{got.Value}\".";
        }
    }

    public class DidNotExpectTokenHere : Diagnostic
    {
        public DidNotExpectTokenHere(Source source, Token got) : base(source, got.Range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Did not expect \"{got.Value}\" here.";
        }
    }

    public class AmbiguousSyntax : Diagnostic
    {
        public AmbiguousSyntax(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Ambiguous syntax. Prepend ';' or move characters to same line.";
        }
    }

    public class CannotAssignToThis : Diagnostic
    {
        public CannotAssignToThis(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "This expression cannot be assigned to.";
        }
    }

    public class NoImplicitGlobalFunction : Diagnostic
    {
        public NoImplicitGlobalFunction(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "Function is implicitly global. Prefix 'global' if this is intentional.";
        }
    }

    public class NameNotFound : Diagnostic
    {
        public NameNotFound(Source source, Tree.Name name) : base(source, name.Range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Cannot find name '{name.Value}'.";
        }
    }

    public class ValueAlreadyDeclared : Diagnostic
    {
        public ValueAlreadyDeclared(Source source, Range range, string name, Symbol existingSymbol) : base(source,
            range)
        {
            Severity = DiagnosticSeverity.Error;

            var noun = existingSymbol switch
            {
                Symbol.LocalVariable => "local variable",
                _ => "value"
            };
            Message = $"A {noun} named '{name}' has already been declared.";
        }
    }

    public class TypeAlreadyDeclared : Diagnostic
    {
        public TypeAlreadyDeclared(Source source, Range range, string name) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"A type named '{name}' has already been declared.";
        }
    }

    public class CantGetLength : Diagnostic
    {
        public CantGetLength(Source source, Range range, Type got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Cannot get the length of a '{got}' value.";
        }
    }

    public class CantNegate : Diagnostic
    {
        public CantNegate(Source source, Range range, Type got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Cannot negate a '{got}' value.";
        }
    }

    public class ForLoopStartNotNumber : Diagnostic
    {
        public ForLoopStartNotNumber(Source source, Range range, Type got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Starting value of `for` loop must be 'number', but is '{got}'.";
        }
    }

    public class ForLoopLimitNotNumber : Diagnostic
    {
        public ForLoopLimitNotNumber(Source source, Range range, Type got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Limit value of `for` loop must be 'number', but is '{got}'.";
        }
    }

    public class ForLoopStepNotNumber : Diagnostic
    {
        public ForLoopStepNotNumber(Source source, Range range, Type got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Step value of `for` loop must be 'number', but is '{got}'.";
        }
    }

    public class TypeNotAssignableToType : Diagnostic
    {
        public TypeNotAssignableToType(Source source, Range range, TypeMismatch mismatch) :
            base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = mismatch.ToString();
        }
    }

    public class TypeNotCallable : Diagnostic
    {
        public TypeNotCallable(Source source, Range range) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = "This expression is not callable.";
        }
    }

    public class NotEnoughArguments : Diagnostic
    {
        public NotEnoughArguments(Source source, Range range, int expected, int got) : base(source, range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Expected {expected} arguments, but got {got}.";
        }
    }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Information,
    Hint
}