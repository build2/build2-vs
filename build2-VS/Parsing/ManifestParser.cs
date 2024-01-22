using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using B2VS.Language.Manifest;

namespace B2VS.Parsing
{
    internal static class ManifestParsing
    {
        private static List<string> CollapseEscapedNewlines(List<string> lines)
        {
            var result = new List<string>();
            string collapsed = string.Empty;
            foreach (var line in lines)
            {
                collapsed += line;
                if (collapsed.EndsWith("\\"))
                {
                    collapsed = collapsed.Substring(0, collapsed.Length - 1);
                }
                else
                {
                    result.Add(collapsed);
                    collapsed = string.Empty;
                }
            }
            return result;
        }

        private static async Task<IEnumerable<string>> PreParseManifestListAsync(StreamReader stream, CancellationToken cancellationToken)
        {
            var lines = new List<string>();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stream.ReadLineAsync();
                if (line == null)
                {
                    break;
                }

                lines.Add(line);
            }
            var result = CollapseEscapedNewlines(lines).Select(line => line.Trim());
            return result.Where(line => !line.StartsWith("#") && !string.IsNullOrWhiteSpace(line));
        }

        // @TODO: Handle multi-line mode
        // https://build2.org/bpkg/doc/build2-package-manager-manual.xhtml#manifest-format

        private static KeyValuePair<string, string> ParseManifestEntry(string line)
        {
            var split = line.Split(new char[] { ':' }, 2);
            if (split.Length != 2)
            {
                throw new ArgumentException();
            }
            var key = split[0].Trim();
            var value = split[1].Trim();

            // @todo: how to handle ; comments?

            return new KeyValuePair<string, string>(key, value);
        }

        private static string ParseManifestSeparatorAndVersion(string line)
        {
            var entry = ParseManifestEntry(line);
            
            // Expect first entry to be the : [version] special entry.
            if (entry.Key != string.Empty)
            {
                throw new ArgumentException();
            }

            // Return value as optional version.
            return entry.Value == string.Empty ? null : entry.Value;
        }

        // Returns true if the enumerator still has entries to process; false otherwise.
        // @NOTE: Expects enumerator is already pointing at a valid entry.
        private static bool ParseManifest(IEnumerator<string> enumerator, out Build2Manifest manifest, string defaultVersion = null)
        {
            var version = ParseManifestSeparatorAndVersion(enumerator.Current);
            if (version == null)
            {
                if (defaultVersion == null)
                {
                    throw new ArgumentException();
                }
                else
                {
                    version = defaultVersion;
                }
            }

            // Parse entries.
            var entries = new List<KeyValuePair<string, string>>();
            bool hitNextManifest = false;
            while (enumerator.MoveNext())
            {
                var entry = ParseManifestEntry(enumerator.Current);
                if (entry.Key == string.Empty)
                {
                    // Hit the next manifest section, time to bail.
                    hitNextManifest = true;
                    break;
                }
                entries.Add(entry);
            }

            manifest = new Build2Manifest(version, entries);
            return hitNextManifest;
        }

        public static async Task<IEnumerable<Build2Manifest>> ParseManifestListAsync(StreamReader stream, CancellationToken cancellationToken)
        {
            var lines = await PreParseManifestListAsync(stream, cancellationToken);
            var manifests = new List<Build2Manifest>();
            var enumerator = lines.GetEnumerator();
            if (enumerator.MoveNext())
            {
                string version = null;
                while (true)
                {
                    Build2Manifest m;
                    bool more = ParseManifest(enumerator, out m, version);
                    manifests.Add(m);

                    if (!more)
                    {
                        break;
                    }

                    // Inherit version as default
                    // @todo: is a second version specification actually invalid?
                    version = m.Version;
                }
            }
            return manifests;
        }

        public static async Task<Build2Manifest> ParseSingleManifestAsync(StreamReader stream, CancellationToken cancellationToken)
        {
            var manifests = await ParseManifestListAsync(stream, cancellationToken);
            if (manifests.Count() != 1)
            {
                throw new Exception("Manifest parsing failed");
            }

            return manifests.First();
        }
    }
}
