using System;
using System.IO;
using System.Linq;
using System.Text;
using PirateSlop.Networking;
using UnityEditor;
using UnityEngine;

namespace PirateSlop.Editor
{
    public static class LootModelGallery
    {
        [MenuItem("PirateSlop/Export Loot Model Gallery")]
        public static void Export()
        {
            string directory = Path.GetFullPath("Docs/LootModels");
            Directory.CreateDirectory(directory);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Networking/NetworkPlayer.prefab");
            var prefabs = player.GetComponent<NetworkWeapon>().DropPrefabs;
            var catalog = AssetDatabase.LoadAssetAtPath<LootCatalog>("Assets/Settings/Loot/DefaultLoot.asset");
            var html = new StringBuilder("<!doctype html><meta charset='utf-8'><title>Модели лута</title><style>body{background:#142127;color:#eee;font:16px system-ui;margin:32px}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(270px,1fr));gap:20px}article{background:#203139;padding:16px;border-radius:12px}img{width:100%;background:#18252c}p{overflow-wrap:anywhere;color:#b9d2d4;font-size:12px}h2{font-size:20px}</style><h1>Лут — текущие игровые модели</h1><p>Рендеры префабов. Это референсы для будущих иконок. Доска отключена в инвентаре; сундук переносится отдельно.</p><main>");
            var md = new StringBuilder("# Модели лутабельных предметов\n\nРеференсы текущих моделей для иконок.\n\n");
            foreach (InventoryItem item in Enum.GetValues(typeof(InventoryItem)))
            {
                if (item == InventoryItem.None) continue;
                int index = CannonAmmo.IsBall(item) ? (int)InventoryItem.Cannonball : (int)item;
                var prefab = index < prefabs.Length ? prefabs[index] : null;
                if (prefab == null) throw new InvalidOperationException("Missing loot prefab: " + item);
                GameObject model = prefab.gameObject;
                if (CannonAmmo.IsBall(item))
                {
                    var ball = prefab.GetComponent<Cannonball>();
                    int ammoIndex = item == InventoryItem.Cannonball ? 0 : item == InventoryItem.BoardingHook ? 5 : (int)item - 7;
                    if (ball != null && ball.AmmoModels != null && ammoIndex < ball.AmmoModels.Length && ball.AmmoModels[ammoIndex] != null) model = ball.AmmoModels[ammoIndex];
                }
                string name = InventoryIcons.ItemName(item);
                string note = item == InventoryItem.Plank ? "Отключена: сервер не добавляет в инвентарь." : catalog.Items.Any(e => e.Item == item && e.Weight > 0) ? "Есть в случайном луте сундуков." : "Предмет инвентаря; отсутствует в случайной таблице сундуков.";
                Add(model, item.ToString(), name, note, directory, html, md);
            }
            Add(catalog.ChestPrefab.gameObject, "LootChest", "Сундук с припасами", "Переносимый контейнер, отдельный от слотов инвентаря.", directory, html, md);
            File.WriteAllText(Path.Combine(directory, "index.html"), html.Append("</main>").ToString(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(directory, "loot-models.md"), md.ToString(), Encoding.UTF8);
        }

        static void Add(GameObject prefab, string id, string name, string note, string directory, StringBuilder html, StringBuilder md)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                var instance = preview.InstantiatePrefabInScene(prefab);
                foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var renderers = instance.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r is not ParticleSystemRenderer && r is not TrailRenderer && r is not LineRenderer).ToArray();
                if (renderers.Length == 0) throw new InvalidOperationException("No visible model: " + id);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                float radius = Mathf.Max(.05f, bounds.extents.magnitude);
                preview.camera.orthographic = true;
                preview.camera.orthographicSize = radius * 1.1f;
                preview.camera.nearClipPlane = .01f;
                preview.camera.farClipPlane = radius * 20f + 10;
                preview.camera.transform.position = bounds.center + new Vector3(1.4f, 1f, -2f).normalized * radius * 4f;
                preview.camera.transform.LookAt(bounds.center);
                preview.camera.clearFlags = CameraClearFlags.SolidColor;
                preview.camera.backgroundColor = new Color(.055f, .08f, .1f, 0);
                preview.lights[0].intensity = 1.4f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, -35, 0);
                preview.lights[1].intensity = .8f;
                preview.lights[1].transform.rotation = Quaternion.Euler(20, 145, 0);
                preview.ambientColor = new Color(.45f, .45f, .45f);
                preview.BeginStaticPreview(new Rect(0, 0, 768, 768));
                preview.Render(true);
                var image = preview.EndStaticPreview();
                File.WriteAllBytes(Path.Combine(directory, id + ".png"), image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally { preview.Cleanup(); }
            string path = AssetDatabase.GetAssetPath(prefab);
            html.Append($"<article><img src='{id}.png'><h2>{name}</h2><p>{id} · {note}</p><p>{path}</p></article>");
            md.Append($"## {name} — {id}\n\n![{name}]({id}.png)\n\n{note}\n\nМодель: `{path}`\n\n");
        }
    }
}
