using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2VS.Language.Manifest
{
    internal class Build2Manifest
    {
        public Build2Manifest(string ver, IReadOnlyDictionary<string, string> entries)
        {
            Version = ver;
            Entries = entries;
        }

        public string Version { get; }
        public IReadOnlyDictionary<string, string> Entries { get; }
    }
}
