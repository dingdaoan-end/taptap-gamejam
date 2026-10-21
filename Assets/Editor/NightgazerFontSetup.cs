using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class NightgazerFontSetup
{
    const string Target = "Assets/Dialogue/Nightgazer16.asset";
    [MenuItem("Tools/UI Flow/Create Nightgazer Font")]
    public static void EnsureFont()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Target) != null) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Nightgazer-Bitmap-main/Nightgazer16.ttf");
            if (source == null) return;
            var font = TMP_FontAsset.CreateFontAsset(source, 16, 1, GlyphRenderMode.RASTER_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new Exception("Font creation failed.");
            font.name = "Nightgazer16";
            font.material.name = "Nightgazer16 Atlas Material";
            string corpus = string.Concat(Directory.GetFiles("Assets/Dialogue", "*.asset").Select(File.ReadAllText)) + File.ReadAllText("Assets/Scenes/Map.unity");
            corpus = Regex.Replace(corpus, @"\\u([0-9a-fA-F]{4})", m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            string characters = new string(corpus.Where(c => !char.IsControl(c)).Distinct().ToArray());
            font.TryAddCharacters(characters, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("Nightgazer missing characters: " + missing);
            AssetDatabase.CreateAsset(font, Target);
            foreach (var atlas in font.atlasTextures)
            {
                atlas.name = "Nightgazer16 Atlas";
                atlas.filterMode = FilterMode.Point;
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            AssetDatabase.AddObjectToAsset(font.material, font);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font.material, out string guid, out long materialId);
            File.WriteAllText("Library/NightgazerFontSetup.txt", "PASS\n" + guid + "\n" + materialId + "\nMissing: " + missing);
            Debug.Log("NIGHTGAZER_FONT_READY");
        }
        catch (Exception e) { Debug.LogException(e); File.WriteAllText("Library/NightgazerFontSetup.txt", e.ToString()); }
    }
}
