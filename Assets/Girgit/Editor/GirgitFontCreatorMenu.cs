#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using TMPro.EditorUtilities;

namespace Girgit
{
    /// <summary>
    /// Opens Unity's own TMP Font Asset Creator window pre-loaded with a Jaro
    /// source font, so you can visually tune Padding / Atlas Resolution /
    /// Render Mode and generate the atlas yourself instead of relying on
    /// GirgitFonts' hardcoded-default fallback (TMP_FontAsset.CreateFontAsset).
    ///
    /// Click "Generate Font Atlas", adjust Padding if glyphs look clipped or
    /// bled together, then "Save" — accept the default filename/location it
    /// suggests ("{name} SDF" next to the source font in Assets/Girgit/Fonts).
    /// GirgitFonts looks for exactly that name, so your saved asset is picked
    /// up everywhere automatically, no further wiring needed.
    ///
    /// Menu: Girgit > Create Font Assets > ...
    /// </summary>
    public static class GirgitFontCreatorMenu
    {
        const string FontDir = "Assets/Girgit/Fonts";

        [MenuItem("Girgit/Create Font Assets/Jaro Regular")]
        public static void CreateRegular() => Open("Jaro-Regular.ttf");

        [MenuItem("Girgit/Create Font Assets/Jaro 9pt")]
        public static void Create9pt() => Open("Jaro_9pt-Regular.ttf");

        [MenuItem("Girgit/Create Font Assets/Jaro 24pt")]
        public static void Create24pt() => Open("Jaro_24pt-Regular.ttf");

        [MenuItem("Girgit/Create Font Assets/Jaro 36pt")]
        public static void Create36pt() => Open("Jaro_36pt-Regular.ttf");

        [MenuItem("Girgit/Create Font Assets/Jaro 60pt")]
        public static void Create60pt() => Open("Jaro_60pt-Regular.ttf");

        static void Open(string ttfFileName)
        {
            string path = $"{FontDir}/{ttfFileName}";
            var font = AssetDatabase.LoadAssetAtPath<Font>(path);
            if (font == null)
            {
                EditorUtility.DisplayDialog("Girgit", $"Font not found at {path}", "OK");
                return;
            }
            TMPro_FontAssetCreatorWindow.ShowFontAtlasCreatorWindow(font);
        }
    }
}
#endif
