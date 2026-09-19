using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PirateSlop.Editor
{
    public sealed class FocusedInspectorWindow : EditorWindow
    {
        ObjectField target;
        Toggle children, details;
        TextField filter, output;

        [MenuItem("PirateSlop/Diagnostics/Focused Inspector")]
        public static void Open() => GetWindow<FocusedInspectorWindow>("Focused Inspector");

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            minSize = new Vector2(480, 400);
            rootVisualElement.Add(new HelpBox("Read-only. Select a scene object or prefab, then press Inspect. Missing references are separate from unassigned fields. Reports are snapshots.", HelpBoxMessageType.Info));
            target = new ObjectField("Object / Prefab") { objectType = typeof(GameObject), allowSceneObjects = true, value = Selection.activeGameObject };
            children = new Toggle("Include children (first 40 objects)");
            details = new Toggle("More field values");
            filter = new TextField("Component name filter");
            rootVisualElement.Add(target);
            rootVisualElement.Add(children);
            rootVisualElement.Add(details);
            rootVisualElement.Add(filter);
            rootVisualElement.Add(new Button(() => { target.value = Selection.activeGameObject; Inspect(); }) { text = "Inspect current selection" });
            rootVisualElement.Add(new Button(Inspect) { text = "Inspect" });
            rootVisualElement.Add(new Button(() => EditorGUIUtility.systemCopyBuffer = output.value) { text = "Copy report" });
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            output = new TextField { multiline = true, isReadOnly = true, value = "Choose an object and press Inspect." };
            scroll.Add(output);
            rootVisualElement.Add(scroll);
        }

        void Inspect()
        {
            try { output.value = FocusedInspector.Capture(target.value as GameObject, children.value, filter.value, details.value); }
            catch (System.Exception exception) { output.value = "Inspection incomplete: " + exception.Message; }
        }
    }
}
