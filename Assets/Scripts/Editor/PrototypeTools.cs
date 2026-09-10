using UnityEditor;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace PirateSlop.Editor
{
    public static class PrototypeTools
    {
        [MenuItem("PirateSlop/Geometry/Boarding Ramp")]
        static void Ramp() => Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2.4f, .22f, 8f)), "BoardingRamp");

        [MenuItem("PirateSlop/Geometry/Deck Cover")]
        static void Cover() => Place(ShapeGenerator.GenerateCube(PivotLocation.Center, new Vector3(2f, 1.1f, .6f)), "DeckCover");

        [MenuItem("PirateSlop/Geometry/Reef Rock")]
        static void Rock()
        {
            var mesh = ShapeGenerator.GenerateIcosahedron(PivotLocation.Center, 1f, 1, true, false);
            mesh.transform.localScale = new Vector3(4f, 6f, 3f);
            Place(mesh, "ReefRock");
        }

        static void Place(ProBuilderMesh mesh, string name)
        {
            mesh.name = name;
            mesh.ToMesh();
            mesh.Refresh();
            var target = Selection.activeTransform;
            if (target != null)
            {
                mesh.transform.SetParent(target, false);
                mesh.transform.localPosition = Vector3.zero;
            }
            else if (SceneView.lastActiveSceneView != null) mesh.transform.position = SceneView.lastActiveSceneView.pivot;
            var collider = mesh.GetComponent<MeshCollider>();
            if (collider == null) collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;
            Undo.RegisterCreatedObjectUndo(mesh.gameObject, "Create " + name);
            Selection.activeGameObject = mesh.gameObject;
        }
    }
}
