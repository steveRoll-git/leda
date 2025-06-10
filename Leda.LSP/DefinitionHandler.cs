using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Definition;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Location = EmmyLua.LanguageServer.Framework.Protocol.Model.Location;

namespace Leda.LSP;

public class DefinitionHandler(LedaServer server) : DefinitionHandlerBase
{
    protected override Task<DefinitionResponse?> Handle(DefinitionParams request, CancellationToken cancellationToken)
    {
        var source = server.UriSources[request.TextDocument.Uri];

        var name = NameFinder.GetNameAtPosition(source.Tree, request.Position.ToLeda());

        if (name != null && source.TryGetTreeSymbol(name, out var symbol) && symbol.Definition.Source != null)
        {
            return Task.FromResult(new DefinitionResponse(new Location(
                server.SourceUris[symbol.Definition.Source],
                symbol.Definition.Range.ToLs())))!;
        }

        return Task.FromResult<DefinitionResponse?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.DefinitionProvider = true;
    }
}