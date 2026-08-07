namespace Leda.Lang;

/// <summary>
/// A collection of Leda source files.
/// </summary>
public class Project
{
    public readonly List<Source> Sources = [];
    private readonly Dictionary<string, Source> sourcesByPath = [];

    /// <summary>
    /// Set of sources whose code has been modified, meaning they need a Parser and Binder pass.
    /// </summary>
    private readonly HashSet<Source> modifiedSources = [];

    /// <summary>
    /// Global variables defined by any of the sources in the project.
    /// </summary>
    private readonly Dictionary<string, Symbol.GlobalVariable> globalVariables = [];

    public TypeEvaluator TypeEvaluator { get; internal set; }

    /// <summary>
    /// Maps tree nodes to non-local symbols they're bound to, such as string keys or global variables.
    /// </summary>
    private Dictionary<Tree, Symbol> nonLocalBindings = [];

    /// <summary>
    /// A counter that is incremented every time a file changes.
    /// Used to figure out whether files need to be rechecked.
    /// </summary>
    internal int EditVersion { get; private set; }

    public Project()
    {
        TypeEvaluator = new TypeEvaluator(this);
    }

    /// <summary>
    /// Adds a source file to this project.
    /// </summary>
    public void AddSource(Source source)
    {
        if (sourcesByPath.ContainsKey(source.Path))
        {
            throw new Exception("A source with this path has already been added");
        }

        Sources.Add(source);
        sourcesByPath.Add(source.Path, source);
        MarkModified(source);
    }

    /// <summary>
    /// Removes a previously added source from this project.
    /// </summary>
    public void RemoveSource(Source source)
    {
        if (!sourcesByPath.ContainsKey(source.Path))
        {
            throw new Exception("This source hasn't been added");
        }

        Sources.Remove(source);
        sourcesByPath.Remove(source.Path);
    }

    private void MarkModified(Source source)
    {
        modifiedSources.Add(source);
        EditVersion++;
    }

    /// <summary>
    /// Change's a source's code, and marks it and all of its dependent sources as modified.
    /// </summary>
    /// <param name="source">The source that was modified.</param>
    /// <param name="code">The updated source code.</param>
    public void ModifySource(Source source, string code)
    {
        source.Code = code;
        MarkModified(source);
    }

    /// <summary>
    /// If modified files are present - parses and binds them, and resets the type evaluator and non-local bindings.
    /// </summary>
    private void UpdateModifiedFiles()
    {
        if (modifiedSources.Count == 0)
        {
            return;
        }

        TypeEvaluator = new(this);
        nonLocalBindings.Clear();

        foreach (var modifiedSource in modifiedSources)
        {
            RemoveGlobals(modifiedSource);
            Parse(modifiedSource);
            Bind(modifiedSource);
            CreateGlobals(modifiedSource);
        }

        modifiedSources.Clear();
    }

    /// <summary>
    /// Returns the list of current diagnostics of a source.
    /// </summary>
    public List<Diagnostic> GetDiagnostics(Source source)
    {
        UpdateModifiedFiles();

        Check(source);

        return
        [
            ..source.ParserDiagnostics,
            ..source.BinderDiagnostics,
            ..source.CheckerDiagnostics,
        ];
    }

    /// <summary>
    /// Parse the source's contents and store the syntax tree.
    /// </summary>
    private static void Parse(Source source)
    {
        var (tree, diagnostics) = Parser.ParseFile(source);
        source.File = tree;
        source.ParserDiagnostics = diagnostics;
    }

    /// <summary>
    /// Creates global variable symbols for all globals defined in the source, and stores them in the project's
    /// `globalVariables` dictionary.
    /// </summary>
    private void CreateGlobals(Source source)
    {
        foreach (var globalDeclaration in source.File.GlobalDeclarations)
        {
            for (var i = 0; i < globalDeclaration.Declarations.Count; i++)
            {
                var declaration = globalDeclaration.Declarations[i];
                if (globalVariables.ContainsKey(declaration.Name.Value))
                {
                    // Diagnostics for duplicate global declarations will be reported by the Checker.
                    continue;
                }

                var symbol = new Symbol.GlobalVariable(globalDeclaration,
                    i,
                    Binder.IsVariableUninitialized(globalDeclaration, i),
                    source.File)
                {
                    Definition = new(source, declaration.Name.Range),
                };
                AttachNonLocalSymbol(declaration.Name, symbol);
                globalVariables.Add(declaration.Name.Value, symbol);
            }
        }
    }

    /// <summary>
    /// Removes all global variables defined by this source.
    /// </summary>
    private void RemoveGlobals(Source source)
    {
        foreach (var globalDeclaration in source.File.GlobalDeclarations)
        {
            foreach (var declaration in globalDeclaration.Declarations)
            {
                if (globalVariables.TryGetValue(declaration.Name.Value, out var global) &&
                    global.Definition.Source == source)
                {
                    globalVariables.Remove(declaration.Name.Value);
                }
            }
        }
    }

    /// <summary>
    /// Associates all top level `Name` nodes with symbols.
    /// </summary>
    private static void Bind(Source source)
    {
        source.BinderDiagnostics = Binder.Bind(source);
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    private void Check(Source source)
    {
        if (source.CheckedVersion == EditVersion)
        {
            return;
        }

        source.CheckerDiagnostics = Checker.Check(this, source, TypeEvaluator);
        source.CheckedVersion = EditVersion;
    }

    public Symbol? GetTreeSymbol(Tree tree)
    {
        return tree.LocalBinding ?? nonLocalBindings.GetValueOrDefault(tree);
    }

    /// <summary>
    /// Associates the given tree node with a non-local symbol.
    /// </summary>
    internal void AttachNonLocalSymbol(Tree tree, Symbol symbol)
    {
        nonLocalBindings[tree] = symbol;
    }

    internal Symbol? GetGlobalVariable(string name)
    {
        return globalVariables.GetValueOrDefault(name);
    }

    /// <summary>
    /// Creates a new project and adds all leda files in the given path.
    /// </summary>
    public static Project FromFilesInDirectory(string path)
    {
        var project = new Project();

        foreach (var filePath in Directory.EnumerateFiles(path, "*.leda"))
        {
            project.AddSource(Source.ReadFromFile(filePath));
        }

        return project;
    }
}