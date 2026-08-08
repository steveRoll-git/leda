using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Client.ClientCapabilities;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server;
using EmmyLua.LanguageServer.Framework.Protocol.Capabilities.Server.Options;
using EmmyLua.LanguageServer.Framework.Protocol.Message.DocumentDiagnostic;
using EmmyLua.LanguageServer.Framework.Server.Handler;

namespace Leda.LSP;

public class DocumentDiagnosticHandler(LedaServer server) : DocumentDiagnosticHandlerBase
{
    protected override Task<DocumentDiagnosticReport> Handle(DocumentDiagnosticParams request, CancellationToken token)
    {
        var resultId = server.Project.EditVersion.ToString();
        if (request.PreviousResultId == resultId)
        {
            return Task.FromResult<DocumentDiagnosticReport>(new RelatedUnchangedDocumentDiagnosticReport
            {
                ResultId = resultId,
            });
        }

        var source = server.GetSourceByUri(request.TextDocument.Uri);

        var diagnostics = server.Project.GetDiagnostics(source);
        return Task.FromResult<DocumentDiagnosticReport>(new RelatedFullDocumentDiagnosticReport
        {
            Diagnostics = diagnostics.Select(d => d.ToLs()).ToList(),
            ResultId = resultId,
        });
    }

    public override void RegisterCapability(ServerCapabilities serverCapabilities,
        ClientCapabilities clientCapabilities)
    {
        serverCapabilities.DiagnosticProvider = new DiagnosticOptions { InterFileDependencies = true };
    }
}