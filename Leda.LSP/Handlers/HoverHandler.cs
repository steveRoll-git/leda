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
    private Project Project => server.Project;

    protected override Task<HoverResponse?> Handle(HoverParams request, CancellationToken token)
    {
        var source = server.GetSourceByUri(request.TextDocument.Uri);

        if (SymbolFinder.GetSymbolAtPosition(Project, source, request.Position.ToLeda()) is
            ({ } symbol, var range, var getType))
        {
            string? content;

            var gotType = getType?.Invoke(Project.TypeEvaluator);

            switch (symbol)
            {
                case Symbol.StringField { Key: var key }:
                    content =
                        $"(field) {key}: {Project.TypeEvaluator.TypeToString(gotType ?? Type.Unknown)}";
                    break;

                case Symbol.Variable or Symbol.Parameter:
                {
                    var kind = symbol switch
                    {
                        Symbol.LocalVariable => "local",
                        Symbol.GlobalVariable => "global",
                        Symbol.Parameter => "(parameter)",
                        _ => "???",
                    };
                    var type = gotType ?? Project.TypeEvaluator.GetTypeOfSymbol(symbol);
                    content =
                        $"{kind} {symbol.Name}: {Project.TypeEvaluator.TypeToString(type, multiline: true)}";
                    break;
                }

                case Symbol.LocalFunction:
                {
                    var type = gotType ?? Project.TypeEvaluator.GetTypeOfSymbol(symbol);
                    content =
                        $"local function {symbol.Name}{(type is Type.Function function ? Project.TypeEvaluator.FunctionSignatureToString(function) : "")}";
                    break;
                }

                case Symbol.IntrinsicType or Symbol.TypeAlias or Symbol.TypeParameter:
                {
                    var typeValue = symbol is not Symbol.IntrinsicType and not Symbol.TypeParameter
                        ? " = " + Project.TypeEvaluator.TypeToString(Project.TypeEvaluator.GetTypeOfSymbol(symbol),
                            true, true)
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
                    Value = content,
                },
                Range = range.ToLs(),
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