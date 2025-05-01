using System.Text;
using Leda.Lang;

namespace Leda;

/// <summary>
/// Given an AST, emits its corresponding Lua code as a string.
/// </summary>
public class Emitter
{
    private readonly StringBuilder builder = new();
    private readonly char indentChar = ' ';
    private readonly int indentSize = 2;

    private static readonly Dictionary<char, string> EscapeChars = new()
    {
        { '\a', @"\a" },
        { '\b', @"\b" },
        { '\f', @"\f" },
        { '\r', @"\r" },
        { '\t', @"\t" },
        { '\v', @"\v" },
        { '\\', @"\\" },
        { '\"', "\\\"" },
        { '\n', @"\n" },
    };

    private static bool IsSimpleKey(string name) =>
        name.Length > 0 && !char.IsDigit(name[0]) && name.All(Lexer.IsNameChar);

    private Emitter() { }

    private void Emit(string str)
    {
        builder.Append(str);
    }

    private void Emit(char c)
    {
        builder.Append(c);
    }

    private void Emit(char c, int count)
    {
        builder.Append(c, count);
    }

    private void EmitIndent(int level)
    {
        Emit(indentChar, indentSize * level);
    }

    private void EmitDeclarationList(List<Tree.Declaration> declarations)
    {
        for (var i = 0; i < declarations.Count; i++)
        {
            Emit(declarations[i].Name);
            if (i < declarations.Count - 1)
            {
                Emit(", ");
            }
        }
    }

    private void EmitExpression(Tree expression)
    {
        if (expression is Tree.Name name)
        {
            Emit(name.Value);
        }
        else if (expression is Tree.Number number)
        {
            Emit(number.Value);
        }
        else if (expression is Tree.True)
        {
            Emit("true");
        }
        else if (expression is Tree.False)
        {
            Emit("false");
        }
        else if (expression is Tree.String str)
        {
            Emit('"');
            foreach (var c in str.Value)
            {
                if (EscapeChars.TryGetValue(c, out var escaped))
                {
                    Emit(escaped);
                }
                else
                {
                    Emit(c);
                }
            }

            Emit('"');
        }
        else if (expression is Tree.LongString longString)
        {
            Emit('[');
            Emit('=', longString.Level);
            Emit('[');
            Emit(longString.Value);
            Emit(']');
            Emit('=', longString.Level);
            Emit(']');
        }
        else if (expression is Tree.Table table)
        {
            Emit('{');
            var lastNumberIndex = 1;
            for (var i = 0; i < table.Fields.Count; i++)
            {
                var field = table.Fields[i];
                if (field.Key is Tree.Number numberKey && numberKey.NumberValue == lastNumberIndex)
                {
                    EmitExpression(field.Value);
                    lastNumberIndex++;
                }
                else if (field.Key is Tree.String stringKey && IsSimpleKey(stringKey.Value))
                {
                    Emit(stringKey.Value);
                    Emit(" = ");
                    EmitExpression(field.Value);
                }
                else
                {
                    Emit('[');
                    EmitExpression(field.Key);
                    Emit("] = ");
                    EmitExpression(field.Value);
                }

                if (i < table.Fields.Count - 1)
                {
                    Emit(", ");
                }
            }

            Emit('}');
        }
        else if (expression is Tree.Access access)
        {
            EmitPrefixExpression(access.Target);
            if (access.Key is Tree.String stringKey && IsSimpleKey(stringKey.Value))
            {
                Emit('.');
                Emit(stringKey.Value);
            }
            else
            {
                Emit('[');
                EmitExpression(access.Key);
                Emit(']');
            }
        }
        else if (expression is Tree.Call call)
        {
            EmitCall(call);
        }
        else if (expression is Tree.Binary binary)
        {
            if (binary.Left is Tree.Binary leftBinary && leftBinary.Precedence < binary.Precedence)
            {
                Emit('(');
                EmitExpression(binary.Left);
                Emit(')');
            }
            else
            {
                EmitExpression(binary.Left);
            }

            Emit(' ');
            Emit(binary.Character);
            Emit(' ');

            if (binary.Right is Tree.Binary rightBinary && rightBinary.Precedence < binary.Precedence)
            {
                Emit('(');
                EmitExpression(binary.Right);
                Emit(')');
            }
            else
            {
                EmitExpression(binary.Right);
            }
        }
        else
        {
            throw new Exception();
        }
    }

    private void EmitExpressionList(List<Tree> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            EmitExpression(values[i]);
            if (i < values.Count - 1)
            {
                Emit(", ");
            }
        }
    }

    private void EmitPrefixExpression(Tree expression)
    {
        if (expression is not (Tree.Call or Tree.Access or Tree.Name))
        {
            Emit('(');
            EmitExpression(expression);
            Emit(')');
        }
        else
        {
            EmitExpression(expression);
        }
    }

    private void EmitCall(Tree.Call call)
    {
        EmitPrefixExpression(call.Target);
        Emit('(');
        EmitExpressionList(call.Parameters);
        Emit(')');
    }

    private void EmitStatement(Tree statement, int indent)
    {
        EmitIndent(indent);

        if (statement is Tree.LocalDeclaration localDeclaration)
        {
            Emit("local ");
            EmitDeclarationList(localDeclaration.Declarations);
            if (localDeclaration.Values.Count > 0)
            {
                Emit(" = ");
                EmitExpressionList(localDeclaration.Values);
            }
        }
        else if (statement is Tree.Call call)
        {
            EmitCall(call);
        }
        else if (statement is Tree.Return returnStatement)
        {
            Emit("return");
            if (returnStatement.Expression != null)
            {
                Emit(" ");
                EmitExpression(returnStatement.Expression);
            }
        }
        else if (statement is Tree.If ifStatement)
        {
            Emit("if ");
            EmitExpression(ifStatement.Primary.Condition);
            Emit(" then\n");
            EmitBlock(ifStatement.Primary.Body, indent + 1);
            EmitIndent(indent);

            foreach (var branch in ifStatement.ElseIfs)
            {
                Emit("elseif ");
                EmitExpression(branch.Condition);
                Emit(" then\n");
                EmitBlock(branch.Body, indent + 1);
                EmitIndent(indent);
            }

            if (ifStatement.ElseBody != null)
            {
                Emit("else\n");
                EmitBlock(ifStatement.ElseBody, indent + 1);
                EmitIndent(indent);
            }

            Emit("end");
        }
        else
        {
            throw new Exception();
        }

        Emit('\n');
    }

    private void EmitBlock(Tree.Block block, int indent)
    {
        foreach (var statement in block.Statements)
        {
            EmitStatement(statement, indent);
        }
    }

    public static string Emit(Tree.Block block)
    {
        var emitter = new Emitter();
        emitter.EmitBlock(block, 0);
        return emitter.builder.ToString();
    }
}