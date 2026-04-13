using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Hover;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;
using Type = Leda.Lang.Type;

namespace Leda.LSP;

public class HoverHandler(LedaServer server) : HoverHandlerBase
{
    protected override Task<HoverResponse?> Handle(HoverParams request, CancellationToken token)
    {
        var source = server.UriSources[request.TextDocument.Uri];

        var name = NameFinder.GetNameAtPosition(source.Chunk, request.Position.ToLeda());

        if (name is not null && source.TryGetTreeSymbol(name, out var symbol))
        {
            string? content = null;
            if (symbol is Symbol.StringKey { Table: var table, Key: var key })
            {
                content =
                    $"(field) {key}: {source.Evaluator.TypeToString(source.Evaluator.GetStringKeyInTable(table, key)?.Type ?? Type.Unknown)}";
            }
            else if (name is Tree.Expression.Name valueName)
            {
                var type = source.Evaluator.GetTypeOfSymbol(symbol);
                if (symbol is Symbol.LocalFunction && type is Type.Function function)
                {
                    content = $"local function {valueName.Value}{source.Evaluator.FunctionSignatureToString(function)}";
                }
                else
                {
                    content = $"local {valueName.Value}: {source.Evaluator.TypeToString(type)}";
                }
            }
            else if (name is Tree.Type.Name typeName)
            {
                var typeValue = symbol is not Symbol.IntrinsicType and not Symbol.TypeParameter
                    ? " = " + source.Evaluator.TypeToString(source.Evaluator.GetTypeOfTypeName(typeName),
                        typeContents: true, multiline: true)
                    : "";
                content = $"type {typeName.Value}{typeValue}";
            }

            if (content != null)
            {
                content = $"""
                           ```leda
                           {content}
                           ```
                           """;
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