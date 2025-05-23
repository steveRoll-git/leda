using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Client.PublishDiagnostics;
using EmmyLua.LanguageServer.Framework.Protocol.Message.TextDocument;
using EmmyLua.LanguageServer.Framework.Protocol.Model.TextEdit;
using EmmyLua.LanguageServer.Framework.Server;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;
using Diagnostic = EmmyLua.LanguageServer.Framework.Protocol.Model.Diagnostic.Diagnostic;

namespace Leda.LSP;

public class TextDocumentHandler(LanguageServer server) : TextDocumentHandlerBase
{
    private List<Diagnostic> CheckFile(string path, string text)
    {
        var collector = new DiagnosticCollector();
        var source = new Source(path, text);
        source.Parse(collector);
        source.Bind(collector);
        source.Check(collector);

        return collector.Diagnostics.Select(d => d.ToLs()).ToList();
    }

    protected override Task Handle(DidOpenTextDocumentParams request, CancellationToken token)
    {
        Console.Error.WriteLine($"TextDocumentHandler: DidOpenTextDocument {request.TextDocument.Uri}");
        var document = request.TextDocument;
        var diagnostics = CheckFile(document.Uri.FileSystemPath, document.Text);

        server.Client.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = diagnostics
        });

        return Task.CompletedTask;
    }

    protected override Task Handle(DidChangeTextDocumentParams request, CancellationToken token)
    {
        Console.Error.WriteLine($"TextDocumentHandler: DidChangeTextDocument {request.TextDocument.Uri}");
        var document = request.TextDocument;
        var diagnostics = CheckFile(document.Uri.FileSystemPath, request.ContentChanges[0].Text);

        server.Client.PublishDiagnostics(new PublishDiagnosticsParams
        {
            Uri = request.TextDocument.Uri,
            Diagnostics = diagnostics
        });

        return Task.CompletedTask;
    }

    protected override Task Handle(DidCloseTextDocumentParams request, CancellationToken token)
    {
        Console.Error.WriteLine($"TextDocumentHandler: DidCloseTextDocument {request.TextDocument.Uri}");
        return Task.CompletedTask;
    }

    protected override Task Handle(WillSaveTextDocumentParams request, CancellationToken token)
    {
        return Task.CompletedTask;
    }

    protected override Task<List<TextEdit>?> HandleRequest(WillSaveTextDocumentParams request, CancellationToken token)
    {
        return Task.FromResult<List<TextEdit>?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.TextDocumentSync = new TextDocumentSyncOptions
        {
            Change = TextDocumentSyncKind.Full,
            OpenClose = true,
            Save = true
        };
    }
}