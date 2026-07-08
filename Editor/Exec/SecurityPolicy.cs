using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityExec
{
    /// <summary>
    /// Whitelist-based security policy for C# code execution.
    /// Only allows code that uses approved namespaces.
    /// Blocks known dangerous patterns regardless of whitelist.
    /// </summary>
    public static class SecurityPolicy
    {
        public const int MAX_CODE_SIZE_BYTES = 10240; // 10KB

        /// <summary>
        /// Default whitelist of allowed namespace prefixes.
        /// Any 'using' directive must start with one of these.
        /// </summary>
        static readonly string[] s_DefaultWhitelist = new[]
        {
            "UnityEngine",
            "UnityEditor",
            "System.Linq",
            "System.Collections",
            "System.Collections.Generic",
            "System.Text",
            "System.Text.RegularExpressions",
            "System.Math",
            "System.Convert",
            "System.String",
            "System.Array",
            "System.Enum",
            "System.Guid",
            "System.TimeSpan",
            "System.DateTime",
            "System.Globalization",
            "System.Runtime.CompilerServices",
        };

        /// <summary>
        /// Exact-match namespaces: the "using System;" is always injected by
        /// the executor's default usings, so we allow it implicitly.
        /// But "System.IO", "System.Net", "System.Diagnostics" etc. are NOT allowed
        /// unless explicitly added to the whitelist.
        /// </summary>
        static readonly HashSet<string> s_ExactOnlyNamespaces = new HashSet<string>
        {
            "System"
        };

        /// <summary>
        /// Patterns that are ALWAYS blocked even if namespace is whitelisted.
        /// These represent dangerous operations that should never be executed remotely.
        /// </summary>
        static readonly Regex[] s_ForbiddenPatterns = new[]
        {
            // Process/OS execution
            new Regex(@"\bProcess\s*\.\s*Start\b", RegexOptions.Compiled),
            new Regex(@"\bProcessStartInfo\b", RegexOptions.Compiled),
            new Regex(@"\bEnvironment\s*\.\s*Exit\b", RegexOptions.Compiled),

            // Dynamic assembly loading / code generation
            new Regex(@"\bAssembly\s*\.\s*Load\b", RegexOptions.Compiled),
            new Regex(@"\bAssembly\s*\.\s*LoadFrom\b", RegexOptions.Compiled),
            new Regex(@"\bAssembly\s*\.\s*LoadFile\b", RegexOptions.Compiled),
            new Regex(@"\bAppDomain\s*\.\s*Create\b", RegexOptions.Compiled),

            // File system destructive operations
            new Regex(@"\bFile\s*\.\s*Delete\b", RegexOptions.Compiled),
            new Regex(@"\bFile\s*\.\s*Move\b", RegexOptions.Compiled),
            new Regex(@"\bFile\s*\.\s*WriteAll\b", RegexOptions.Compiled),
            new Regex(@"\bFile\s*\.\s*AppendAll\b", RegexOptions.Compiled),
            new Regex(@"\bDirectory\s*\.\s*Delete\b", RegexOptions.Compiled),

            // Network operations
            new Regex(@"\bWebClient\b", RegexOptions.Compiled),
            new Regex(@"\bHttpClient\b", RegexOptions.Compiled),
            new Regex(@"\bWebRequest\s*\.\s*Create\b", RegexOptions.Compiled),
            new Regex(@"\bTcpClient\b", RegexOptions.Compiled),
            new Regex(@"\bSocket\b", RegexOptions.Compiled),

            // Reflection abuse
            new Regex(@"\bType\s*\.\s*InvokeMember\b", RegexOptions.Compiled),
            new Regex(@"\bActivator\s*\.\s*CreateInstance\b", RegexOptions.Compiled),

            // Compiler/CodeDom (prevent recursive exec)
            new Regex(@"\bCSharpCodeProvider\b", RegexOptions.Compiled),
            new Regex(@"\bCodeDomProvider\b", RegexOptions.Compiled),

            // Unity quit
            new Regex(@"\bEditorApplication\s*\.\s*Exit\b", RegexOptions.Compiled),
            new Regex(@"\bApplication\s*\.\s*Quit\b", RegexOptions.Compiled),
        };

        static HashSet<string> s_Whitelist;

        static HashSet<string> Whitelist
        {
            get
            {
                if (s_Whitelist == null)
                {
                    s_Whitelist = new HashSet<string>(s_DefaultWhitelist);
                    var settings = ExecSettings.Instance;
                    if (settings?.additionalWhitelist != null)
                    {
                        foreach (var ns in settings.additionalWhitelist)
                        {
                            if (!string.IsNullOrWhiteSpace(ns))
                                s_Whitelist.Add(ns.Trim());
                        }
                    }
                }
                return s_Whitelist;
            }
        }

        /// <summary>
        /// Reloads whitelist from settings. Call when settings change.
        /// </summary>
        public static void ReloadWhitelist()
        {
            s_Whitelist = null;
        }

        /// <summary>
        /// Validates code and usings against security policy.
        /// Returns null if valid, or an error message if blocked.
        /// </summary>
        public static string Validate(string code, string[] usings)
        {
            if (string.IsNullOrEmpty(code))
                return "Code is empty.";

            if (System.Text.Encoding.UTF8.GetByteCount(code) > MAX_CODE_SIZE_BYTES)
                return $"Code exceeds maximum size of {MAX_CODE_SIZE_BYTES / 1024}KB.";

            // Validate using directives against whitelist
            if (usings != null)
            {
                foreach (var u in usings)
                {
                    if (!IsNamespaceAllowed(u.Trim()))
                        return $"Namespace '{u}' is not in the whitelist. Allowed: {string.Join(", ", Whitelist.OrderBy(x => x))}";
                }
            }

            // Check for using directives embedded in code
            var embeddedUsings = Regex.Matches(code, @"\busing\s+([\w.]+)\s*;");
            foreach (Match m in embeddedUsings)
            {
                var ns = m.Groups[1].Value;
                if (!IsNamespaceAllowed(ns))
                    return $"Embedded using '{ns}' is not in the whitelist.";
            }

            // Check forbidden patterns
            foreach (var pattern in s_ForbiddenPatterns)
            {
                var match = pattern.Match(code);
                if (match.Success)
                    return $"Forbidden pattern detected: '{match.Value}'. This operation is not allowed for security reasons.";
            }

            return null; // valid
        }

        /// <summary>
        /// Checks if a namespace is allowed by the whitelist.
        /// Supports prefix matching: "UnityEngine" allows "UnityEngine.UI".
        /// Exact-only namespaces (e.g. "System") only match themselves, not children.
        /// </summary>
        static bool IsNamespaceAllowed(string ns)
        {
            // Check exact-only namespaces first
            if (s_ExactOnlyNamespaces.Contains(ns))
                return true;

            foreach (var allowed in Whitelist)
            {
                // Skip exact-only entries for prefix matching
                if (s_ExactOnlyNamespaces.Contains(allowed))
                    continue;

                if (ns == allowed || ns.StartsWith(allowed + "."))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the current whitelist for display/debugging.
        /// </summary>
        public static IReadOnlyCollection<string> GetWhitelist() => Whitelist;

        /// <summary>
        /// Returns the forbidden pattern descriptions for display/debugging.
        /// </summary>
        public static string[] GetForbiddenPatternDescriptions() =>
            s_ForbiddenPatterns.Select(p => p.ToString()).ToArray();
    }
}
