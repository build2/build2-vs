using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace.Build;
using BCLConfig = B2VS.Toolchain.Json.Bdep.Config.List.Configuration;
using BSConfigStatus = B2VS.Toolchain.Json.Bdep.Status.ConfigurationPackageStatus;
using B2VS.Exceptions;
using B2VS.Utilities;

namespace B2VS.Toolchain
{
    internal static class Build2Configs
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="path">Must be the exact path of a bdep project folder</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal static async Task<IReadOnlyCollection<Build2BuildConfiguration>> EnumerateBuildConfigsForProjectPathAsync(string path, CancellationToken cancellationToken)
        {
            var configListOutput = "";
            {
                var args = new string[] { "config", "list", "--stdout-format", "json", "-d", path };
                Action<string> outputHandler = (string line) => configListOutput += line;
                var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler);
                if (exitCode != 0)
                {
                    throw new Exception("'bdep config list' failed");
                }
            }

            try
            {
                Func<BCLConfig, Build2BuildConfiguration> convertConfig = (BCLConfig configJson) =>
                {
                    return new Build2BuildConfiguration(configJson.name ?? configJson.path, configJson.path);
                };

                var configsJson = JsonUtils.StrictDeserialize<List<BCLConfig>>(configListOutput);
                return configsJson.Select(convertConfig).ToList();
            }
            catch (Exception ex)
            {
                OutputUtils.OutputWindowPaneAsync("'bdep config list' json output not parseable");
                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="packagePath">Must be the exact path of a package folder within a bdep project</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal static async Task<IReadOnlyCollection<Build2BuildConfiguration>> EnumerateBuildConfigsForPackagePathAsync(
            string packagePath,
            CancellationToken cancellationToken)
        {
            var jsonOutput = "";
            {
                var args = new string[] { "status", "-d", packagePath, "-a", "--stdout-format", "json" };
                Action<string> outputHandler = (string line) => jsonOutput += line;
                var errors = "";
                Action<string> errorHandler = (string line) => errors += line;
                var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler, stdErrHandler: errorHandler);
                if (exitCode != 0)
                {
                    throw new InvalidPackageException(string.Format("'bdep status' failed with: {0}", errors));
                }
            }

            try
            {
                Func<BSConfigStatus, Build2BuildConfiguration> convertConfigStatus = (BSConfigStatus configStatusJson) =>
                {
                    return new Build2BuildConfiguration(configStatusJson.configuration.name ?? configStatusJson.configuration.path, configStatusJson.configuration.path);
                };

                var configStatusesJson = JsonUtils.StrictDeserialize<List<BSConfigStatus>>(jsonOutput);
                // @TODO: don't think build2 actually requires the package folder name to match the package name??
                // need to refactor to always pass in package name if possible.
                var packageName = System.IO.Path.GetFileName(packagePath);

                return configStatusesJson
                    // Filter to configurations in which this package exists and is configured.
                    .Where(cfgStatus => cfgStatus.packages.Any(pkgStatus => string.Compare(pkgStatus.name, packageName, StringComparison.OrdinalIgnoreCase) == 0 && pkgStatus.status == "configured"))
                    .Select(convertConfigStatus).ToList();
            }
            catch (Exception ex)
            {
                OutputUtils.OutputWindowPaneAsync("'bdep status' json output not parseable");
                throw;
            }
        }
    }
}
