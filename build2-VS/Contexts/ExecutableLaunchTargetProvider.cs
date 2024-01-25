using Microsoft.VisualStudio.Workspace.Debug;
using Microsoft.VisualStudio.Workspace;
using System;

namespace B2VS.Contexts
{
    [ExportLaunchDebugTarget(LaunchDebugTargetProviderOptions.IsRuntimeSupportContext, ProviderType, new[] { ".exe" }, ProviderPriority.Lowest)]
    internal class ExecutableLaunchTargetProvider : ILaunchDebugTargetProvider
    {
        public const string ProviderType = "{72D3FCEF-1111-4266-B8DD-D3ED06E35A2B}";

        public void LaunchDebugTarget(IWorkspace workspaceContext, IServiceProvider serviceProvider, DebugLaunchActionContext debugLaunchActionContext)
        {
            throw new NotImplementedException();
        }

        public bool SupportsContext(IWorkspace workspaceContext, string targetFilePath)
        {
            return false; // throw new NotImplementedException();
        }
    }
}
