using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace B2VS.Toolchain.Json
{
    namespace Bdep.Config.List
    {
        internal class Configuration
        {
            internal class Package
            {
                [JsonInclude]
                public string name;
            }

            [JsonInclude]
            public ulong id;
            [JsonInclude]
            public string path;
            [JsonInclude]
            public string name;
            [JsonInclude]
            public string type;
            [JsonInclude, JsonPropertyName("default")]
            public bool is_default;
            [JsonInclude]
            public bool forward;
            [JsonInclude]
            public bool auto_sync;
            [JsonInclude]
            public List<Package> packages = new List<Package>();
        }
    }

    namespace Bpkg.Status
    {
        internal class PackageStatus
        {
            [JsonInclude]
            public string name;
            [JsonInclude]
            public string status; // @todo: define and convert to enum
            //[JsonInclude]
            //public string version;
            //"hold_package": true,
            //"hold_version": true,
            //"available_versions": [
            //  {
            //    "version": "0.1.0-a.0.20230407095419"
            //  }
            //]
        }
    }

    namespace Bdep.Status
    {
        internal class ConfigurationPackageStatus
        {
            internal class Configuration
            {
                [JsonInclude]
                public ulong id;
                [JsonInclude]
                public string path;
                [JsonInclude]
                public string name;
            }

            [JsonInclude]
            public Configuration configuration;
            [JsonInclude]
            public List<Bpkg.Status.PackageStatus> packages = new List<Bpkg.Status.PackageStatus>();
        }
    }
}
