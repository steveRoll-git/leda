namespace Leda.Lang;

/// <summary>
/// A node in an untyped abstract syntax tree.
/// </summary>
public abstract class Tree
{
    /// <summary>
    /// The range in the source code that this tree's text occupies.
    /// </summary>
    public Range Range { get; internal set; }

    /// <summary>
    /// The range that is occupied by this tree's text along with leading and trailing whitespace.<br/>
    /// It is useful only if this tree is delimited by tokens that do not belong to any node, e.g. call arguments.
    /// </summary>
    public Range FullRange { get; internal set; }

    /// <summary>
    /// The FlowNode that this tree node is executed on. Initialized by the Binder.
    /// </summary>
    public FlowNode? FlowNode { get; internal set; }

    /// <summary>
    /// A binding to a local name or type.
    /// Initialized by the Binder.
    /// </summary>
    internal Symbol? LocalBinding { get; set; }

    /// <summary>
    /// A tree that defines a type.
    /// </summary>
    public abstract class Type : Tree
    {
        /// <summary>
        /// A named reference to a type.
        /// </summary>
        public class Name(string value) : Type
        {
            public string Value { get; } = value;

            public override string ToString()
            {
                return Value;
            }
        }

        public class StringLiteral(string value) : Type
        {
            public string Value { get; } = value;
        }

        public class NumberLiteral(double value) : Type
        {
            public double Value { get; } = value;
        }

        /// <summary>
        /// A list of key-value pairs of types.
        /// </summary>
        public class Table(List<Table.Field> fields) : Type
        {
            public List<Field> Fields { get; } = fields;

            /// <summary>
            /// A pair of key and value types.
            /// </summary>
            public class Field(Type key, Type value)
            {
                public Type Key { get; } = key;
                public Type Value { get; } = value;

                /// <summary>
                /// The symbol that this field defines.<br/>
                /// Initialized by the Binder.
                /// </summary>
                internal Symbol? Symbol { get; set; }
            }
        }

        /// <summary>
        /// The type of a function.
        /// </summary>
        public class Function(List<Declaration> parameters, List<Type>? returnTypes, List<Name>? typeParameters) : Type
        {
            public List<Declaration> Parameters { get; } = parameters;
            public List<Type>? ReturnTypes { get; } = returnTypes;
            public List<Name>? TypeParameters { get; } = typeParameters;
        }

        public class Array(Type elementType) : Type
        {
            public Type ElementType { get; } = elementType;
        }

        /// <summary>
        /// A type followed by a '?'.
        /// </summary>
        public class Nillable(Type inner) : Type
        {
            public Type Inner { get; } = inner;
        }

        // public class Union(List<Type> types) : Type
        // {
        //     public List<Type> Types { get; } = types;
        // }
    }

    /// <summary>
    /// A list of statements.
    /// </summary>
    public class Block(
        List<Statement> statements,
        List<TypeAliasDeclaration> typeDeclarations,
        List<Statement.LabelDefinition> labels)
    {
        public List<Statement> Statements { get; } = statements;

        /// <summary>
        /// All types that were declared in this block.
        /// </summary>
        public List<TypeAliasDeclaration> TypeDeclarations { get; } = typeDeclarations;

        public List<Statement.LabelDefinition> Labels { get; } = labels;
    }

    /// <summary>
    /// The top-level block of a function or a file, which also stores all of its return statements.
    /// </summary>
    public class Chunk : Block
    {
        /// <summary>
        /// All the return statements in this chunk.
        /// </summary>
        public List<Statement.Return> ReturnStatements { get; }

        /// <summary>
        /// Whether all the control paths in this chunk stop or return a value.
        /// </summary>
        public bool AllPathsReturn { get; set; }

        public Expression.Function? ParentFunction { get; internal set; }

        public Chunk(List<Statement> statements,
            List<TypeAliasDeclaration> typeDeclarations,
            List<Statement.LabelDefinition> labels,
            List<Statement.Return> returnStatements) : base(statements, typeDeclarations, labels)
        {
            ReturnStatements = returnStatements;
            foreach (var returnStatement in returnStatements)
            {
                returnStatement.ParentChunk = this;
            }
        }
    }

    /// <summary>
    /// The contents of a file, containing its chunk and the global declarations in it.
    /// </summary>
    public class File : Chunk
    {
        public List<Statement.GlobalDeclaration> GlobalDeclarations { get; }

        public File(Chunk chunk, List<Statement.GlobalDeclaration> globalDeclarations)
            : base(chunk.Statements, chunk.TypeDeclarations, chunk.Labels, chunk.ReturnStatements)
        {
            GlobalDeclarations = globalDeclarations;
        }

        public File() : base([], [], [], [])
        {
            GlobalDeclarations = [];
        }
    }

    /// <summary>
    /// Tree nodes that appear as statements.
    /// </summary>
    public abstract class Statement : Tree
    {
        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Statement;

        /// <summary>
        /// A do-end block.
        /// </summary>
        public class Do(Block body) : Statement
        {
            public Block Body { get; } = body;
        }

        /// <summary>
        /// An `if` statement, with zero or more `elseif` branches and an optional `else` branch.
        /// </summary>
        public class If(IfBranch primary, List<IfBranch> elseIfs, Block? elseBody) : Statement
        {
            public IfBranch Primary { get; } = primary;
            public List<IfBranch> ElseIfs { get; } = elseIfs;
            public Block? ElseBody { get; } = elseBody;
        }

        /// <summary>
        /// A numerical for loop.
        /// </summary>
        public class NumericalFor(
            Expression.Name counter,
            Expression start,
            Expression limit,
            Expression? step,
            Block body) : Statement
        {
            public Expression.Name Counter { get; } = counter;
            public Expression Start { get; } = start;
            public Expression Limit { get; } = limit;
            public Expression? Step { get; } = step;
            public Block Body { get; } = body;
        }

        /// <summary>
        /// A for loop with an iterator.
        /// </summary>
        public class IteratorFor(List<Declaration> declarations, Expression iterator, Block body) : Statement
        {
            public List<Declaration> Declarations { get; } = declarations;
            public Expression Iterator { get; } = iterator;
            public Block Body { get; } = body;
        }

        /// <summary>
        /// A while loop.
        /// </summary>
        public class While(Expression condition, Block body) : Statement
        {
            public Expression Condition { get; } = condition;
            public Block Body { get; } = body;
        }

        /// <summary>
        /// A repeat-until loop.
        /// </summary>
        public class RepeatUntil(Block body, Expression condition) : Statement
        {
            public Block Body { get; } = body;
            public Expression Condition { get; } = condition;
        }

        /// <summary>
        /// Declarations of one or more variables.
        /// </summary>
        public class VariableDeclaration(List<Declaration> declarations, List<Expression> values) : Statement
        {
            public List<Declaration> Declarations { get; } = declarations;
            public List<Expression> Values { get; } = values;
        }

        /// <summary>
        /// Declarations of one or more local variables.
        /// </summary>
        public class LocalDeclaration(List<Declaration> declarations, List<Expression> values)
            : VariableDeclaration(declarations, values);

        /// <summary>
        /// Declarations of one or more global variables.
        /// </summary>
        public class GlobalDeclaration(List<Declaration> declarations, List<Expression> values)
            : VariableDeclaration(declarations, values);

        /// <summary>
        /// A local function declaration.<br/>
        /// (This is different from a `LocalDeclaration`, because here, the function's name is made available in the body,
        /// allowing it to reference itself.)
        /// </summary>
        public class LocalFunctionDeclaration(Expression.Name name, Expression.Function function) : Statement
        {
            public Expression.Name Name { get; } = name;
            public Expression.Function Function { get; } = function;
        }

        /// <summary>
        /// A `return` statement, with optional return values.
        /// </summary>
        public class Return(List<Expression> values) : Statement
        {
            public List<Expression> Values { get; } = values;
            public Chunk ParentChunk { get; internal set; } = null!;
        }

        /// <summary>
        /// A `break` statement.
        /// </summary>
        public class Break : Statement;

        /// <summary>
        /// An assignment of one or more values to one or more targets.
        /// </summary>
        public class Assignment(List<Expression> targets, List<Expression> values) : Statement
        {
            public List<Expression> Targets { get; } = targets;
            public List<Expression> Values { get; } = values;
        }

        /// <summary>
        /// A wrapper that allows Expression.Call to be a statement.
        /// </summary>
        public class Call(Expression.Call call) : Statement
        {
            public Expression.Call CallExpr { get; } = call;
        }

        /// <summary>
        /// A wrapper that allows Expression.MethodCall to be a statement.
        /// </summary>
        public class MethodCall(Expression.MethodCall methodCall) : Statement
        {
            public Expression.MethodCall CallExpr { get; } = methodCall;
        }

        /// <summary>
        /// A label definition. ("::someLabel::")
        /// </summary>
        public class LabelDefinition(LabelName name) : Statement
        {
            public LabelName Name { get; } = name;
        }

        /// <summary>
        /// A `goto` statement. ("goto someLabel")
        /// </summary>
        public class Goto(LabelName name) : Statement
        {
            public LabelName Name { get; } = name;
        }
    }

    /// <summary>
    /// Tree nodes that appear as expressions.
    /// </summary>
    public abstract class Expression : Tree
    {
        /// <summary>
        /// An invalid tree node - returned when an error was encountered during parsing.
        /// </summary>
        public class Error : Expression;

        /// <summary>
        /// A named reference to a variable.
        /// </summary>
        public class Name(string value) : Expression
        {
            public string Value { get; } = value;

            public bool IsAssignmentTarget { get; internal set; }

            public override string ToString()
            {
                return Value;
            }
        }

        /// <summary>
        /// The `nil` value.
        /// </summary>
        public class Nil : Expression;

        /// <summary>
        /// The `true` value.
        /// </summary>
        public class True : Expression;

        /// <summary>
        /// The `false` value.
        /// </summary>
        public class False : Expression;

        /// <summary>
        /// A numerical constant.
        /// </summary>
        public class Number(string value, double numberValue) : Expression
        {
            public string Value { get; } = value;
            public double NumberValue { get; } = numberValue;

            public override string ToString()
            {
                return Value;
            }
        }

        /// <summary>
        /// A string literal.
        /// </summary>
        public class String(string value) : Expression
        {
            public string Value { get; } = value;

            public override string ToString()
            {
                return Value;
            }
        }

        /// <summary>
        /// A multi-line string literal.
        /// </summary>
        public class LongString(string value, int level) : String(value)
        {
            public int Level { get; } = level;
        }

        /// <summary>
        /// A table constructor.
        /// </summary>
        public class Table(List<Table.Field> fields, bool isArray) : Expression
        {
            public List<Field> Fields { get; } = fields;

            /// <summary>
            /// Whether all this table's fields are list elements.
            /// </summary>
            public bool IsArray { get; } = isArray;

            public ValueLocation? ValueLocation { get; internal set; }

            /// <summary>
            /// A field in a table constructor.
            /// </summary>
            public class Field(Expression key, Expression value)
            {
                public Expression Key { get; } = key;
                public Expression Value { get; } = value;

                /// <summary>
                /// The symbol that this field defines. Used only for table types that are inferred from the containing
                /// table.<br/>
                /// Initialized by the Binder.
                /// </summary>
                internal Symbol? Symbol { get; set; }
            }
        }

        /// <summary>
        /// A function value.
        /// </summary>
        public class Function : Expression
        {
            public new Type.Function Type { get; }
            public Chunk Chunk { get; }
            public Range NameRange { get; }

            internal ValueLocation? ValueLocation { get; set; }

            /// <summary>
            /// Whether this function was defined with a `:`.
            /// </summary>
            public bool IsMethod { get; }

            /// <summary>
            /// A function value.
            /// </summary>
            public Function(Type.Function type, Chunk chunk, Range nameRange, bool isMethod)
            {
                Type = type;
                Chunk = chunk;
                chunk.ParentFunction = this;
                NameRange = nameRange;
                IsMethod = isMethod;
            }
        }

        /// <summary>
        /// A vararg expression (...).
        /// </summary>
        public class Vararg : Expression;

        public class Unary(Expression expression, Token op) : Expression
        {
            public Expression Expression { get; } = expression;
            public Token Operator { get; } = op;
        }

        /// <summary>
        /// A binary operator.
        /// </summary>
        public class Binary(Expression left, Expression right, Token op) : Expression
        {
            public Expression Left { get; } = left;
            public Expression Right { get; } = right;
            public Token Operator { get; } = op;
        }

        /// <summary>
        /// Indexed value access - target.key or target[key].
        /// </summary>
        public class Access(Expression target, Expression key) : Expression
        {
            public Expression Target { get; } = target;
            public Expression Key { get; } = key;
        }

        /// <summary>
        /// A function call.
        /// </summary>
        public class Call(Expression target, List<Expression> arguments, List<Type>? typeArguments) : Expression
        {
            public Expression Target { get; } = target;
            public List<Expression> Arguments { get; } = arguments;
            public List<Type>? TypeArguments { get; } = typeArguments;
        }

        /// <summary>
        /// A method call using `:` syntax.
        /// </summary>
        public class MethodCall(Expression target, String funcName, List<Expression> arguments) : Expression
        {
            public Expression Target { get; } = target;
            public String FuncName { get; } = funcName;
            public List<Expression> Arguments { get; } = arguments;
        }
    }

    public class LabelName(string value) : Tree
    {
        public string Value { get; } = value;
    }

    /// <summary>
    /// A branch in an `if` statement.
    /// </summary>
    public class IfBranch(Expression condition, Block body)
    {
        public Expression Condition { get; } = condition;
        public Block Body { get; } = body;
    }

    /// <summary>
    /// A declaration of a named value, with an optional type.
    /// </summary>
    public class Declaration(Expression.Name name, Type? type) : Tree
    {
        public Expression.Name Name { get; } = name;
        public Type? Type { get; } = type;
    }

    /// <summary>
    /// The contexts in which a Name can appear.
    /// </summary>
    public enum NameContext
    {
        /// <summary>
        /// The name references a value.
        /// </summary>
        Value,

        /// <summary>
        /// The name references a type.
        /// </summary>
        Type,

        /// <summary>
        /// The name references a label.
        /// </summary>
        Label,
    }

    /// <summary>
    /// A declaration of a type alias.
    /// </summary>
    public class TypeAliasDeclaration(Type.Name name, Type type) : Statement
    {
        public Type.Name Name { get; } = name;
        public Type Type { get; } = type;
    }
}

/// <summary>
/// Represents a location where a value could have a type that it's assigned to.
/// Used when inferring the parameter types of functions.
/// </summary>
public abstract record ValueLocation
{
    public record AssignmentValue(Tree.Statement.Assignment Assignment, int Index) : ValueLocation;

    public record Variable(Tree.Statement.VariableDeclaration VariableDeclaration, int Index) : ValueLocation;

    public record Argument(Tree.Expression.Call Call, int Index) : ValueLocation;

    public record ReturnValue(Tree.Statement.Return Return, int Index) : ValueLocation;

    public record TableField(Tree.Expression.Table.Field Field, ValueLocation Parent) : ValueLocation;
}