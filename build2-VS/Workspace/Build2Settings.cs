﻿using Microsoft.VisualStudio.Workspace.Settings;
using Microsoft.VisualStudio.Workspace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2VS.Workspace
{
    public static class Build2Settings
    {
        const string SettingsName = "Build2VS";

        public static IWorkspaceSettings get(IWorkspace workspace)
        {
            return workspace.GetSettingsManager().GetAggregatedSettings(SettingsName);
        }
    }
}
