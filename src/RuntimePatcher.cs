using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace SplituxFacepunch
{
    /// <summary>
    /// Applies game-specific patches dynamically based on config.
    /// Finds classes/methods at runtime via reflection and applies actions from PatchActions.
    /// </summary>
    public static class RuntimePatcher
    {
        private const BindingFlags AllBindings =
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static;

        /// <summary>
        /// Apply all runtime patches from config.
        /// </summary>
        public static void ApplyAll(Harmony harmony, List<RuntimePatch> patches)
        {
            if (patches == null || patches.Count == 0)
            {
                Plugin.Log.LogInfo("[RuntimePatcher] No runtime patches to apply");
                return;
            }

            Plugin.Log.LogInfo($"[RuntimePatcher] Applying {patches.Count} runtime patches...");

            foreach (var patch in patches)
            {
                ApplyPatch(harmony, patch);
            }

            Plugin.Log.LogInfo("[RuntimePatcher] Done applying runtime patches");
        }

        private static void ApplyPatch(Harmony harmony, RuntimePatch patch)
        {
            try
            {
                // Find the target type
                var type = FindType(patch.Class);
                if (type == null)
                {
                    Plugin.Log.LogWarning($"[RuntimePatcher] Class not found: {patch.Class}");
                    return;
                }

                // Get the action
                var (prefix, postfix) = PatchActions.GetAction(patch.Action);
                if (prefix == null && postfix == null)
                {
                    Plugin.Log.LogWarning($"[RuntimePatcher] Unknown action: {patch.Action}");
                    return;
                }

                // Find target method or property getter
                MethodBase target = null;

                if (patch.IsMethod)
                {
                    target = FindMethod(type, patch.Method);
                }
                else
                {
                    target = FindPropertyGetter(type, patch.Property);
                }

                if (target == null)
                {
                    var targetName = patch.IsMethod ? patch.Method : patch.Property;
                    Plugin.Log.LogWarning($"[RuntimePatcher] Target not found: {patch.Class}.{targetName}");
                    DumpTypeMembers(type, patch.Class);
                    return;
                }

                // Apply the patch
                harmony.Patch(target, prefix, postfix);
                Plugin.Log.LogInfo($"[RuntimePatcher] Applied {patch.Action} to {patch}");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[RuntimePatcher] Failed to apply patch {patch}: {ex}");
            }
        }

        /// <summary>
        /// Find a type by name across all loaded assemblies.
        /// Searches for exact match first, then by simple name.
        /// </summary>
        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Try exact match first
                    var type = assembly.GetType(typeName, false, true);
                    if (type != null) return type;

                    // Try by simple name
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name == typeName || t.FullName == typeName)
                            return t;
                    }
                }
                catch
                {
                    // Skip assemblies that throw on GetTypes()
                }
            }
            return null;
        }

        /// <summary>
        /// Find a method by name in a type.
        /// Handles overloaded methods by returning the first match.
        /// </summary>
        private static MethodInfo FindMethod(Type type, string methodName)
        {
            // Try exact match first
            var method = type.GetMethod(methodName, AllBindings);
            if (method != null) return method;

            // Try to find by name in all methods (handles some edge cases)
            foreach (var m in type.GetMethods(AllBindings))
            {
                if (m.Name == methodName)
                    return m;
            }

            // Check base types
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                method = baseType.GetMethod(methodName, AllBindings);
                if (method != null) return method;
                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Find a property getter by name in a type.
        /// </summary>
        private static MethodInfo FindPropertyGetter(Type type, string propertyName)
        {
            var property = type.GetProperty(propertyName, AllBindings);
            if (property != null)
            {
                return property.GetGetMethod(true);
            }

            // Check base types
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(object))
            {
                property = baseType.GetProperty(propertyName, AllBindings);
                if (property != null)
                {
                    return property.GetGetMethod(true);
                }
                baseType = baseType.BaseType;
            }

            return null;
        }

        /// <summary>
        /// Dump all members of a type for debugging.
        /// </summary>
        private static void DumpTypeMembers(Type type, string label)
        {
            Plugin.Log.LogDebug($"[RuntimePatcher] === Members of {label} ({type.FullName}) ===");

            Plugin.Log.LogDebug("  Methods:");
            foreach (var m in type.GetMethods(AllBindings | BindingFlags.DeclaredOnly))
            {
                if (!m.Name.StartsWith("get_") && !m.Name.StartsWith("set_"))
                {
                    var access = m.IsStatic ? "static" : "instance";
                    Plugin.Log.LogDebug($"    [{access}] {m.Name}()");
                }
            }

            Plugin.Log.LogDebug("  Properties:");
            foreach (var p in type.GetProperties(AllBindings))
            {
                var getter = p.GetGetMethod(true);
                var access = getter?.IsStatic == true ? "static" : "instance";
                Plugin.Log.LogDebug($"    [{access}] {p.Name} : {p.PropertyType.Name}");
            }

            Plugin.Log.LogDebug("  Fields:");
            foreach (var f in type.GetFields(AllBindings))
            {
                var access = f.IsStatic ? "static" : "instance";
                Plugin.Log.LogDebug($"    [{access}] {f.Name} : {f.FieldType.Name}");
            }

            Plugin.Log.LogDebug($"[RuntimePatcher] === End {label} dump ===");
        }
    }
}
