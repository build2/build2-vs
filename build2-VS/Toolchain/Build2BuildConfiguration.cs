using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;

namespace B2VS.Toolchain
{
    internal class Build2BuildConfiguration : BuildConfigurationContext // IBuildConfigurationContext
    {
        // @NOTE: Horrible as it is, param names here have to match (excluding case) the names of the properties (including the one inherited
        // from BuildConfigurationContext in order for Newtonsoft deserialization to work correctly (which is used internally by VS in the case
        // that this type is used for indexed data values). If they don't, it will just quietly fail to deserialize the right values.
        public Build2BuildConfiguration(string buildConfiguration, string configDir) : base(buildConfiguration)
        {
            ConfigDir = configDir;
        }

        public string ConfigDir { get; }
    }

    /// <summary>
    /// Associates a particular target file path to be built, with a build configuration it can be built in.
    /// </summary>
    internal class ContextualBuildConfiguration : IBuildConfigurationContext
    {
        public Build2BuildConfiguration Configuration { get; }
        public string BuildConfiguration { get { return Configuration.BuildConfiguration; } }
        public string TargetPath { get; }
        public ContextualBuildConfiguration(Build2BuildConfiguration cfg, string targetFilepath)
        {
            Configuration = cfg;
            TargetPath = targetFilepath;
        }
    }
}
