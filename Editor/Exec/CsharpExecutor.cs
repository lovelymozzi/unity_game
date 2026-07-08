using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CSharp;
using UnityEngine;

namespace UnityExec
{
    /// <summary>
    /// Compiles and executes C# code at runtime inside the Unity Editor.
    /// Applies SecurityPolicy whitelist before compilation.
    /// Uses CSharpCodeProvider with assembly reference filtering.
    /// </summary>
    public static class CsharpExecutor
    {
        static readonly string[] DefaultUsings =
        {
            "System",
            "System.Collections.Generic",
            "System.Linq",
            "UnityEngine",
            "UnityEditor",
        };

        /// <summary>
        /// Executes C# code with optional additional usings.
        /// Returns a SuccessResponse or ErrorResponse.
        /// </summary>
        public static object Execute(string code, string[] extraUsings)
        {
            // Security validation
            var violation = SecurityPolicy.Validate(code, extraUsings);
            if (violation != null)
            {
                AuditLogger.LogBlocked(code, violation);
                return new ErrorResponse($"Security policy violation: {violation}");
            }

            // Auto-wrap single expressions
            if (!Regex.IsMatch(code, @"\breturn\b"))
            {
                var trimmed = code.TrimEnd().TrimEnd(';');
                code = $"return (object)({trimmed});";
            }

            var source = BuildSource(code, extraUsings);

            try
            {
                var result = CompileAndExecute(source);
                if (result is ErrorResponse err)
                {
                    AuditLogger.LogError(code, err.error);
                    return result;
                }

                AuditLogger.LogSuccess(code);
                return result;
            }
            catch (Exception ex)
            {
                AuditLogger.LogError(code, ex.Message);
                return new ErrorResponse($"Execution error: {ex.Message}");
            }
        }

        static string BuildSource(string code, string[] extraUsings)
        {
            var sb = new StringBuilder();
            foreach (var u in DefaultUsings)
                sb.AppendLine($"using {u};");
            if (extraUsings != null)
            {
                foreach (var u in extraUsings)
                    sb.AppendLine($"using {u};");
            }

            sb.AppendLine();
            sb.AppendLine("public static class __ExecDynamic {");
            sb.AppendLine("    public static object Execute() {");
            sb.AppendLine(code);
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        static object CompileAndExecute(string source)
        {
            var provider = new CSharpCodeProvider();
            var cp = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
                TreatWarningsAsErrors = false
            };

            // Collect whitelisted assembly references
            var references = new List<string>();
            var added = new HashSet<string>();
            var whitelist = SecurityPolicy.GetWhitelist();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.IsDynamic || string.IsNullOrEmpty(asm.Location))
                        continue;

                    var name = asm.GetName().Name;
                    if (!added.Add(name))
                        continue;
                    if (name == "mscorlib")
                        continue;
                    if (IsBclFacade(asm))
                        continue;

                    // Only include assemblies from whitelisted namespaces
                    if (!IsAssemblyAllowed(asm, whitelist))
                        continue;

                    references.Add(asm.Location);
                }
                catch
                {
                    // Skip problematic assemblies
                }
            }

            // Use response file to avoid command line length limits
            string rspPath = null;
            try
            {
                rspPath = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    $"unity_exec_{Guid.NewGuid():N}.rsp");

                var rspContent = new StringBuilder();
                foreach (var r in references)
                    rspContent.AppendLine($"/r:\"{r}\"");
                System.IO.File.WriteAllText(rspPath, rspContent.ToString());
                cp.CompilerOptions = $"@\"{rspPath}\"";
            }
            catch
            {
                // Fallback: add references directly
                foreach (var r in references)
                    cp.ReferencedAssemblies.Add(r);
            }

            try
            {
                var result = provider.CompileAssemblyFromSource(cp, source);
                if (result.Errors.HasErrors)
                {
                    var errors = new List<string>();
                    foreach (CompilerError err in result.Errors)
                    {
                        if (!err.IsWarning)
                            errors.Add($"L{err.Line}: {err.ErrorText}");
                    }
                    return new ErrorResponse($"Compile error:\n{string.Join("\n", errors)}");
                }

                var method = result.CompiledAssembly.GetType("__ExecDynamic")?.GetMethod("Execute");
                if (method == null)
                    return new ErrorResponse("Internal error: compiled type or method not found.");

                var output = method.Invoke(null, null);
                return new SuccessResponse("OK", Serialize(output, 0));
            }
            finally
            {
                if (rspPath != null)
                {
                    try { System.IO.File.Delete(rspPath); }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Check if assembly exports types from whitelisted namespaces.
        /// Core Unity assemblies are always allowed.
        /// </summary>
        static bool IsAssemblyAllowed(Assembly asm, IReadOnlyCollection<string> whitelist)
        {
            var name = asm.GetName().Name;

            // Always allow core assemblies needed for compilation
            if (name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor"))
                return true;
            if (name.StartsWith("System") || name == "netstandard")
                return true;
            if (name.StartsWith("Microsoft."))
                return true;

            // Check if any exported namespace matches whitelist
            try
            {
                foreach (var type in asm.GetExportedTypes())
                {
                    var ns = type.Namespace;
                    if (string.IsNullOrEmpty(ns))
                        continue;

                    foreach (var allowed in whitelist)
                    {
                        if (ns == allowed || ns.StartsWith(allowed + "."))
                            return true;
                    }
                }
            }
            catch
            {
                // Can't inspect types — skip this assembly
            }

            return false;
        }

        static bool IsBclFacade(Assembly asm)
        {
            var name = asm.GetName().Name;
            if (!name.StartsWith("System."))
                return false;
            if (name.StartsWith("System.Private."))
                return false;
            try
            {
                foreach (var attr in asm.GetCustomAttributesData())
                {
                    if (attr.AttributeType.Name == "TypeForwardedToAttribute")
                        return true;
                }
            }
            catch { }
            return false;
        }

        #region Serialization

        const int MAX_DEPTH = 4;
        const int MAX_COLLECTION_ITEMS = 100;

        // 접근(get)만으로 인스턴스를 새로 생성하거나 씬을 dirty 시키는 Unity getter — 직렬화에서 제외.
        // (Renderer.material/materials → 머티리얼 인스턴스화, MeshFilter.mesh → 메시 인스턴스화)
        static readonly HashSet<string> s_SideEffectProperties = new HashSet<string>
        {
            "material", "materials", "mesh",
        };

        static object Serialize(object obj, int depth) => Serialize(obj, depth, new HashSet<Type>());

        static object Serialize(object obj, int depth, HashSet<Type> ancestors)
        {
            if (obj == null) return null;
            if (depth > MAX_DEPTH) return obj.ToString();

            var type = obj.GetType();
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal))
                return obj;
            if (type.IsEnum)
                return obj.ToString();
            if (type.Name.StartsWith("FixedString"))
                return obj.ToString();

            // 중첩된 UnityEngine.Object 레퍼런스(transform, gameObject, parent 등)는 재귀하지 않는다.
            // 객체 그래프(transform↔gameObject, parent/root, 자식 배열)를 타고 들어가면 응답이 폭증하므로
            // 짧은 참조 문자열("Name (Type)")로만 표기한다. 최상위(depth 0) 요청 객체는 그대로 펼친다.
            if (depth > 0 && obj is UnityEngine.Object)
                return obj.ToString();

            if (obj is IDictionary dict)
            {
                var r = new Dictionary<string, object>();
                foreach (DictionaryEntry e in dict)
                    r[e.Key.ToString()] = Serialize(e.Value, depth + 1, ancestors);
                return r;
            }

            // Transform 등 일부 UnityEngine.Object 는 IEnumerable(자식 순회)을 구현한다.
            // 컬렉션으로 직렬화하면 position 등 실제 데이터가 사라지므로, Unity 오브젝트는 컬렉션 분기에서 제외한다.
            if (obj is IEnumerable enumerable && !(obj is UnityEngine.Object))
            {
                var list = new List<object>();
                int count = 0;
                foreach (var item in enumerable)
                {
                    if (count++ >= MAX_COLLECTION_ITEMS)
                    {
                        list.Add($"... (truncated at {MAX_COLLECTION_ITEMS})");
                        break;
                    }
                    list.Add(Serialize(item, depth + 1, ancestors));
                }
                return list;
            }

            if (type.IsValueType || type.IsClass)
            {
                // 순환(자기참조) 방지: 지금 펼치고 있는 타입이 다시 나오면 재귀하지 않고 ToString.
                // (예: UnityEngine.TransformHandle 의 root/parent 가 다시 TransformHandle 을 반환 — 값타입이라 Object 가드로는 못 막음)
                if (!ancestors.Add(type))
                    return obj.ToString();

                try
                {
                    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                    if (fields.Length > 0)
                    {
                        // 필드가 있는 타입(예: Vector3, Color)은 필드가 곧 데이터다.
                        // 파생 프로퍼티(normalized, magnitude 등)는 중복·자기참조 재귀를 유발하므로 필드만 직렬화.
                        var r = new Dictionary<string, object>();
                        foreach (var f in fields)
                        {
                            try { r[f.Name] = Serialize(f.GetValue(obj), depth + 1, ancestors); }
                            catch { r[f.Name] = "<error>"; }
                        }
                        return r;
                    }

                    // public 필드가 없는 타입(예: GameObject, Transform, Ray)은 데이터를 프로퍼티로 노출한다.
                    // 읽기 불가/인덱서/Obsolete/부수효과 getter(material, mesh 등)는 제외.
                    var props = new Dictionary<string, object>();
                    foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (!p.CanRead) continue;
                        if (p.GetIndexParameters().Length > 0) continue;
                        if (s_SideEffectProperties.Contains(p.Name)) continue;
                        if (Attribute.IsDefined(p, typeof(ObsoleteAttribute))) continue;

                        try { props[p.Name] = Serialize(p.GetValue(obj), depth + 1, ancestors); }
                        catch { props[p.Name] = "<error>"; }
                    }

                    if (props.Count > 0)
                        return props;
                }
                finally
                {
                    ancestors.Remove(type);
                }
            }

            return obj.ToString();
        }

        #endregion
    }
}
