using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Hover;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;

namespace Leda.LSP;

public class HoverHandler(LedaServer server) : HoverHandlerBase
{
    protected override Task<HoverResponse?> Handle(HoverParams request, CancellationToken token)
    {
        // TODO TEMPORARY
        var source = server.UriSources[request.TextDocument.Uri];

        var name = NameFinder.GetNameAtPosition(source.Tree, request.Position.ToLeda());

        if (name != null && source.TryGetTreeSymbol(name, out var symbol))
        {
            string? content = null;
            if (name.Context == Tree.NameContext.Value)
            {
                source.TryGetSymbolType(symbol, out var type);
                content = $"""
                           ```leda
                           local {name.Value}: {type?.ToString() ?? "unknown"}
                           ```
                           """;
            }
            else if (name.Context == Tree.NameContext.Type)
            {
                content = $"""
                           ```leda
                           type {name.Value}
                           ```
                           """;
            }

            if (content != null)
            {
                return Task.FromResult(new HoverResponse()
                {
                    Contents = new MarkupContent()
                    {
                        Kind = MarkupKind.Markdown,
                        Value = content
                    },
                    Range = name.Range.ToLs()
                })!;
            }
        }

        return Task.FromResult<HoverResponse?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.HoverProvider = true;
    }
}