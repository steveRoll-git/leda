using System.Reflection;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Configuration;
using EmmyLua.LanguageServer.Framework.Protocol.Model;
using EmmyLua.LanguageServer.Framework.Server;
using Leda.Lang;

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
    }

    public Task Run()
    {
        return server.Run();
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
}