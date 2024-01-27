using Microsoft.VisualStudio.Workspace.Debug;
using Microsoft.VisualStudio.Workspace;
using System;
using Microsoft.VisualStudio.RpcContracts.Settings;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;
using static Microsoft.VisualStudio.VSConstants;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using B2VS.Toolchain;
using B2VS.Workspace;
using System.Threading;
using B2VS.ProjectModel;
using System.Linq;
using B2VS.VSPackage;
using System.Diagnostics;

namespace B2VS.Contexts
{
    [ExportLaunchDebugTarget(LaunchDebugTargetProviderOptions.IsRuntimeSupportContext, ProviderType, new[] { ".exe" }, ProviderPriority.Lowest)]
    internal class ExecutableLaunchTargetProvider : ILaunchDebugTargetProvider2
    {
        public const string ProviderType = "{72D3FCEF-1111-4266-B8DD-D3ED06E35A2B}";

        public void LaunchDebugTarget(IWorkspace workspaceContext, IServiceProvider serviceProvider, DebugLaunchActionContext debugLaunchActionContext)
        {
            workspaceContext.JTF.Run(async() => await LaunchDebugTargetAsync(workspaceContext, serviceProvider, debugLaunchActionContext));
        }

        public bool SupportsContext(IWorkspace workspaceContext, string targetFilePath)
        {
            return Path.GetFileName(targetFilePath) == Build2Constants.BuildfileFilename; // throw new NotImplementedException();
        }

        public bool SupportsProjectConfiguration(IWorkspace workspaceContext, ProjectConfiguration projectConfig)
        {
            return true;
        }

        private async Task LaunchDebugTargetAsync(IWorkspace workspaceContext, IServiceProvider serviceProvider, DebugLaunchActionContext debugLaunchActionContext)
        {
            try
            {
                var fileContext = debugLaunchActionContext.ProjectFileContext;

                var buildSvc = await workspaceContext.GetBuildServiceAsync();

                var absBuildfilePath = workspaceContext.MakeRooted(fileContext.FilePath);
                var configService = await workspaceContext.GetProjectConfigurationServiceAsync();
                var buildConfigId = configService.GetActiveProjectBuildConfiguration(fileContext);

                var upToDateActionCtx1 = await buildSvc.GetBuildUpToDateActionContextAsync(fileContext.FilePath, fileContext.Target, null /*todo*/);
                var upToDateActionCtx2 = await buildSvc.GetBuildUpToDateActionContextAsync(fileContext.FilePath, fileContext.Target, buildConfigId);

                var packagePath = await Build2Workspace.GetContainingPackagePathAsync(workspaceContext, absBuildfilePath);
                var configurations = await ProjectConfigUtils.GetIndexedBuildConfigurationsForPathAsync(packagePath, workspaceContext);
                var config = configurations.Where(cfg => cfg.BuildConfiguration == buildConfigId).FirstOrDefault();
                if (config == null)
                {
                    Build2Toolchain.DebugHandler?.Invoke(string.Format("Error: unexpected failure to match build configuration from debug launch. Aborting."));
                    return;
                }

                var index = await workspaceContext.GetIndexWorkspaceServiceAsync();
                var targetDataValues = await index.GetFileDataValuesAsync<Toolchain.Json.B.DumpLoad.BuildLoadStatus.Target>(
                    fileContext.FilePath,
                    PackageIds.Build2BuildTargetDataValueTypeGuid,
                    target: fileContext.Target);
                var targetDataValue = targetDataValues.First(dv => dv.Name == PackageIds.Build2BuildTargetDataValueName);
                Debug.Assert(targetDataValue.Target == fileContext.Target);
                // @todo: recreating this path here is not ideal. should probably either store the config relative target outfile path on the target structure in the 
                // data value, or create a data value per target-config pair with the complete path.
                var pkgName = new DirectoryInfo(packagePath).Name;
                var packageRelativeBuildfilePath = PathUtils.GetRelativePath(packagePath + '/', absBuildfilePath);
                var binPath = Path.Combine(config.ConfigDir, pkgName, Path.GetDirectoryName(packageRelativeBuildfilePath), targetDataValue.Value.OutFileTitle) + ".exe";

                //var binPath = debugLaunchActionContext.ProjectFileContext.Target;

                //var mds = workspaceContext.GetService<IMetadataService>();
                //var package = await mds.GetContainingPackageAsync((PathEx)lcw[LaunchConfigurationConstants.ProgramKey], default);
                //var profile = workspaceContext.GetProfile(package.ManifestPath);
                //var targetFQN = lcw[LaunchConfigurationConstants.NameKey];
                //var target = package.GetTargets().FirstOrDefault(t => t.QualifiedTargetFileName == targetFQN);
                //if (target == null)
                //{
                //    string message = string.Format("Cannot find target '{0}' in '{1}', for profile '{2}'. This indicates a bug in the manifest parsing logic. Unable to start debugging.", targetFQN, package?.FullPath, profile);
                //    L.WriteError(message);
                //    T.TrackException(new ArgumentOutOfRangeException("target", message));
                //    await VsCommon.ShowMessageBoxAsync(message, "Try again after deleting the .vs folder. If that does not work please file a bug.");
                //    return;
                //}

                //var processName = target.GetPath(profile);
                //if (!File.Exists(processName))
                //{
                //    var message = string.Format("Unable to find file: '{0}'. This indicates a bug with the Manifest parsing logic. Unable to start debugging.", processName);
                //    L.WriteLine(message);
                //    T.TrackException(new FileNotFoundException(message, processName));
                //    await VsCommon.ShowMessageBoxAsync(message, "Try again after deleting the .vs folder. If that does not work please file a bug.");
                //    return;
                //}

                //var args = await GetSettingsAsync(SettingsInfo.TypeCommandLineArguments, workspaceContext.GetService<ISettingsService>(), lcw);
                var args = "foo";
                //var env = await GetSettingsAsync(SettingsInfo.TypeDebuggerEnvironment, workspaceContext.GetService<ISettingsService>(), lcw);
                //var workingDirectory = await GetSettingsAsync(SettingsInfo.TypeDebuggerWorkingDirectory, workspaceContext.GetService<ISettingsService>(), lcw);
                //var noDebugFlag = lcw.ContainsKey(LaunchConfigurationConstants.NoDebugKey) ? __VSDBGLAUNCHFLAGS.DBGLAUNCH_NoDebug : 0;

                var info = new VsDebugTargetInfo
                {
                    dlo = DEBUG_LAUNCH_OPERATION.DLO_CreateProcess,
                    bstrExe = binPath,
                    //bstrCurDir = workingDirectory.IsNullOrEmpty() ? Path.GetDirectoryName(processName) : workingDirectory,
                    bstrArg = args,
                    //bstrEnv = env.OverrideProcessEnvironment()
                    //    .PrependToPathInEnviroment(
                    //        package.GetDepsPath(profile),
                    //        package.GetTargetPath(profile),
                    //        ToolChainServiceExtensions.GetLibPath(),
                    //        ToolChainServiceExtensions.GetBinPath()).ToEnvironmentBlock(),
                    bstrOptions = null,
                    bstrPortName = null,
                    bstrMdmRegisteredName = null,
                    bstrRemoteMachine = null,
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<VsDebugTargetInfo>(),
                    grfLaunch = (uint)(/*noDebugFlag |*/ __VSDBGLAUNCHFLAGS.DBGLAUNCH_Silent | __VSDBGLAUNCHFLAGS.DBGLAUNCH_StopDebuggingOnEnd),
                    fSendStdoutToOutputWindow = 0,
                    clsidCustom = DebugEnginesGuids.NativeOnly_guid,
                };

                VsShellUtilities.LaunchDebugger(serviceProvider, info);
            }
            catch (Exception e)
            {
                Build2Toolchain.DebugHandler?.Invoke("Error: unknown failure attempting to launch debug target");
                throw;
            }
        }
    }
}
