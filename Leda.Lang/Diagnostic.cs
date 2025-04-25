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
            Message = $"Expected {expected.KindName}, but got {got.Value}.";
        }
    }

    public class ExpectedExpressionButGotToken : Diagnostic
    {
        public ExpectedExpressionButGotToken(Source source, Token got) : base(source, got.Range)
        {
            Severity = DiagnosticSeverity.Error;
            Message = $"Expected an expression, but got {got.Value}.";
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