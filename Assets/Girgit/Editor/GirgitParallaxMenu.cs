#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Girgit
{
    /// <summary>
    /// Bulk tools for the self-contained ParallaxTile setup — each tile moves,
    /// rotates, and screen-wraps on its own, with no parent coordinator.
    /// </summary>
    public static class GirgitParallaxMenu
    {
        static readonly string[] RootNames = { "Parallax_Back", "Parallax_Front" };

        /// <summary>
        /// One-time migration: adds ParallaxTile (with sensible defaults) to
        /// any child of Parallax_Back/Parallax_Front that doesn't already
        /// have one, and clears out the "Missing Script" leftovers from the
        /// now-deleted ParallaxLayer component on those two root objects.
        /// </summary>
        [MenuItem("Girgit/Setup Parallax Tiles")]
        public static void SetupTiles()
        {
            int added = 0, cleaned = 0, rootsFound = 0;

            foreach (var rootName in RootNames)
            {
                GameObject root = GameObject.Find(rootName);
                if (root == null) continue;
                rootsFound++;

                cleaned += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);

                foreach (Transform child in root.transform)
                {
                    if (child.GetComponent<ParallaxTile>() == null)
                    {
                        child.gameObject.AddComponent<ParallaxTile>();
                        added++;
                    }
                }
            }

            if (rootsFound == 0)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "No Parallax_Back / Parallax_Front objects found in this scene.", "OK");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Girgit",
                $"Added ParallaxTile to {added} tile(s), removed {cleaned} leftover missing-script component(s). Save the scene (Ctrl+S).",
                "OK");
        }

        [MenuItem("Girgit/Randomize Parallax Bars and Circles")]
        public static void RandomizeAll()
        {
            var tiles = Object.FindObjectsOfType<ParallaxTile>();
            if (tiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Girgit",
                    "No ParallaxTile components found in this scene — run 'Setup Parallax Tiles' first.", "OK");
                return;
            }

            foreach (var tile in tiles)
                tile.Randomize();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Girgit",
                $"Randomized {tiles.Length} tile(s). Save the scene (Ctrl+S).", "OK");
        }
    }
}
#endif
