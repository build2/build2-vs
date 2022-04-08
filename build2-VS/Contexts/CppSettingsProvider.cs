using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Composition;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Settings;

namespace B2VS.Contexts
{
    [Export(typeof(IWorkspaceSettingsProviderFactory))]
    internal class CppSettingsProviderFactory : IWorkspaceSettingsProviderFactory
    {
        // 100 is typically the value used by built-in settings providers. Lower value is higher priority.
        public int Priority => 100;

        public IWorkspaceSettingsProvider CreateSettingsProvider(IWorkspace workspace) => new CppSettingsProvider(workspace);

        private class CppSettingsSource : IWorkspaceSettingsSource
        {
            IEnumerable<string> IWorkspaceSettingsSource.GetKeys()
            {
                return new List<string>();
            }

            WorkspaceSettingsResult IWorkspaceSettingsSource.GetProperty<T>(string key, out T value, T defaultValue)
            {
                throw new NotImplementedException();
            }
        }

        private class CppSettingsProvider : IWorkspaceSettingsProvider
        {
            private IWorkspace workspaceContext;

            internal CppSettingsProvider(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            AsyncEvent<WorkspaceSettingsChangedEventArgs> IWorkspaceSettingsProvider.OnWorkspaceSettingsChanged { get; set; }

            Task IWorkspaceSettingsProvider.DisposeAsync()
            {
                return Task.CompletedTask;
            }

            IWorkspaceSettingsSource IWorkspaceSettingsProvider.GetSingleSettings(string type, string scopePath)
            {
                if (type == "CppProperties")
                {
                    return new CppSettingsSource();
                }

                return null;
            }
        }
    }
}
