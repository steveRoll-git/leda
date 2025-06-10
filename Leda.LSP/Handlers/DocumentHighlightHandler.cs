using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentHighlight;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;

namespace Leda.LSP;

public class DocumentHighlightHandler(LedaServer server) : DocumentHighlightHandlerBase
{
    protected override Task<DocumentHighlightResponse> Handle(DocumentHighlightParams request, CancellationToken token)
    {
        var source = server.UriSources[request.TextDocument.Uri];
        if (server.TryGetRequestSymbol(request, out var symbol))
        {
            IEnumerable<Location> references = source.SymbolReferences.GetValueOrDefault(symbol) ?? [];

            if (symbol.Definition.Source == source)
            {
                references = references.Prepend(symbol.Definition);
            }

            return Task.FromResult(new DocumentHighlightResponse(
                references.Select(l => new DocumentHighlight { Range = l.Range.ToLs() })
                    .ToList()));
        }

        return Task.FromResult<DocumentHighlightResponse?>(null)!;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.DocumentHighlightProvider = true;
    }
}