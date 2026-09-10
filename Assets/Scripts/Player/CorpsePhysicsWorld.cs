using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using PirateSlop.Networking;

namespace PirateSlop
{
    public sealed class CorpsePhysicsWorld : MonoBehaviour
    {
        static CorpsePhysicsWorld instance;
        readonly Dictionary<Collider, Rigidbody> surfaces = new();
        readonly List<Collider> expired = new();
        readonly List<GameObject> corpses = new();
        Scene scene;
        PhysicsScene physicsScene;

        public static void Add(GameObject corpse, Vector3 position)
        {
            if (instance == null)
            {
                instance = new GameObject("CorpsePhysicsWorld").AddComponent<CorpsePhysicsWorld>();
                instance.scene = SceneManager.CreateScene("CorpsePhysics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                instance.physicsScene = instance.scene.GetPhysicsScene();
            }
            instance.corpses.RemoveAll(c => c == null);
            if (instance.corpses.Count >= 8)
            {
                Destroy(instance.corpses[0]);
                instance.corpses.RemoveAt(0);
            }
            SceneManager.MoveGameObjectToScene(corpse, instance.scene);
            instance.corpses.Add(corpse);
            foreach (var collider in Physics.OverlapSphere(position, 45f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                instance.AddSurface(collider);
        }

        void AddSurface(Collider source)
        {
            if (surfaces.ContainsKey(source) || source is CharacterController || source.GetComponentInParent<NetworkPlayer>() != null) return;
            if (!(source is BoxCollider || source is SphereCollider || source is CapsuleCollider || source is MeshCollider)) return;
            var root = new GameObject("CorpseSurface") { layer = 2 };
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            root.transform.localScale = source.transform.lossyScale;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            switch (source)
            {
                case BoxCollider box:
                    var b = root.AddComponent<BoxCollider>(); b.center = box.center; b.size = box.size;
                    break;
                case SphereCollider sphere:
                    var s = root.AddComponent<SphereCollider>(); s.center = sphere.center; s.radius = sphere.radius;
                    break;
                case CapsuleCollider capsule:
                    var c = root.AddComponent<CapsuleCollider>(); c.center = capsule.center; c.radius = capsule.radius;
                    c.height = capsule.height; c.direction = capsule.direction;
                    break;
                case MeshCollider mesh:
                    var m = root.AddComponent<MeshCollider>(); m.sharedMesh = mesh.sharedMesh; m.convex = mesh.convex;
                    break;
            }
            surfaces.Add(source, body);
        }

        void FixedUpdate()
        {
            corpses.RemoveAll(c => c == null);
            if (corpses.Count == 0) { Destroy(gameObject); return; }
            expired.Clear();
            foreach (var pair in surfaces)
            {
                if (pair.Key == null) { Destroy(pair.Value.gameObject); expired.Add(pair.Key); continue; }
                bool active = pair.Key.enabled && pair.Key.gameObject.activeInHierarchy;
                pair.Value.gameObject.SetActive(active);
                if (!active) continue;
                pair.Value.MovePosition(pair.Key.transform.position);
                pair.Value.MoveRotation(pair.Key.transform.rotation);
            }
            foreach (var key in expired) surfaces.Remove(key);
            if (physicsScene.IsValid()) physicsScene.Simulate(Time.fixedDeltaTime);
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
    }
}
