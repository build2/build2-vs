using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;

namespace B2VS.Contexts
{
    internal class Build2BuildConfiguration : IBuildConfigurationContext
    {
        public static readonly string PlaceholderBuildConfigName = "PlaceholderBuild2Config";

        // IBuildConfigurationContext interface
        public string BuildConfiguration { get { return PlaceholderBuildConfigName; } }
        // End IBuildConfigurationContext interface
    }
}
