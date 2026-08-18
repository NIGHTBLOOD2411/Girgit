#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// One-time migration: converts the static Dot/Bar tiles (ParallaxTile,
    /// wrap-in-place) under Parallax_Back/Parallax_Front into a pooled
    /// Spawner setup instead — each becomes a Prefab, the original scene
    /// copies are removed, and new Spawner objects take over spawning them
    /// from a small area off the right edge of the camera, moving left at a
    /// random speed, recycling back into the pool once they scroll off the
    /// left edge. Safe to re-run — skips whichever half is already set up.
    ///
    /// Menu: Girgit > Setup Dot+Bar Spawners.
    /// </summary>
    public static class GirgitSpawnerMenu
    {
        const string PrefabDir = "Assets/Girgit/Prefabs";

        [MenuItem("Girgit/Setup Dot+Bar Spawners")]
        public static void Setup()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                EditorUtility.DisplayDialog("Girgit", "No Main Camera found in this scene.", "OK");
                return;
            }

            float edgeX = cam.orthographicSize * cam.aspect + 1f;
            var log = new List<string>();

            GameObject back = GameObject.Find("Parallax_Back");
            if (back != null) SetupDots(back.transform, edgeX, log);

            GameObject front = GameObject.Find("Parallax_Front");
            if (front != null) SetupBars(front.transform, edgeX, log);

            if (log.Count == 0)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "Nothing to set up — no Parallax_Back/Parallax_Front found, or spawners already exist.", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Girgit", string.Join("\n", log) + "\n\nSave the scene (Ctrl+S).", "OK");
        }

        static void SetupDots(Transform back, float edgeX, List<string> log)
        {
            if (back.Find("DotSpawnerTop") != null) return; // already migrated

            var dots = FindChildren(back, "Dot");
            if (dots.Count == 0) return;

            GameObject prefab = BuildPrefab(dots[0], "Dot");

            float topY = AverageY(dots, top: true, fallback: 3f);
            float bottomY = AverageY(dots, top: false, fallback: -3f);

            CreateSpawner(back, "DotSpawnerTop", prefab, new Vector3(edgeX, topY, dots[0].position.z),
                spawnWidth: 0.5f, spawnHeight: 0.6f, activeCount: 5, speedRange: new Vector2(0.8f, 2f), edgeX: edgeX);
            CreateSpawner(back, "DotSpawnerBottom", prefab, new Vector3(edgeX, bottomY, dots[0].position.z),
                spawnWidth: 0.5f, spawnHeight: 0.6f, activeCount: 5, speedRange: new Vector2(0.8f, 2f), edgeX: edgeX);

            foreach (var d in dots) Undo.DestroyObjectImmediate(d.gameObject);
            log.Add($"- Converted {dots.Count} Dot tile(s) into a Dot prefab + 2 spawners (top/bottom).");
        }

        static void SetupBars(Transform front, float edgeX, List<string> log)
        {
            if (front.Find("BarSpawner") != null) return; // already migrated

            var bars = FindChildren(front, "Bar");
            if (bars.Count == 0) return;

            GameObject prefab = BuildPrefab(bars[0], "Bar");

            CreateSpawner(front, "BarSpawner", prefab, new Vector3(edgeX, 0f, bars[0].position.z),
                spawnWidth: 0.5f, spawnHeight: 0f, activeCount: 4, speedRange: new Vector2(3f, 6f), edgeX: edgeX);

            foreach (var b in bars) Undo.DestroyObjectImmediate(b.gameObject);
            log.Add($"- Converted {bars.Count} Bar tile(s) into a Bar prefab + 1 spawner.");
        }

        static List<Transform> FindChildren(Transform root, string name)
        {
            var found = new List<Transform>();
            foreach (Transform child in root)
                if (child.name == name) found.Add(child);
            return found;
        }

        static float AverageY(List<Transform> tiles, bool top, float fallback)
        {
            float sum = 0f; int count = 0;
            foreach (var t in tiles)
            {
                if (top && t.position.y >= 0f) { sum += t.position.y; count++; }
                else if (!top && t.position.y < 0f) { sum += t.position.y; count++; }
            }
            return count > 0 ? sum / count : fallback;
        }

        /// <summary>Strips ParallaxTile (movement is now Spawner/PooledMover's job) and adds PooledMover, then saves the (now-mutated) scene instance as a Prefab asset. The scene instance itself is destroyed by the caller right after.</summary>
        static GameObject BuildPrefab(Transform template, string name)
        {
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/Girgit", "Prefabs");

            string path = $"{PrefabDir}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            GameObject go = template.gameObject;
            var oldTile = go.GetComponent<ParallaxTile>();
            if (oldTile != null) Undo.DestroyObjectImmediate(oldTile);
            if (go.GetComponent<PooledMover>() == null) go.AddComponent<PooledMover>();

            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }

        static void CreateSpawner(Transform parent, string name, GameObject prefab, Vector3 pos,
                                   float spawnWidth, float spawnHeight, int activeCount, Vector2 speedRange, float edgeX)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Spawner");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var spawner = go.AddComponent<Spawner>();
            spawner.Prefab = prefab;
            spawner.SpawnWidth = spawnWidth;
            spawner.SpawnHeight = spawnHeight;
            spawner.ActiveCount = activeCount;
            spawner.SpeedRange = speedRange;
            spawner.DespawnDelay = 2f;
            spawner.InitialSpreadWidth = edgeX * 2f; // spread the first batch across roughly the visible width
        }
    }
}
#endif
