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
        var source = server.UriSources[request.TextDocument.Uri];

        var name = NameFinder.GetNameAtPosition(source.Tree, request.Position.ToLeda());

        if (name is not null && source.TryGetTreeSymbol(name, out var symbol))
        {
            string? content = null;
            var type = source.Evaluator.TypeToString(source.Evaluator.GetTypeOfSymbol(symbol), multiline: true);
            if (name is Tree.Expression.Name valueName)
            {
                content = $"""
                           ```leda
                           local {valueName.Value}: {type}
                           ```
                           """;
            }
            else if (name is Tree.Type.Name typeName)
            {
                var typeValue = symbol is not Symbol.IntrinsicType && symbol.Kind != SymbolKind.TypeParameter
                    ? " = " + type
                    : "";
                content = $"""
                           ```leda
                           type {typeName.Value}{typeValue}
                           ```
                           """;
            }

            if (content != null)
            {
                return Task.FromResult(new HoverResponse
                {
                    Contents = new MarkupContent
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