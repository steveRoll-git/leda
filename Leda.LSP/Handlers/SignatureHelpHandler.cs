using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Message.SignatureHelp;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace Leda.LSP;

public class SignatureHelpHandler(LedaServer server) : SignatureHelpHandlerBase
{
    protected override Task<SignatureHelp> Handle(SignatureHelpParams request, CancellationToken token)
    {
        var source = server.GetSourceByUri(request.TextDocument.Uri);
        // The source's syntax tree and types need to be up tp date for signature help to work properly.
        server.Project.Check(source);

        var position = request.Position.ToLeda();
        if (CallFinder.FindCall(source, position) is { } result)
        {
            return Task.FromResult(new SignatureHelp
            {
                Signatures =
                [
                    new() { Label = result.FunctionName + result.ArgumentIndex }, // TODO
                ],
            });
        }

        return Task.FromResult<SignatureHelp?>(null)!;
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities, ClientCapabilities clientCapabilities)
    {
        serverCapabilities.SignatureHelpProvider = new()
        {
            TriggerCharacters = ["("],
        };
    }
}