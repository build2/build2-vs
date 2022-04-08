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

            var configsJson = JsonSerializer.Deserialize<List<BCLConfig>>(configListOutput);
            if (configsJson == null)
            {
                throw new Exception("'bdep config list' json output not parseable");
            }

            Func<BCLConfig, Build2BuildConfiguration> convertConfig = (BCLConfig configJson) =>
            {
                return new Build2BuildConfiguration(configJson.name ?? configJson.path, configJson.path);
            };

            return configsJson.Select(convertConfig).ToList();
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
                var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(args, cancellationToken, stdOutHandler: outputHandler);
                if (exitCode != 0)
                {
                    throw new Exception("'bdep status' failed");
                }
            }

            var configStatusesJson = JsonSerializer.Deserialize<List<BSConfigStatus>>(jsonOutput);
            if (configStatusesJson == null)
            {
                throw new Exception("'bdep status' json output not parseable");
            }

            Func<BSConfigStatus, Build2BuildConfiguration> convertConfigStatus = (BSConfigStatus configStatusJson) =>
            {
                return new Build2BuildConfiguration(configStatusJson.configuration.name ?? configStatusJson.configuration.path, configStatusJson.configuration.path);
            };

            return configStatusesJson.Select(convertConfigStatus).ToList();
        }
    }
}
