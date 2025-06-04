using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.Hover;
using EmmyLua.LanguageServer.Framework.Protocol.Model.Markup;
using EmmyLua.LanguageServer.Framework.Server.Handler;
using Leda.Lang;

namespace Leda.LSP;

public class HoverHandler : HoverHandlerBase
{
    protected override Task<HoverResponse?> Handle(HoverParams request, CancellationToken token)
    {
        // TODO TEMPORARY
        var source = new Source(request.TextDocument.Uri.FileSystemPath);
        source.Parse(new DiagnosticCollector());

        var name = NameFinder.GetNameAtPosition(source.Tree, request.Position.ToLeda());

        if (name != null)
        {
            return Task.FromResult(new HoverResponse()
            {
                Contents = new MarkupContent()
                {
                    Kind = MarkupKind.Markdown,
                    Value = name.Value
                },
                Range = name.Range.ToLs()
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