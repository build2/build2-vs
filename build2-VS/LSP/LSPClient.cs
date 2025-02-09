using B2VS.Workspace;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Settings;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2VS.LSP
{
    [ContentType("buildfile-like")]
    [ContentType("manifest")]
    [Export(typeof(ILanguageClient))]
    public class Build2LanguageClient : ILanguageClient
    {
        public string Name => "Build2 Language Extension";

        public IEnumerable<string> ConfigurationSections
        {
            get
            {
                yield return "build2"; // @NOTE: Matches to prefix used in Build2VSDefaults.json and/or? Build2Extension.pkgdef
            }
        }

        public object InitializationOptions => null;

        public IEnumerable<string> FilesToWatch => null;

        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs> StartAsync;
        public event AsyncEventHandler<EventArgs> StopAsync;

        private string _lastUsedServerPath = null;

        private readonly IVsFolderWorkspaceService _workspaceService;
        private readonly SVsServiceProvider _serviceProvider;

        [ImportingConstructor]
        public Build2LanguageClient([Import] IVsFolderWorkspaceService workspaceService, [Import] SVsServiceProvider serviceProvider)
        {
            _workspaceService = workspaceService;
            _serviceProvider = serviceProvider;
        }

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            await Task.Yield();

            string serverPath = GetServerPathFromConfigurationSettings();
            var config = GetServerConfigurationSettings();
            config.GetProperty("showConsole", out bool showConsole, false);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = serverPath;
            info.Arguments = "";
            info.RedirectStandardInput = true;
            info.RedirectStandardOutput = true;
            info.UseShellExecute = false;
            info.CreateNoWindow = !showConsole;

            Process process = new Process();
            process.StartInfo = info;

            if (process.Start())
            {
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        public async Task OnLoadedAsync()
        {
            var workspace = _workspaceService.CurrentWorkspace;
            var settingsMgr = await workspace.GetSettingsManagerAsync();
            settingsMgr.OnWorkspaceSettingsChanged += async (object sender, Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsChangedEventArgs args) =>
            {
                string lspServerPath = GetServerPathFromConfigurationSettings();

                if (lspServerPath != _lastUsedServerPath)
                {
                    await StopAsync.InvokeAsync(this, EventArgs.Empty);

                    _lastUsedServerPath = lspServerPath;
                    if (lspServerPath != null)
                    {
                        await StartAsync.InvokeAsync(this, EventArgs.Empty);
                    }
                }

                // @todo: would also be useful to add a watch on the file at the specified server path, and reload if it changes (after server rebuild during dev).
            };

            await StartAsync.InvokeAsync(this, EventArgs.Empty);
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }

        public async Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            await Task.Yield();
            return new InitializationFailureContext
            {
                FailureMessage = initializationState.StatusMessage
            };
        }

        private IWorkspaceSettings GetServerConfigurationSettings()
        {
            var workspace = _workspaceService.CurrentWorkspace;
            var settings = Build2Settings.Get(workspace);
            var result = settings.GetProperty("lsp", out IWorkspaceSettings lspSettings);
            return lspSettings;
        }

        private string GetServerPathFromConfigurationSettings()
        {
            var settings = GetServerConfigurationSettings();
            string lspServerPath = null;
            if (settings != null)
            {
                settings.GetProperty("serverPath", out lspServerPath);
            }
            return lspServerPath;
        }
    }
}
