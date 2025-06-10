using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Reference;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;

namespace Leda.LSP;

public class ReferenceHandler(LedaServer server) : ReferenceHandlerBase
{
    protected override Task<ReferenceResponse?> Handle(ReferenceParams request, CancellationToken cancellationToken)
    {
        if (server.TryGetRequestSymbol(request, out var symbol))
        {
            var references = server.GetSymbolReferences(symbol, request.Context?.IncludeDeclaration ?? false);
            return Task.FromResult(new ReferenceResponse(references))!;
        }

        return Task.FromResult<ReferenceResponse?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.ReferencesProvider = true;
    }
}