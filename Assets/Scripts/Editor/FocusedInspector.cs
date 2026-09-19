using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class FocusedInspector
    {
        sealed class Report
        {
            readonly StringBuilder text = new StringBuilder();
            public bool Full { get; private set; }
            public void Add(string value)
            {
                if (Full) return;
                if (text.Length + value.Length > 18000)
                {
                    text.AppendLine("TRUNCATED: output limit reached; select a smaller subtree or component filter.");
                    Full = true;
                    return;
                }
                text.AppendLine(value);
            }
            public override string ToString() => text.ToString();
        }

        public static string CaptureSelection(bool includeChildren = false, string componentFilter = "", bool details = false, int maxObjects = 40)
        {
            return Capture(Selection.activeGameObject, includeChildren, componentFilter, details, maxObjects);
        }

        public static string CaptureAsset(string assetPath, bool includeChildren = false, string componentFilter = "", bool details = false, int maxObjects = 40)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return "ERROR: provide an exact Assets/... .prefab path.";
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (target == null) return "ERROR: prefab not found: " + assetPath;
            return Capture(target, includeChildren, componentFilter, details, maxObjects);
        }

        public static string Capture(GameObject target, bool includeChildren = false, string componentFilter = "", bool details = false, int maxObjects = 40)
        {
            if (target == null) return "Select a GameObject in the Hierarchy or a prefab in the Project window.";
            maxObjects = Mathf.Clamp(maxObjects, 1, 200);
            var report = new Report();
            componentFilter = (componentFilter ?? "").Trim();
            report.Add("FOCUSED INSPECTOR | " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            report.Add("Read-only snapshot; no gameplay or multiplayer verification.");
            report.Add("Target: " + Path(target.transform) + " | entityId=" + target.GetEntityId());
            report.Add("Source: " + (EditorUtility.IsPersistent(target) ? AssetDatabase.GetAssetPath(target) : target.scene.path + " | unsaved=" + target.scene.isDirty));
            report.Add("PlayMode=" + Application.isPlaying + " | children=" + includeChildren + " | filter=" + (componentFilter.Length == 0 ? "all" : componentFilter) + " | details=" + details);
            report.Add("Null references are unassigned, not necessarily errors. Missing references/scripts are reported separately.");
            var pending = new Stack<Transform>();
            pending.Push(target.transform);
            int visited = 0, matched = 0;
            while (pending.Count > 0 && visited < maxObjects && !report.Full)
            {
                var current = pending.Pop();
                visited++;
                if (includeChildren)
                    for (int i = current.childCount - 1; i >= 0; i--) pending.Push(current.GetChild(i));
                var components = current.GetComponents<Component>();
                var selected = components.Where(c => c != null && (componentFilter.Length == 0 || c.GetType().FullName.IndexOf(componentFilter, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                if (selected.Length == 0 && componentFilter.Length > 0) continue;
                matched++;
                report.Add("");
                report.Add("OBJECT " + Path(current) + " | id=" + current.gameObject.GetEntityId() + " | activeSelf=" + current.gameObject.activeSelf + " | activeHierarchy=" + current.gameObject.activeInHierarchy + " | layer=" + current.gameObject.layer + ":" + LayerMask.LayerToName(current.gameObject.layer));
                report.Add("Transform local: position=" + current.localPosition.ToString("F3") + " rotation=" + current.localEulerAngles.ToString("F2") + " scale=" + current.localScale.ToString("F3"));
                report.Add("Components: " + Clip(string.Join(", ", components.Select(c => c == null ? "MISSING SCRIPT" : c.GetType().Name)), 1400));
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(current.gameObject);
                if (missing > 0) report.Add("ERROR: missing scripts=" + missing);
                Prefab(current.gameObject, report);
                foreach (var component in selected.Take(40))
                {
                    if (report.Full) break;
                    InspectComponent(component, details, report);
                }
                if (selected.Length > 40) report.Add("TRUNCATED: only first 40 matching components inspected.");
            }
            if (pending.Count > 0) report.Add("TRUNCATED: subtree incomplete; select a closer object or increase maxObjects (maximum 200).");
            report.Add("Visited=" + visited + "; matching objects=" + matched + ". Filter limits reference checks to matching components.");
            return report.ToString();
        }

        static void InspectComponent(Component component, bool details, Report report)
        {
            string enabled = component is Behaviour behaviour ? " | enabled=" + behaviour.enabled : "";
            report.Add("  " + component.GetType().FullName + enabled);
            if (component is MonoBehaviour script)
            {
                var source = MonoScript.FromMonoBehaviour(script);
                if (source != null) report.Add("    script: " + AssetDatabase.GetAssetPath(source));
            }
            try
            {
                using (var serialized = new SerializedObject(component))
                {
                    var property = serialized.GetIterator();
                    int examined = 0, missing = 0, unassigned = 0, shown = 0, candidates = 0;
                    int limit = details ? 40 : 8;
                    while (property.NextVisible(true))
                    {
                        if (++examined > 2000) { report.Add("    TRUNCATED: property scan stopped at 2000; reference checks incomplete."); break; }
                        if (property.propertyPath == "m_Script") continue;
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            if (property.objectReferenceValue == null)
                            {
                                if (!property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)))
                                {
                                    missing++;
                                    report.Add("    MISSING REFERENCE: " + Clip(property.propertyPath, 220));
                                }
                                else unassigned++;
                            }
                        }
                        if (property.depth > 0 && !details) continue;
                        string value = Value(property);
                        if (value == null) continue;
                        candidates++;
                        if (shown++ < limit) report.Add("    " + Clip(property.propertyPath, 180) + " = " + Clip(value, 220) + (property.prefabOverride ? " [override]" : ""));
                    }
                    if (candidates > limit) report.Add("    Values omitted=" + (candidates - limit) + (details ? "; narrow the component or inspect Unity fields." : "; enable details for more."));
                    report.Add("    References: missing=" + missing + ", unassigned=" + unassigned + " (may be optional/runtime assigned)");
                }
            }
            catch (Exception exception) { report.Add("    INCOMPLETE: " + exception.GetType().Name + ": " + Clip(exception.Message, 200)); }
        }

        static string Value(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue.ToString();
                case SerializedPropertyType.Integer: return property.longValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Float: return property.doubleValue.ToString("G6", CultureInfo.InvariantCulture);
                case SerializedPropertyType.String: return Clip(property.stringValue, 180);
                case SerializedPropertyType.Enum:
                    int index = property.enumValueIndex;
                    return index >= 0 && index < property.enumDisplayNames.Length ? property.enumDisplayNames[index] : property.intValue.ToString(CultureInfo.InvariantCulture);
                case SerializedPropertyType.Vector2: return property.vector2Value.ToString("F3");
                case SerializedPropertyType.Vector3: return property.vector3Value.ToString("F3");
                case SerializedPropertyType.Color: return property.colorValue.ToString();
                case SerializedPropertyType.ObjectReference:
                    var reference = property.objectReferenceValue;
                    if (reference == null) return !property.objectReferenceEntityIdValue.Equals(default(UnityEngine.EntityId)) ? "MISSING" : "unassigned";
                    string path = AssetDatabase.GetAssetPath(reference);
                    if (string.IsNullOrEmpty(path)) path = reference is Component c ? Path(c.transform) : reference is GameObject g ? Path(g.transform) : reference.name;
                    return path + " (" + reference.GetType().Name + ", id=" + reference.GetEntityId() + ")";
                default: return property.isArray ? "array size=" + property.arraySize : null;
            }
        }

        static void Prefab(GameObject target, Report report)
        {
            if (PrefabUtility.IsPartOfPrefabAsset(target))
            {
                report.Add("Prefab asset: " + PrefabUtility.GetPrefabAssetType(target) + " | " + AssetDatabase.GetAssetPath(target) + " (not a scene instance)");
                if (PrefabUtility.GetPrefabAssetType(target) != PrefabAssetType.Variant) return;
            }
            var status = PrefabUtility.GetPrefabInstanceStatus(target);
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(target);
            report.Add("Prefab: " + status + (string.IsNullOrEmpty(source) ? "" : " | " + source));
            if (status == PrefabInstanceStatus.MissingAsset) report.Add("ERROR: missing prefab source.");
            if (status != PrefabInstanceStatus.Connected) return;
            var root = PrefabUtility.GetNearestPrefabInstanceRoot(target);
            if (root != target) { report.Add("Prefab override summary belongs to instance root: " + Path(root.transform)); return; }
            var modifications = PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>();
            var meaningful = modifications.Where(m => m != null && !PrefabUtility.IsDefaultOverride(m)).ToArray();
            report.Add("Instance overrides (entire nearest root): properties=" + meaningful.Length + ", added components=" + PrefabUtility.GetAddedComponents(root).Count + ", removed components=" + PrefabUtility.GetRemovedComponents(root).Count + ", added objects=" + PrefabUtility.GetAddedGameObjects(root).Count + ", removed objects=" + PrefabUtility.GetRemovedGameObjects(root).Count);
            foreach (var modification in meaningful.Take(8))
                report.Add("  override: " + (modification.target == null ? "missing target" : modification.target.name + ":" + modification.target.GetType().Name) + "." + Clip(modification.propertyPath, 180));
            if (meaningful.Length > 8) report.Add("  Override paths omitted=" + (meaningful.Length - 8));
        }

        static string Path(Transform transform)
        {
            var parts = new Stack<string>();
            while (transform != null) { parts.Push(Clip(transform.name, 100) + "[" + transform.GetSiblingIndex() + "]"); transform = transform.parent; }
            return string.Join("/", parts);
        }

        static string Clip(string value, int limit)
        {
            value = (value ?? "").Replace("\r", " ").Replace("\n", " ");
            return value.Length > limit ? value.Substring(0, limit) + "..." : value;
        }
    }
}

