using System.Reflection;
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
    internal Project Project { get; private set; } = null!;
    private readonly LanguageServer server;

    /// <summary>
    /// Maps DocumentUris to the source they reference.
    /// </summary>
    private readonly Dictionary<DocumentUri, Source> uriSources = [];

    /// <summary>
    /// Maps sources to their respective DocumentUri.
    /// </summary>
    private readonly Dictionary<Source, DocumentUri> sourceUris = [];

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
                Project = Project.FromFilesInDirectory(uri.FileSystemPath);
                foreach (var source in Project.Sources)
                {
                    MapSourceToUri(source, source.Path);
                }
            }
            else
            {
                Project = new Project();
            }

            return Task.CompletedTask;
        });

        server.OnInitialized(async _ =>
        {
            var r = await server.Client.GetConfiguration(new ConfigurationParams
            {
                Items = [],
            }, CancellationToken.None);
        });

        server.AddHandler(new TextDocumentHandler(this));
        server.AddHandler(new HoverHandler(this));
        server.AddHandler(new DefinitionHandler(this));
        server.AddHandler(new ReferenceHandler(this));
        server.AddHandler(new DocumentHighlightHandler(this));
        server.AddHandler(new DidChangeWatchedFilesHandler(this));
        server.AddHandler(new DocumentDiagnosticHandler(this));
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
        return new(sourceUris[location.Source!], location.Range.ToLs());
    }

    /// <summary>
    /// Adds a new empty source at the given URI.
    /// </summary>
    public void AddSource(DocumentUri uri)
    {
        var source = new Source(uri.FileSystemPath, "");
        Project.AddSource(source);
        MapSourceToUri(source, uri);
    }

    public void RemoveSource(DocumentUri uri)
    {
        var source = uriSources[uri];
        Project.RemoveSource(source);
        uriSources.Remove(uri);
        sourceUris.Remove(source);
    }

    /// <summary>
    /// Maps a source to the URI it's located in.
    /// </summary>
    private void MapSourceToUri(Source source, DocumentUri uri)
    {
        uriSources[uri] = source;
        sourceUris[source] = uri;
    }

    public Source GetSourceByUri(DocumentUri uri)
    {
        return uriSources[uri];
    }

    public DocumentUri GetUriOfSource(Source source)
    {
        return sourceUris[source];
    }

    /// <summary>
    /// Tries to find the symbol that the `TextDocumentPosition` request is pointing to.
    /// </summary>
    public Symbol? GetRequestSymbol(TextDocumentPositionParams request)
    {
        var source = uriSources[request.TextDocument.Uri];
        return SymbolFinder.GetSymbolAtPosition(Project, source, request.Position.ToLeda()).Symbol;
    }

    public List<Location> GetSymbolReferences(Symbol symbol, bool includeDefinition)
    {
        List<Location> list = [];

        foreach (var source in Project.Sources)
        {
            list.AddRange(Project.GetSymbolReferencesInSource(source, symbol, includeDefinition)
                .Select(r => new Location(sourceUris[source], r.ToLs())));
        }

        return list;
    }
}