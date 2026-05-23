using System;
using System.Collections.Generic;
using System.IO;

namespace SharedLib.Config
{
    public static class EnvLoader
    {
        private static readonly object SyncRoot = new object();
        private static bool _loaded;
        private static readonly HashSet<string> LoadedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Load(string explicitPath = null, bool reload = false)
        {
            lock (SyncRoot)
            {
                if (_loaded && !reload)
                    return;

                if (reload)
                {
                    LoadedKeys.Clear();
                }

                string envPath = ResolveEnvPath(explicitPath);
                if (string.IsNullOrWhiteSpace(envPath) || !File.Exists(envPath))
                {
                    _loaded = true;
                    return;
                }

                foreach (string rawLine in File.ReadAllLines(envPath))
                {
                    if (string.IsNullOrWhiteSpace(rawLine))
                        continue;

                    string line = rawLine.Trim();
                    if (line.StartsWith("#"))
                        continue;

                    int sep = line.IndexOf('=');
                    if (sep <= 0)
                        continue;

                    string key = line.Substring(0, sep).Trim();
                    if (string.IsNullOrWhiteSpace(key))
                        continue;

                    string value = line.Substring(sep + 1).Trim();
                    value = Unquote(value);
                    Environment.SetEnvironmentVariable(key, value, EnvironmentVariableTarget.Process);
                    LoadedKeys.Add(key);
                }

                _loaded = true;
            }
        }

        public static string Get(string key, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(key))
                return fallback;

            string value = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        public static string GetRequired(string key)
        {
            string value = Get(key, "");
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Missing required environment variable: " + key);
            return value;
        }

        public static int GetInt(string key, int fallback)
        {
            string raw = Get(key, "");
            return int.TryParse(raw, out int value) ? value : fallback;
        }

        public static bool IsConfigured(string key)
        {
            return !string.IsNullOrWhiteSpace(Get(key, ""));
        }

        private static string ResolveEnvPath(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string full = Path.GetFullPath(explicitPath);
                if (File.Exists(full))
                    return full;
            }

            var bases = new List<string>();
            bases.Add(AppDomain.CurrentDomain.BaseDirectory);
            bases.Add(Directory.GetCurrentDirectory());

            foreach (string start in bases)
            {
                string current = start;
                for (int i = 0; i < 8 && !string.IsNullOrWhiteSpace(current); i++)
                {
                    string candidate = Path.Combine(current, ".env");
                    if (File.Exists(candidate))
                        return candidate;

                    DirectoryInfo parent = Directory.GetParent(current);
                    current = parent?.FullName;
                }
            }

            return null;
        }

        private static string Unquote(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return value;

            if ((value.StartsWith("\"") && value.EndsWith("\"")) ||
                (value.StartsWith("'") && value.EndsWith("'")))
            {
                return value.Substring(1, value.Length - 2);
            }

            return value;
        }
    }
}
