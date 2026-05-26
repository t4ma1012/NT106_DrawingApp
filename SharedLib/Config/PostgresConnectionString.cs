using System;
using System.Collections.Generic;

namespace SharedLib.Config
{
    public static class PostgresConnectionString
    {
        public static string Normalize(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return string.Empty;

            string normalized = TrimMatchingQuotes(connectionString.Trim());
            if (normalized.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
            {
                return AddRuntimeDefaults(NormalizePostgresUri(normalized));
            }

            return AddRuntimeDefaults(NormalizeKeyValueConnectionString(normalized));
        }

        private static string AddRuntimeDefaults(string normalized)
        {
            var parts = new List<string>();
            bool hasTimeout = false;

            foreach (string segment in SplitSemicolonSegments(normalized))
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                parts.Add(segment);
                int separatorIndex = segment.IndexOf('=');
                string key = separatorIndex > 0 ? segment.Substring(0, separatorIndex).Trim() : segment.Trim();
                if (key.Equals("Timeout", StringComparison.OrdinalIgnoreCase))
                    hasTimeout = true;
            }

            if (!hasTimeout)
                parts.Add("Timeout=15");

            return string.Join(";", parts);
        }

        private static string NormalizePostgresUri(string connectionString)
        {
            var uri = new Uri(connectionString);
            var parts = new List<string>
            {
                FormatPart("Host", uri.Host)
            };

            if (!uri.IsDefaultPort && uri.Port > 0)
                parts.Add(FormatPart("Port", uri.Port.ToString()));

            if (!string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                string[] userParts = uri.UserInfo.Split(new[] { ':' }, 2);
                if (userParts.Length > 0 && !string.IsNullOrWhiteSpace(userParts[0]))
                    parts.Add(FormatPart("Username", Uri.UnescapeDataString(userParts[0])));

                if (userParts.Length > 1 && !string.IsNullOrWhiteSpace(userParts[1]))
                    parts.Add(FormatPart("Password", Uri.UnescapeDataString(userParts[1])));
            }

            string database = Uri.UnescapeDataString(uri.AbsolutePath.Trim('/'));
            if (!string.IsNullOrWhiteSpace(database))
                parts.Add(FormatPart("Database", database));

            foreach (var queryPart in ParseQuery(uri.Query))
            {
                string key = queryPart.Key;
                string value = queryPart.Value;

                if (IsChannelBindingKey(key))
                    continue;

                if (IsSslModeKey(key))
                {
                    parts.Add(FormatPart("SSL Mode", NormalizeSslMode(value)));
                    continue;
                }

                if (IsTrustServerCertificateKey(key))
                {
                    parts.Add(FormatPart("Trust Server Certificate", NormalizeBoolean(value)));
                    continue;
                }

                parts.Add(FormatPart(key, value));
            }

            return string.Join(";", parts);
        }

        private static string NormalizeKeyValueConnectionString(string connectionString)
        {
            var parts = new List<string>();
            foreach (string segment in SplitSemicolonSegments(connectionString))
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                int separatorIndex = segment.IndexOf('=');
                if (separatorIndex <= 0)
                    continue;

                string key = segment.Substring(0, separatorIndex).Trim();
                string value = TrimMatchingQuotes(segment.Substring(separatorIndex + 1).Trim());
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (IsChannelBindingKey(key))
                    continue;

                if (IsSslModeKey(key))
                {
                    parts.Add(FormatPart("SSL Mode", NormalizeSslMode(value)));
                    continue;
                }

                if (IsTrustServerCertificateKey(key))
                {
                    parts.Add(FormatPart("Trust Server Certificate", NormalizeBoolean(value)));
                    continue;
                }

                parts.Add(FormatPart(NormalizeKeyName(key), value));
            }

            return string.Join(";", parts);
        }

        private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                yield break;

            string trimmed = query.TrimStart('?');
            foreach (string rawSegment in trimmed.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int separatorIndex = rawSegment.IndexOf('=');
                if (separatorIndex < 0)
                {
                    string keyOnly = UnescapeQueryValue(rawSegment.Trim());
                    if (IsSslModeKey(keyOnly))
                        yield return new KeyValuePair<string, string>(keyOnly, "Require");
                    continue;
                }

                string key = UnescapeQueryValue(rawSegment.Substring(0, separatorIndex).Trim());
                string value = UnescapeQueryValue(rawSegment.Substring(separatorIndex + 1).Trim());
                if (!string.IsNullOrWhiteSpace(key))
                    yield return new KeyValuePair<string, string>(key, value);
            }
        }

        private static IEnumerable<string> SplitSemicolonSegments(string connectionString)
        {
            int start = 0;
            char quote = '\0';
            for (int i = 0; i < connectionString.Length; i++)
            {
                char ch = connectionString[i];
                if ((ch == '\'' || ch == '"') && (i == 0 || connectionString[i - 1] != '\\'))
                {
                    quote = quote == '\0' ? ch : (quote == ch ? '\0' : quote);
                }
                else if (ch == ';' && quote == '\0')
                {
                    yield return connectionString.Substring(start, i - start);
                    start = i + 1;
                }
            }

            if (start <= connectionString.Length)
                yield return connectionString.Substring(start);
        }

        private static string NormalizeKeyName(string key)
        {
            if (key.Equals("User ID", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("UserID", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("User", StringComparison.OrdinalIgnoreCase))
                return "Username";

            if (key.Equals("SslMode", StringComparison.OrdinalIgnoreCase))
                return "SSL Mode";

            if (key.Equals("TrustServerCertificate", StringComparison.OrdinalIgnoreCase))
                return "Trust Server Certificate";

            return key;
        }

        private static string NormalizeSslMode(string value)
        {
            string cleaned = (value ?? string.Empty).Trim().Replace("-", "").Replace(" ", "");
            if (string.IsNullOrWhiteSpace(cleaned))
                return "Require";

            if (cleaned.Equals("disable", StringComparison.OrdinalIgnoreCase))
                return "Disable";
            if (cleaned.Equals("allow", StringComparison.OrdinalIgnoreCase))
                return "Allow";
            if (cleaned.Equals("prefer", StringComparison.OrdinalIgnoreCase))
                return "Prefer";
            if (cleaned.Equals("require", StringComparison.OrdinalIgnoreCase))
                return "Require";
            if (cleaned.Equals("verifyca", StringComparison.OrdinalIgnoreCase))
                return "VerifyCA";
            if (cleaned.Equals("verifyfull", StringComparison.OrdinalIgnoreCase))
                return "VerifyFull";

            return value;
        }

        private static string NormalizeBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "true";

            if (value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("yes", StringComparison.OrdinalIgnoreCase))
                return "true";

            if (value.Equals("0", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("no", StringComparison.OrdinalIgnoreCase))
                return "false";

            return value.ToLowerInvariant();
        }

        private static bool IsSslModeKey(string key)
        {
            return key.Equals("sslmode", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("SSL Mode", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("SslMode", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrustServerCertificateKey(string key)
        {
            return key.Equals("Trust Server Certificate", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("TrustServerCertificate", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsChannelBindingKey(string key)
        {
            return key.Equals("Channel Binding", StringComparison.OrdinalIgnoreCase) ||
                   key.Equals("ChannelBinding", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatPart(string key, string value)
        {
            return key + "=" + EscapeValue(value ?? string.Empty);
        }

        private static string EscapeValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            bool needsQuotes =
                value.IndexOf(';') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0 ||
                value.StartsWith(" ", StringComparison.Ordinal) ||
                value.EndsWith(" ", StringComparison.Ordinal);

            return needsQuotes ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
        }

        private static string TrimMatchingQuotes(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2)
                return value;

            char first = value[0];
            char last = value[value.Length - 1];
            if ((first == '"' && last == '"') || (first == '\'' && last == '\''))
                return value.Substring(1, value.Length - 2);

            return value;
        }

        private static string UnescapeQueryValue(string value)
        {
            return Uri.UnescapeDataString((value ?? string.Empty).Replace("+", " "));
        }
    }
}
