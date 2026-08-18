#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Girgit
{
    /// <summary>
    /// Loads (or creates, first time) TMP SDF Font Assets from Jaro's optical-size
    /// instances. Jaro ships separate hand-tuned instances for different point-size
    /// ranges (9/24/36/60pt) rather than one instance stretched across every size —
    /// <see cref="ForSize"/> picks the closest match for a given UI font size.
    ///
    /// Regular goes through <see cref="LoadOrCreateDefaultPadding"/> — TMP's plain
    /// single-arg CreateFontAsset call (atlasPadding=9) — because that exact call
    /// already produced a working asset; it's left untouched. The other 4 sizes
    /// clipped/bled with that same default (their bolder, larger strokes need more
    /// SDF padding), so they go through a deliberately SEPARATE method,
    /// <see cref="LoadOrCreateWiderPadding"/>, with its own independent copy of the
    /// generation logic and a larger padding value. Nothing is shared between the
    /// two beyond the essential-resources check, so tuning one can't regress the
    /// other.
    /// </summary>
    internal static class GirgitFonts
    {
        const string FontDir = "Assets/Girgit/Fonts";
        const string RequiredShader = "TextMeshPro/Mobile/Distance Field";

        // Asset names match exactly what Unity's own Font Asset Creator window
        // suggests by default ("{font name} SDF") when saved next to the source
        // TTF — see Girgit > Create Font Assets. That way, a manually-created
        // asset is picked up here automatically with zero renaming needed.
        public static TMP_FontAsset Regular() => LoadOrCreateDefaultPadding("Jaro-Regular", "Jaro-Regular SDF");

        public static TMP_FontAsset Size9() => LoadOrCreateWiderPadding("Jaro_9pt-Regular", "Jaro_9pt-Regular SDF", 16);
        public static TMP_FontAsset Size24() => LoadOrCreateWiderPadding("Jaro_24pt-Regular", "Jaro_24pt-Regular SDF", 16);
        public static TMP_FontAsset Size36() => LoadOrCreateWiderPadding("Jaro_36pt-Regular", "Jaro_36pt-Regular SDF", 20);
        public static TMP_FontAsset Size60() => LoadOrCreateWiderPadding("Jaro_60pt-Regular", "Jaro_60pt-Regular SDF", 24);

        /// <summary>Picks the Jaro optical-size instance closest to a given UI font size.</summary>
        public static TMP_FontAsset ForSize(float fontSize)
        {
            if (fontSize >= 60f) return Size60();
            if (fontSize >= 28f) return Size36();
            if (fontSize >= 16f) return Size24();
            return Size9();
        }

        // ------------------------------------------------------------------
        // Path A — Regular. The exact call that already worked. Untouched —
        // do not fold this into Path B or add shared helpers between them.
        // ------------------------------------------------------------------
        static TMP_FontAsset LoadOrCreateDefaultPadding(string ttfBaseName, string assetBaseName)
        {
            string tmpAssetPath = $"{FontDir}/{assetBaseName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
            if (existing != null) return existing;

            // TMP_FontAsset.CreateFontAsset internally does `new Material(Shader.Find(RequiredShader))`
            // with no null-check of its own — if TMP's Essential Resources were
            // never imported into this project, that shader doesn't exist yet
            // and it throws. Detect that up front instead of crashing.
            if (Shader.Find(RequiredShader) == null)
            {
                ImportEssentialResources();
                return null;
            }

            string ttfPath = $"{FontDir}/{ttfBaseName}.ttf";
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogError($"Girgit: source font not found at {ttfPath}");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(sourceFont);
            if (asset == null)
            {
                Debug.LogError($"Girgit: TMP_FontAsset.CreateFontAsset returned null for {ttfBaseName}");
                return null;
            }
            asset.name = assetBaseName;

            AssetDatabase.CreateAsset(asset, tmpAssetPath);
            if (asset.atlasTextures != null)
                foreach (var tex in asset.atlasTextures)
                    if (tex != null) AssetDatabase.AddObjectToAsset(tex, asset);
            if (asset.material != null)
                AssetDatabase.AddObjectToAsset(asset.material, asset);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(tmpAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
        }

        // ------------------------------------------------------------------
        // Path B — the other 4 sizes. A fully independent copy of the same
        // shell as Path A, deliberately NOT reusing it, using the full
        // CreateFontAsset overload with a wider atlasPadding so the bolder
        // Jaro instances stop clipping/bleeding at their glyph edges.
        // ------------------------------------------------------------------
        static TMP_FontAsset LoadOrCreateWiderPadding(string ttfBaseName, string assetBaseName, int atlasPadding)
        {
            string tmpAssetPath = $"{FontDir}/{assetBaseName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
            if (existing != null) return existing;

            if (Shader.Find(RequiredShader) == null)
            {
                ImportEssentialResources();
                return null;
            }

            string ttfPath = $"{FontDir}/{ttfBaseName}.ttf";
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (sourceFont == null)
            {
                Debug.LogError($"Girgit: source font not found at {ttfPath}");
                return null;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                atlasPadding,
                UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                1024,
                1024,
                TMPro.AtlasPopulationMode.Dynamic,
                true);
            if (asset == null)
            {
                Debug.LogError($"Girgit: TMP_FontAsset.CreateFontAsset returned null for {ttfBaseName}");
                return null;
            }
            asset.name = assetBaseName;

            AssetDatabase.CreateAsset(asset, tmpAssetPath);
            if (asset.atlasTextures != null)
                foreach (var tex in asset.atlasTextures)
                    if (tex != null) AssetDatabase.AddObjectToAsset(tex, asset);
            if (asset.material != null)
                AssetDatabase.AddObjectToAsset(asset.material, asset);

            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(tmpAssetPath);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(tmpAssetPath);
        }

        /// <summary>
        /// Package imports are processed over subsequent editor updates, not
        /// synchronously, so we can't just continue on and use the shader in
        /// this same call — trigger the import and ask for one more run.
        /// Shared by both paths above — this is an environment pre-check, not
        /// part of either generation pipeline.
        /// </summary>
        static void ImportEssentialResources()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_FontAsset).Assembly);
            string path = packageInfo != null
                ? Path.Combine(packageInfo.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage")
                : null;

            if (path != null && File.Exists(path))
            {
                AssetDatabase.ImportPackage(path, false);
                EditorUtility.DisplayDialog("Girgit",
                    "TextMeshPro's Essential Resources (its default shader) were never imported into this project — importing them now automatically.\n\nOnce that finishes (a few seconds), run the command again.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Girgit",
                    "TextMeshPro's Essential Resources are missing and couldn't be located automatically.\n\nPlease run Window > TextMeshPro > Import TMP Essential Resources, then try again.",
                    "OK");
            }
        }
    }
}
#endif
