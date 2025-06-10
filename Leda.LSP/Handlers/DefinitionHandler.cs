using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Definition;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace Leda.LSP;

public class DefinitionHandler(LedaServer server) : DefinitionHandlerBase
{
    protected override Task<DefinitionResponse?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        if (server.TryGetRequestSymbol(request, out var symbol) && symbol.Definition.Source != null)
        {
            return Task.FromResult(new DefinitionResponse(server.ToLsLocation(symbol.Definition)))!;
        }

        return Task.FromResult<DefinitionResponse?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.DefinitionProvider = true;
    }
}