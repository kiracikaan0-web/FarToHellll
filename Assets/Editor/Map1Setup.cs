using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Tek seferlik duzenleme: Tools > FarToHell > Map1'i Kur + Ana Menuyu Duzelt
///  1. Arkadasinin MainMenu sahnesine cizdigi harita (tilemap, karakter, Seytan Kiz, gri arka plan)
///     oldugu gibi Assets/Scenes/Map1.unity olarak kopyalanir.
///  2. MainMenu sahnesi yeniden sadece ana menu olarak kurulur; BASLA artik Map1'i acar.
///  3. circle, Level1 ve eski bolum denemesinin kullanilmayan dosyalari Geri Donusum Kutusu'na gider.
///     Arkadasinin kullandigi dosyalara (palette, platforms, RedDungeonSetSmooth, Platform_Red, BonePile) dokunulmaz.
///  4. Build Settings: 0 = MainMenu, 1 = Map1
/// Is bitince bu script kendini de siler.
/// </summary>
public static class Map1Setup
{
    private const string MenuScene = "Assets/Scenes/MainMenu.unity";
    private const string MapScene = "Assets/Scenes/Map1.unity";

    private static readonly string[] Trash =
    {
        "Assets/circle.unity",
        "Assets/Scenes/Level1.unity",
        "Assets/Levels",
        "Assets/Tiles",
        "Assets/Prefabs/Level",
        "Assets/Sprites/Tiles/Tile_RedRocks.png",
        "Assets/Sprites/Tiles/Tile_RedPlain.png",
        "Assets/Sprites/Tiles/Tile_RedLava.png",
        "Assets/Sprites/Tiles/Deco_SkullHut.png",
        "Assets/Sprites/Tiles/Hazard_SkullBall.png",
        "Assets/Scripts/World/LavaFall.cs",
        "Assets/Scripts/World/Pendulum.cs",
        "Assets/Scripts/World/MovingPlatform.cs",
        "Assets/Scripts/World/LevelMarker.cs",
        "Assets/Editor/LevelBuilder.cs",
        "Assets/Editor/LevelCleanup.cs",
    };

    [MenuItem("Tools/FarToHell/Map1'i Kur + Ana Menuyu Duzelt")]
    public static void Run()
    {
        if (File.Exists(MapScene))
        {
            EditorUtility.DisplayDialog("Map1", MapScene + " zaten var. Bu islem daha once yapilmis gorunuyor.", "Tamam");
            return;
        }
        if (!File.Exists(MenuScene)) { EditorUtility.DisplayDialog("Map1", MenuScene + " bulunamadi.", "Tamam"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Haritanin gercekten MainMenu'de oldugunu kontrol et
        var menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        int tileCount = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include)
            .Where(t => t.gameObject.scene == menu).Sum(t => t.GetUsedTilesCount());
        if (tileCount == 0)
        {
            EditorUtility.DisplayDialog("Map1", "MainMenu sahnesinde cizili tilemap bulunamadi. Hicbir sey degistirilmedi.", "Tamam");
            return;
        }

        var existing = Trash.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
        if (!EditorUtility.DisplayDialog("Map1'i Kur",
                "1) MainMenu'deki harita (tilemap, karakter, dusmanlar, arka plan) -> " + MapScene + "\n" +
                "2) MainMenu yeniden sadece ana menu olarak kurulur (BASLA -> Map1)\n" +
                "3) Geri Donusum Kutusu'na gidecekler:\n   " + string.Join("\n   ", existing) + "\n\n" +
                "Devam edilsin mi?", "Evet", "Vazgec"))
            return;

        // 1) Harita sahnesini kopyala
        if (!AssetDatabase.CopyAsset(MenuScene, MapScene))
        {
            EditorUtility.DisplayDialog("Map1", "Sahne kopyalanamadi: " + MapScene, "Tamam");
            return;
        }
        AssetDatabase.Refresh();

        // Map1: menu kamerasinin kayma scriptini kaldir, kamera karakteri takip etsin
        var map = EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
        var player = Object.FindAnyObjectByType<PlayerMovement>();
        foreach (var drift in Object.FindObjectsByType<MenuCameraDrift>(FindObjectsInactive.Include))
            Object.DestroyImmediate(drift);
        var cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.enabled = true;
            if (player != null)
            {
                var so = new SerializedObject(follow);
                so.FindProperty("target").objectReferenceValue = player.transform;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        EditorSceneManager.MarkSceneDirty(map);
        EditorSceneManager.SaveScene(map);

        // 2) Ana menuyu yeniden kur (Map1'e baglar, Build Settings'i ayarlar)
        if (!MainMenuSetup.Build(false))
        {
            EditorUtility.DisplayDialog("Map1", "Map1 olusturuldu ama ana menu kurulamadi. Tools > FarToHell > Ana Menu Kur'u elle calistir.", "Tamam");
            return;
        }

        // 3) Eski dosyalar
        EditorSceneManager.OpenScene(MapScene, OpenSceneMode.Single);
        var failed = new List<string>();
        foreach (var p in existing)
            if (!AssetDatabase.MoveAssetToTrash(p)) failed.Add(p);

        // 4) Build Settings: MainMenu, Map1, kalanlar (silinenler haric)
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuScene, true),
            new EditorBuildSettingsScene(MapScene, true),
        };
        foreach (var s in EditorBuildSettings.scenes)
            if (scenes.All(x => x.path != s.path) && File.Exists(s.path))
                scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("Map1 hazir",
            "Map1 acik: arkadasinin haritasi, karakter ve Seytan Kiz burada.\n" +
            "MainMenu artik sadece ana menu, BASLA Map1'i aciyor.\n" +
            "Build Settings: 0 = MainMenu, 1 = Map1\n" +
            (failed.Count > 0 ? "\nSilinemeyenler:\n   " + string.Join("\n   ", failed) + "\n" : "") +
            "\nSilinenler Geri Donusum Kutusu'nda. Simdi GitHub Desktop'tan commit + push yap,\n" +
            "arkadasin da calismadan once pull yapsin.", "Tamam");

        AssetDatabase.MoveAssetToTrash("Assets/Editor/Map1Setup.cs");
    }
}
