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
        var source = server.GetSourceByUri(request.TextDocument.Uri);

        if (SymbolFinder.GetSymbolAtPosition(source, request.Position.ToLeda()) is ({ } symbol, var range))
        {
            string? content;
            switch (symbol)
            {
                case Symbol.StringField { Table: var table, Key: var key }:
                    content =
                        $"(field) {key}: {source.Evaluator.TypeToString(source.Evaluator.GetTypeOfStringFieldInTable(table, key) ?? Type.Unknown)}";
                    break;
                case Symbol.LocalVariable:
                    content =
                        $"local {symbol.Name}: {source.Evaluator.TypeToString(source.Evaluator.GetTypeOfSymbol(symbol))}";
                    break;
                case Symbol.Parameter:
                    content =
                        $"(parameter) {symbol.Name}: {source.Evaluator.TypeToString(source.Evaluator.GetTypeOfSymbol(symbol))}";
                    break;
                case Symbol.LocalFunction:
                {
                    var type = source.Evaluator.GetTypeOfSymbol(symbol);
                    content =
                        $"local function {symbol.Name}{(type is Type.Function function ? source.Evaluator.FunctionSignatureToString(function) : "")}";
                    break;
                }
                case Symbol.IntrinsicType or Symbol.TypeAlias or Symbol.TypeParameter:
                {
                    var typeValue = symbol is not Symbol.IntrinsicType and not Symbol.TypeParameter
                        ? " = " + source.Evaluator.TypeToString(source.Evaluator.GetTypeOfSymbol(symbol),
                            typeContents: true, multiline: true)
                        : "";
                    content = $"type {symbol.Name}{typeValue}";
                    break;
                }
                case Symbol.Label:
                    content = $"(label) {symbol.Name}";
                    break;
                default:
                    content = $"??? {symbol.Name}";
                    break;
            }

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
                Range = range.ToLs()
            })!;
        }

        return Task.FromResult<HoverResponse?>(null);
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.HoverProvider = true;
    }
}