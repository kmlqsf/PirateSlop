using System.Collections.Generic;
using UnityEngine;

public sealed class VfxPool : MonoBehaviour
{
    static VfxPool instance;
    public static VfxPool Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
            {
                instance = Object.FindAnyObjectByType<VfxPool>();
                if (instance == null)
                {
                    var go = new GameObject("VfxPool");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<VfxPool>();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [System.Serializable]
    public struct Entry
    {
        public string Key;
        public GameObject Prefab;
        public int Warmup;
    }

    public Entry[] Entries;
    readonly Dictionary<string, Queue<GameObject>> pools = new();
    readonly Dictionary<string, GameObject> prefabMap = new();

    void Awake()
    {
        if (instance == null) instance = this;
        Initialize();
    }

    void Initialize()
    {
        if (Entries == null || Entries.Length == 0)
        {
            var defaultEntries = new List<Entry>();
            var flash = Resources.Load<GameObject>("CombatVfx/MuzzleFlash");
            if (flash != null) defaultEntries.Add(new Entry { Key = "MuzzleFlash", Prefab = flash, Warmup = 8 });
            var splinters = Resources.Load<GameObject>("CombatVfx/HullSplinters");
            if (splinters != null) defaultEntries.Add(new Entry { Key = "HullSplinters", Prefab = splinters, Warmup = 8 });
            var mist = Resources.Load<GameObject>("CombatVfx/CrewRespawnMist");
            if (mist != null) defaultEntries.Add(new Entry { Key = "CrewRespawnMist", Prefab = mist, Warmup = 4 });
            var particles = Resources.Load<GameObject>("CombatVfx/CombatParticles");
            if (particles != null) defaultEntries.Add(new Entry { Key = "CombatParticles", Prefab = particles, Warmup = 16 });
            Entries = defaultEntries.ToArray();
        }

        foreach (var e in Entries)
        {
            if (string.IsNullOrEmpty(e.Key) || e.Prefab == null) continue;
            prefabMap[e.Key] = e.Prefab;
            if (!pools.TryGetValue(e.Key, out var q))
            {
                q = new Queue<GameObject>();
                pools[e.Key] = q;
            }
            for (int i = 0; i < e.Warmup; i++)
            {
                var go = Instantiate(e.Prefab, transform);
                go.SetActive(false);
                q.Enqueue(go);
            }
        }
    }

    public GameObject Get(string key, Vector3 position, Quaternion rotation)
    {
        if (!pools.TryGetValue(key, out var q))
        {
            if (!prefabMap.TryGetValue(key, out var prefab) || prefab == null) return null;
            q = new Queue<GameObject>();
            pools[key] = q;
        }

        GameObject go = null;
        while (q.Count > 0)
        {
            go = q.Dequeue();
            if (go != null) break;
        }

        if (go == null)
        {
            if (!prefabMap.TryGetValue(key, out var prefab) || prefab == null) return null;
            go = Instantiate(prefab, transform);
        }

        go.transform.SetPositionAndRotation(position, rotation);
        go.SetActive(true);
        return go;
    }

    public void Return(string key, GameObject go, float delay = 0f)
    {
        if (go == null) return;
        if (delay > 0f)
        {
            StartCoroutine(ReturnDelayed(key, go, delay));
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(transform);
        if (pools.TryGetValue(key, out var q))
        {
            q.Enqueue(go);
        }
        else
        {
            Destroy(go);
        }
    }

    System.Collections.IEnumerator ReturnDelayed(string key, GameObject go, float delay)
    {
        yield return new WaitForSeconds(delay);
        Return(key, go);
    }
}
