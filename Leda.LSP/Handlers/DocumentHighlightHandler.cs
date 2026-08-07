using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentHighlight;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace Leda.LSP;

public class DocumentHighlightHandler(LedaServer server) : DocumentHighlightHandlerBase
{
    protected override Task<DocumentHighlightResponse> Handle(DocumentHighlightParams request, CancellationToken token)
    {
        var source = server.GetSourceByUri(request.TextDocument.Uri);
        if (server.GetRequestSymbol(request) is { } symbol)
        {
            var references = server.Project.GetSymbolReferencesInSource(source, symbol, true);

            return Task.FromResult(new DocumentHighlightResponse(
                references.Select(r => new DocumentHighlight { Range = r.ToLs() }).ToList()));
        }

        return Task.FromResult(new DocumentHighlightResponse([]));
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.DocumentHighlightProvider = true;
    }
}