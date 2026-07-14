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
        modifiedSources.Add(source);
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

    /// <summary>
    /// Clears all of a source's dependencies, while also updating their `Dependents` set.
    /// </summary>
    internal static void ClearDependencies(Source source)
    {
        foreach (var dependency in source.Dependencies)
        {
            dependency.Dependents.Remove(source);
        }

        source.Dependencies.Clear();
    }

    /// <summary>
    /// Adds a source as a dependency of another source.
    /// </summary>
    /// <param name="dependent">The source that depends on the other source.</param>
    /// <param name="dependency">The source that is the dependency of the other source.</param>
    internal static void AddDependency(Source dependent, Source dependency)
    {
        dependent.Dependencies.Add(dependency);
        dependency.Dependents.Add(dependent);
    }

    /// <summary>
    /// Update's the source's flags to indicate that it needs rechecking (and optionally binding), and does the same for
    /// all of its recursive dependents.
    /// </summary>
    /// <param name="source">The source to mark as modified.</param>
    /// <param name="codeEdited">Whether the source's code was edited, meaning parsing and binding need to run
    /// again.</param>
    private void MarkModified(Source source, bool codeEdited)
    {
        if (codeEdited)
        {
            // The source is added to the modified set - it will be parsed and bound the next time diagnostics are
            // requested.
            modifiedSources.Add(source);
            source.NeedsBindingGlobals = true;
            MarkGlobalUsers(source);
        }

        if (source.NeedsChecking)
        {
            return;
        }

        source.NeedsChecking = true;

        foreach (var dependent in source.Dependents)
        {
            MarkModified(dependent, false);
        }
    }

    /// <summary>
    /// Goes over all sources that reference any global variables defined in the given source, and marks them.
    /// </summary>
    private void MarkGlobalUsers(Source source)
    {
        foreach (var globalDeclaration in source.File.GlobalDeclarations)
        {
            foreach (var declaration in globalDeclaration.Declarations)
            {
                foreach (var otherSource in Sources)
                {
                    if (otherSource == source)
                    {
                        continue;
                    }

                    if (otherSource.GlobalNames.ContainsKey(declaration.Name.Value))
                    {
                        otherSource.NeedsBindingGlobals = true;
                        MarkModified(otherSource, false);
                        goto NextSource;
                    }
                }
            }

            NextSource: ;
        }
    }

    /// <summary>
    /// Change's a source's code, and marks it and all of its dependent sources as modified.
    /// </summary>
    /// <param name="source">The source that was modified.</param>
    /// <param name="code">The updated source code.</param>
    public void ModifySource(Source source, string code)
    {
        source.Code = code;
        MarkModified(source, true);
    }

    /// <summary>
    /// Returns the list of current diagnostics of a source.
    /// </summary>
    public List<Diagnostic> GetDiagnostics(Source source)
    {
        if (modifiedSources.Count > 0)
        {
            // If there are any modified sources, they must be parsed and bound before the checking of any other source,
            // so that global variables will be available.
            foreach (var modifiedSource in modifiedSources)
            {
                ClearBindings(modifiedSource);
                RemoveGlobals(modifiedSource);
                Parse(modifiedSource);
                CreateGlobals(modifiedSource);
            }

            foreach (var modifiedSource in modifiedSources)
            {
                Bind(modifiedSource);
                BindGlobals(modifiedSource);
            }

            // A modified source may now define new global variables - mark users of those too
            foreach (var modifiedSource in modifiedSources)
            {
                MarkGlobalUsers(modifiedSource);
            }

            modifiedSources.Clear();
        }

        BindGlobals(source);

        Check(source);

        return
        [
            ..source.ParserDiagnostics,
            ..source.BinderDiagnostics,
            ..source.NameNotFoundDiagnostics,
            ..source.CheckerDiagnostics,
        ];
    }

    /// <summary>
    /// Parse the source's contents and store the syntax tree.
    /// </summary>
    private void Parse(Source source)
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
                var symbol = new Symbol.GlobalVariable(globalDeclaration,
                    i,
                    Binder.IsVariableUninitialized(globalDeclaration, i),
                    source.File);
                source.AttachSymbol(declaration.Name, symbol, true);
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
    private void Bind(Source source)
    {
        source.SymbolReferences.Clear();
        source.BinderDiagnostics = Binder.Bind(source);
    }

    /// <summary>
    /// Binds all top level `Name` nodes that aren't bound to any locally defined symbol in this source.<br/>
    /// Names are either bound to global variables defined in another source in the project, or reported as not found.
    /// </summary>
    private void BindGlobals(Source source)
    {
        if (!source.NeedsBindingGlobals)
        {
            return;
        }

        source.NameNotFoundDiagnostics.Clear();

        // First, remove existing references to global symbols.
        foreach (var (_, nodes) in source.GlobalNames)
        {
            foreach (var tree in nodes)
            {
                if (source.GetTreeSymbol(tree) is { } symbol)
                {
                    source.SymbolReferences.Remove(symbol);
                }

                source.DetachSymbol(tree);
            }
        }

        foreach (var (name, nodes) in source.GlobalNames)
        {
            if (globalVariables.TryGetValue(name, out var global))
            {
                foreach (var tree in nodes)
                {
                    source.AttachSymbol(tree, global);
                }
            }
            else
            {
                foreach (var tree in nodes)
                {
                    source.NameNotFoundDiagnostics.Add(new Diagnostic.NameNotFound(tree.Range, tree.Value,
                        Tree.NameContext.Value));
                }
            }
        }

        source.NeedsBindingGlobals = false;
    }

    /// <summary>
    /// Checks the types of all nodes.
    /// </summary>
    private void Check(Source source)
    {
        if (!source.NeedsChecking)
        {
            return;
        }

        ClearDependencies(source);
        source.Evaluator = new TypeEvaluator(source);
        source.CheckerDiagnostics = Checker.Check(source, source.Evaluator);
        source.NeedsChecking = false;
    }

    private void ClearBindings(Source source)
    {
        source.TreeSymbolMap.Clear();
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