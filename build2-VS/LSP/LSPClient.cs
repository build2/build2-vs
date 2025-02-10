using B2VS.Workspace;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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

        private static readonly string failureInfoBarHelpContext = "helpContext";
        private static readonly string build2VSExtensionReadmeURL = "https://github.com/build2/build2-vs/blob/main/README.md";

        class InfoBarService : IVsInfoBarUIEvents
        {
            IVsInfoBarUIElement _infoBar = null;
            uint _cookie = 0;

            public InfoBarService(IVsInfoBarUIElement infoBar)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                _infoBar = infoBar;
                _infoBar.Advise(this, out _cookie);
            }

            public IVsInfoBarUIElement GetInfoBarElement()
            {
                return _infoBar;
            }

            public void OnClosed(IVsInfoBarUIElement infoBarUIElement)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                infoBarUIElement.Unadvise(_cookie);
            }

            public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem)
            {
                ThreadHelper.ThrowIfNotOnUIThread();

                string context = (string)actionItem.ActionContext;
                
                if (context == failureInfoBarHelpContext)
                {
                    Process.Start(new ProcessStartInfo(build2VSExtensionReadmeURL) { UseShellExecute = true });
                }
            }
        }

        private Process _activeServer = null;
        private string _lastUsedServerPath = null; // @todo: perhaps can dump this and query the above process object instead
        private InfoBarService _failureInfoBar = null;

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
            Debug.Assert(_activeServer == null);

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
                await OutputUtils.OutputWindowPaneAsync("build2-lsp-server launched");

                _activeServer = process;
                return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
            }

            return null;
        }

        private bool isServerRunning()
        {
            return _activeServer != null;
        }

        private bool TryCreateInfoBarUI(IVsInfoBar infoBar, out IVsInfoBarUIElement uiElement)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsInfoBarUIFactory infoBarUIFactory = ServiceProvider.GlobalProvider.GetService(typeof(SVsInfoBarUIFactory)) as IVsInfoBarUIFactory;
            if (infoBarUIFactory == null)
            {
                uiElement = null;
                return false;
            }

            uiElement = infoBarUIFactory.CreateInfoBar(infoBar);
            return uiElement != null;
        }

        private bool TryGetInfoBarHost(out IVsInfoBarHost host)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var vsUiShell = ServiceProvider.GlobalProvider.GetService(typeof(SVsShell)) as IVsShell;
            object infoBarHostObj;
            if (!ErrorHandler.Failed(vsUiShell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out infoBarHostObj)))
            {
                host = infoBarHostObj as IVsInfoBarHost;
                return true;
            }
            host = null;
            return false;
        }

        private void UpdateFailureInfoBar(InfoBarModel newContent)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (TryGetInfoBarHost(out IVsInfoBarHost infoBarHost))
            {
                // Remove existing
                if (_failureInfoBar != null)
                {
                    infoBarHost.RemoveInfoBar(_failureInfoBar.GetInfoBarElement());
                    _failureInfoBar = null;
                }

                // Optionally add new info bar
                if (newContent != null)
                {
                    if (TryCreateInfoBarUI(newContent, out IVsInfoBarUIElement infoBar))
                    {
                        infoBarHost.AddInfoBar(infoBar);
                        _failureInfoBar = new InfoBarService(infoBar);
                        return;
                    }
                }
            }
        }

        private async Task RecheckConfigAndStartIfValidAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string lspServerPath = GetServerPathFromConfigurationSettings();

            if (File.Exists(lspServerPath))
            {
                UpdateFailureInfoBar(null);

                await StartAsync.InvokeAsync(this, EventArgs.Empty);
                _lastUsedServerPath = lspServerPath;
            }
            else
            {
                string msg;
                if (lspServerPath == "")
                {
                    msg = "No build2 LSP server path specified in Build2VS.json.";
                }
                else
                {
                    msg = string.Format("Invalid path to build2 LSP specified in Build2VS.json: '{0}'.", lspServerPath);
                }

                InfoBarTextSpan textSpan1 = new InfoBarTextSpan(msg + " ");
                InfoBarHyperlink helpLink = new InfoBarHyperlink("Help", failureInfoBarHelpContext);
                
                InfoBarTextSpan[] textSpanCollection = new InfoBarTextSpan[] { textSpan1, helpLink };
                InfoBarModel infoBarModel = new InfoBarModel(textSpanCollection, KnownMonikers.StatusInformation, isCloseButtonVisible: true);

                UpdateFailureInfoBar(infoBarModel);
            }
        }

        public async Task OnLoadedAsync()
        {
            var workspace = _workspaceService.CurrentWorkspace;
            var settingsMgr = await workspace.GetSettingsManagerAsync();
            settingsMgr.OnWorkspaceSettingsChanged += async (object sender, WorkspaceSettingsChangedEventArgs args) =>
            {
                if (args.Type == Build2Settings.SettingsName)
                {
                    // @todo: use args to check if the server path specifically was changed?

                    if (isServerRunning())
                    {
                        string lspServerPath = GetServerPathFromConfigurationSettings();
                        if (lspServerPath != _lastUsedServerPath)
                        {
                            await StopAsync.InvokeAsync(this, EventArgs.Empty);
                        }
                    }

                    await RecheckConfigAndStartIfValidAsync();
                }

                // @todo: would also be useful to add a watch on the file at the specified server path, and reload if it changes (after server rebuild during dev).
            };

            await RecheckConfigAndStartIfValidAsync();
        }

        public async Task OnServerInitializedAsync()
        {
            await OutputUtils.OutputWindowPaneAsync("build2-lsp-server successfully initialized");
        }

        public async Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo initializationState)
        {
            await Task.Yield();

            _activeServer = null;
            
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
            string lspServerPath = "";
            if (settings != null)
            {
                settings.GetProperty("serverPath", out lspServerPath, "");
            }
            return lspServerPath;
        }
    }
}
