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
            // when needed
            //[JsonInclude]
            //public List<Bpkg.Status.PackageStatus> packages = new List<Bpkg.Status.PackageStatus>();
        }
    }
}
