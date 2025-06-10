using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextDocument;
using EmmyLua.LanguageServer.Framework.Server;
using Leda.Lang;
using Location = EmmyLua.LanguageServer.Framework.Protocol.Model.Location;

namespace Leda.LSP;

/// <summary>
/// Responsible for the communication between a Leda project and the language server. Updates sources with changes
/// received from the client, and pushes diagnostics that the language reports.
/// </summary>
public class LedaServer
{
    private Project project = null!;
    private readonly LanguageServer server;

    /// <summary>
    /// Maps DocumentUris to the source they reference.
    /// </summary>
    public readonly Dictionary<DocumentUri, Source> UriSources = [];

    /// <summary>
    /// Maps sources to their respective DocumentUri.
    /// </summary>
    public readonly Dictionary<Source, DocumentUri> SourceUris = [];

    public LedaServer()
    {
        // TODO support TCP
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();

        server = LanguageServer.From(input, output);
        server.OnInitialize((initParams, info) =>
        {
            info.Name = "Leda";
            info.Version = Assembly.GetEntryAssembly()!.GetName().Version?.ToString();

            if (initParams.RootUri is { } uri)
            {
                project = Project.FromFilesInDirectory(uri.FileSystemPath);
                foreach (var source in project.Sources)
                {
                    AddSource(source, source.Path);
                }
            }
            else
            {
                project = new Project();
            }

            return Task.CompletedTask;
        });

        server.OnInitialized(async _ =>
        {
            var r = await server.Client.GetConfiguration(new ConfigurationParams()
            {
                Items = []
            }, CancellationToken.None);

            project.CheckAll(PushDiagnostics);
        });

        server.AddHandler(new TextDocumentHandler(this));
        server.AddHandler(new HoverHandler(this));
        server.AddHandler(new DefinitionHandler(this));
        server.AddHandler(new ReferenceHandler(this));
    }

    public Task Run()
    {
        return server.Run();
    }

    /// <summary>
    /// Converts a Leda location to a language server location. Assumes that `location.Source` is not null.
    /// </summary>
    public Location ToLsLocation(Leda.Lang.Location location)
    {
        return new(SourceUris[location.Source], location.Range.ToLs());
    }

    private void AddSource(Source source, DocumentUri uri)
    {
        UriSources[uri] = source;
        SourceUris[source] = uri;
    }

    /// <summary>
    /// Updates this source's code, then parses, binds and checks it.
    /// </summary>
    public void UpdateAndRecheckSource(Source source, string code)
    {
        source.Code = code;
        PushDiagnostics(source, project.Check(source));
    }

    private void PushDiagnostics(Source source, List<Diagnostic> diagnostics)
    {
        server.Client.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = SourceUris[source],
            Diagnostics = diagnostics.Select(d => d.ToLs()).ToList()
        });
    }


    /// <summary>
    /// Tries to find the symbol that the `TextDocumentPosition` request is pointing to.
    /// </summary>
    public bool TryGetRequestSymbol(TextDocumentPositionParams request, [NotNullWhen(true)] out Symbol? symbol)
    {
        var source = UriSources[request.TextDocument.Uri];
        var name = NameFinder.GetNameAtPosition(source.Tree, request.Position.ToLeda());
        if (name != null)
        {
            return source.TryGetTreeSymbol(name, out symbol);
        }

        symbol = null;
        return false;
    }

    public List<Location> GetSymbolReferences(Symbol symbol, bool includeDefinition)
    {
        List<Location> list = [];

        if (includeDefinition && symbol.Definition.Source != null)
        {
            list.Add(ToLsLocation(symbol.Definition));
        }

        foreach (var projectSource in project.Sources)
        {
            if (projectSource.SymbolReferences.TryGetValue(symbol, out var references))
            {
                list.AddRange(references.Select(ToLsLocation));
            }
        }

        return list;
    }
}