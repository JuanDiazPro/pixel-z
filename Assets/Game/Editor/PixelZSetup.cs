using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// Configuración automática del juego Pixel Z.
//  Pixel Z > Configurar juego (todo): importa/corta sprites, crea la base de datos, la capa Enemy,
//                                     la escena MainMenu y el orden de escenas del build.
//  Pixel Z > Decorar castillo:        rellena el mapa 2 con decoración, luces y partículas.
public static class PixelZSetup
{
    private const string Pack1 = "Assets/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Characters(100x100 split)/";
    private const string Pack2 = "Assets/Tiny RPG Character Asset Pack 02 -Free Demon_A&Blood Monster_A/Characters(100x100 split)/";
    private const string Icons = "Assets/PixelUIIconsFree/Icons/";
    private const string KnightFx = "Assets/Knight Files/Effect & FireBalls/";
    private const string FxDir = "Assets/Game/Sprites/FX/";
    private const string DecoDir = "Assets/Game/Sprites/CastleDeco/";
    private const string DatabasePath = "Assets/Game/Resources/PixelZDatabase.asset";
    private const string MaterialDir = "Assets/Game/Materials/";
    private const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
    private const string GameScene = "Assets/Scenes/Game.unity";
    private const string CastleScene = "Assets/Scenes/Castle.unity";

    private static readonly Vector2 CharacterPivot = new Vector2(0.5f, 0.4f);

    [MenuItem("Pixel Z/Configurar juego (todo)", priority = 0)]
    public static void SetupAll()
    {
        ConfigureImports();
        BuildDatabase();
        EnsureEnemyLayer();
        CreateMainMenuScene();
        SetupBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log("[Pixel Z] Configuración completa. Abre Assets/Scenes/MainMenu.unity y pulsa Play.");
    }

    // ---------------- Importación ----------------

    private static void ConfigureImports()
    {
        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (string path in CharacterSheetPaths()) SliceSheet(path, 16f, CharacterPivot);

            foreach (string path in Directory.GetFiles(Icons, "*.png", SearchOption.AllDirectories))
                ConfigureSingle(path.Replace('\\', '/'), 32f, FilterMode.Point, new Vector2(0.5f, 0.5f));

            foreach (string name in new[] { "Menu_pixelZ.jpg", "PantallaDeCarga_pixelZ.jpg", "Pausa_pixelZ.jpg" })
                ConfigureSingle("Assets/Images/" + name, 100f, FilterMode.Bilinear, new Vector2(0.5f, 0.5f), 4096, true);

            ConfigureSingle(FxDir + "white.png", 4f, FilterMode.Point, new Vector2(0.5f, 0.5f));
            foreach (string n in new[] { "circle", "ring", "glow" })
                ConfigureSingle(FxDir + n + ".png", 64f, FilterMode.Bilinear, new Vector2(0.5f, 0.5f));
            ConfigureSingle(FxDir + "shadow.png", 32f, FilterMode.Bilinear, new Vector2(0.5f, 0.5f));

            ConfigureSingle("Assets/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Arrow(Projectile)/Arrow01(32x32).png", 32f, FilterMode.Point, new Vector2(0.5f, 0.5f));
            for (int i = 1; i <= 3; i++) ConfigureSingle(KnightFx + "fireball_0" + i + ".png", 150f, FilterMode.Bilinear, new Vector2(0.75f, 0.5f));
            for (int i = 1; i <= 6; i++) ConfigureSingle(KnightFx + "explosion_0" + i + ".png", 120f, FilterMode.Bilinear, new Vector2(0.5f, 0.5f));

            foreach (string path in Directory.GetFiles(DecoDir, "*.png"))
            {
                string p = path.Replace('\\', '/');
                bool rug = Path.GetFileName(p).StartsWith("rug");
                ConfigureSingle(p, 24f, FilterMode.Point, rug ? new Vector2(0.5f, 0.5f) : new Vector2(0.5f, 0f));
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }
        AssetDatabase.Refresh();
    }

    private static IEnumerable<string> CharacterSheetPaths()
    {
        foreach (string dir in new[] { Pack1 + "Soldier/Soldier with shadows", Pack1 + "Orc/Orc with shadows",
                                       Pack2 + "Demon_A/Demon_A with shadows", Pack2 + "Blood Monster_A/Blood Monster_A with shadows" })
            foreach (string f in Directory.GetFiles(dir, "*.png"))
                yield return f.Replace('\\', '/');
    }

    private static void SliceSheet(string path, float ppu, Vector2 pivot)
    {
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        if (ti == null) return;
        ti.GetSourceTextureWidthAndHeight(out int w, out int h);

        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Multiple;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dp = factory.GetSpriteEditorDataProviderFromObject(ti);
        dp.InitSpriteEditorDataProvider();

        var existing = dp.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        string baseName = Path.GetFileNameWithoutExtension(path);
        int frames = Mathf.Max(1, w / 100);
        var rects = new List<SpriteRect>();
        for (int i = 0; i < frames; i++)
        {
            string name = baseName + "_" + i;
            rects.Add(new SpriteRect
            {
                name = name,
                rect = new Rect(i * 100, 0, 100, h),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                spriteID = existing.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
            });
        }
        dp.SetSpriteRects(rects.ToArray());
        var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        ids?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        dp.Apply();
        ti.SaveAndReimport();
    }

    private static void ConfigureSingle(string path, float ppu, FilterMode filter, Vector2 pivot, int maxSize = 2048, bool compressed = false)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null)
        {
            Debug.LogWarning("[Pixel Z] No se encontró la textura " + path);
            return;
        }
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = ppu;
        ti.filterMode = filter;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.maxTextureSize = maxSize;
        ti.textureCompression = compressed ? TextureImporterCompression.CompressedHQ : TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(settings);
        ti.SaveAndReimport();
    }

    // ---------------- Base de datos ----------------

    private static void BuildDatabase()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
        var db = AssetDatabase.LoadAssetAtPath<PixelZDatabase>(DatabasePath);
        if (db == null)
        {
            db = ScriptableObject.CreateInstance<PixelZDatabase>();
            AssetDatabase.CreateAsset(db, DatabasePath);
        }

        db.soldier = LoadArt(Pack1 + "Soldier/Soldier with shadows/Soldier");
        db.orc = LoadArt(Pack1 + "Orc/Orc with shadows/Orc");
        db.demon = LoadArt(Pack2 + "Demon_A/Demon_A with shadows/Demon_A");
        db.bloodMonster = LoadArt(Pack2 + "Blood Monster_A/Blood Monster_A with shadows/Blood Monster_A");

        db.heart = LoadSprite(Icons + "stats/heart.png");
        db.heartEmpty = LoadSprite(Icons + "stats/heart_empty.png");
        db.mana = LoadSprite(Icons + "stats/mana.png");
        db.star = LoadSprite(Icons + "stats/star.png");
        db.coin = LoadSprite(Icons + "currency/coin.png");
        db.gem = LoadSprite(Icons + "currency/gem.png");
        db.key = LoadSprite(Icons + "currency/key.png");
        db.chest = LoadSprite(Icons + "currency/chest.png");
        db.potion = LoadSprite(Icons + "consumables/potion.png");
        db.apple = LoadSprite(Icons + "consumables/apple.png");
        db.sword = LoadSprite(Icons + "weapons/sword.png");
        db.bow = LoadSprite(Icons + "weapons/bow.png");
        db.shield = LoadSprite(Icons + "equipment/shield.png");
        db.helmet = LoadSprite(Icons + "equipment/helmet.png");
        db.gear = LoadSprite(Icons + "menu/gear.png");
        db.map = LoadSprite(Icons + "tools/map.png");

        db.whirlwind = LoadIcon(Icons + "Iconos Adicionales/Torbellino.png");
        db.arrowRain = LoadIcon(Icons + "Iconos Adicionales/lluvia_de_flechas.png");
        db.fury = LoadIcon(Icons + "Iconos Adicionales/Furia.png");
        db.dash = LoadIcon(Icons + "Iconos Adicionales/Dash.png");

        db.menuBackground = LoadSprite("Assets/Images/Menu_pixelZ.jpg");
        db.loadingBackground = LoadSprite("Assets/Images/PantallaDeCarga_pixelZ.jpg");
        db.pauseBackground = LoadSprite("Assets/Images/Pausa_pixelZ.jpg");

        db.white = LoadSprite(FxDir + "white.png");
        db.circle = LoadSprite(FxDir + "circle.png");
        db.ring = LoadSprite(FxDir + "ring.png");
        db.glow = LoadSprite(FxDir + "glow.png");
        db.arrow = LoadSprite("Assets/Tiny RPG Character Asset Pack 01 v2.0 -Free Soldier&Orc/Arrow(Projectile)/Arrow01(32x32).png");
        db.keyPrompt = LoadSprite("Assets/Game/Sprites/UI_KeyE.png");
        db.fireball = Enumerable.Range(1, 3).Select(i => LoadSprite(KnightFx + "fireball_0" + i + ".png")).ToArray();
        db.explosion = Enumerable.Range(1, 6).Select(i => LoadSprite(KnightFx + "explosion_0" + i + ".png")).ToArray();

        var chestOpen = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Props/PF Props - Chest 01 Open.prefab");
        if (chestOpen != null) db.chestOpen = chestOpen.GetComponentInChildren<SpriteRenderer>().sprite;

        db.unlitMaterial = UnlitMaterial();

        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
    }

    private static Material UnlitMaterial()
    {
        Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + "PZ Sprite Unlit.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        mat = new Material(shader);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material ParticleMaterial()
    {
        Directory.CreateDirectory(MaterialDir);
        string path = MaterialDir + "PZ Particle Glow.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
        mat = new Material(shader) { mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(FxDir + "glow.png") };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static CharacterArt LoadArt(string prefix)
    {
        return new CharacterArt
        {
            idle = Frames(prefix + "_Idle.png"),
            walk = Frames(prefix + "_Walk.png"),
            attack1 = Frames(prefix + "_Attack01.png"),
            attack2 = Frames(prefix + "_Attack02.png"),
            attack3 = Frames(prefix + "_Attack03.png"),
            hurt = Frames(prefix + "_Hurt.png"),
            death = Frames(prefix + "_Death.png"),
        };
    }

    private static Sprite[] Frames(string path)
    {
        if (!File.Exists(path)) return null;
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.rect.x).ToArray();
    }

    // Icono suelto (una imagen = un sprite). Si se importó como "Multiple", Unity lo trocea (p. ej. las chispas
    // de la Furia salían como sprites aparte), así que se fuerza a Single con mipmaps para que se vea bien pequeño.
    public static Sprite LoadIcon(string path)
    {
        if (AssetImporter.GetAtPath(path) is TextureImporter importer &&
            (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || !importer.mipmapEnabled))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return LoadSprite(path);
    }

    private static Sprite LoadSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s == null) Debug.LogWarning("[Pixel Z] Sprite no encontrado: " + path);
        return s;
    }

    // ---------------- Capa, escenas y build ----------------

    private static void EnsureEnemyLayer()
    {
        if (LayerMask.NameToLayer("Enemy") >= 0) return;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 23; i < 32; i++)
        {
            SerializedProperty p = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(p.stringValue)) continue;
            p.stringValue = "Enemy";
            tagManager.ApplyModifiedProperties();
            Debug.Log("[Pixel Z] Capa 'Enemy' creada en el índice " + i);
            return;
        }
        Debug.LogError("[Pixel Z] No hay huecos libres para la capa 'Enemy'.");
    }

    private static void CreateMainMenuScene()
    {
        if (File.Exists(MainMenuScene)) return;

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        SceneManager.MoveGameObjectToScene(camGo, scene);

        var menu = new GameObject("Main Menu");
        menu.AddComponent<MainMenu>();
        SceneManager.MoveGameObjectToScene(menu, scene);

        EditorSceneManager.SaveScene(scene, MainMenuScene);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static void SetupBuildSettings()
    {
        var scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MainMenuScene, true),
            new EditorBuildSettingsScene(GameScene, true),
            new EditorBuildSettingsScene(CastleScene, true),
        };
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------- Decoración del castillo ----------------

    [MenuItem("Pixel Z/Decorar castillo", priority = 20)]
    public static void DecorateCastle()
    {
        Scene scene = SceneManager.GetSceneByPath(CastleScene);
        bool openedHere = !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Additive);

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "DECOR - Pixel Z");
        if (old != null) Object.DestroyImmediate(old);

        CastleMap map = scene.GetRootGameObjects().Select(g => g.GetComponentInChildren<CastleMap>()).FirstOrDefault(m => m != null);
        if (map == null)
        {
            Debug.LogError("[Pixel Z] No hay CastleMap en la escena Castle.");
            return;
        }

        var decorator = new CastleDecorator(scene, map, ParticleMaterial());
        decorator.Run();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        Debug.Log("[Pixel Z] Castillo decorado: " + decorator.Count + " objetos nuevos.");
    }

    private class CastleDecorator
    {
        private readonly Scene scene;
        private readonly CastleMap map;
        private readonly Material particleMat;
        private readonly System.Random rng = new System.Random(4242);
        private readonly HashSet<Vector2Int> floor = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        private readonly List<Bounds> occupied = new List<Bounds>();
        private readonly List<Bounds> onWalls = new List<Bounds>();   // ventanas, estandartes: los objetos del suelo pueden ir delante
        private Transform root, decals, props, wallDeco, lights, particles;

        private SpriteRenderer bannerRef, barrelRef;
        public int Count;

        // Salas (centro, tamaño) según los ZoneMarker del castillo.
        private static readonly (string name, Vector2 center, Vector2 size)[] Rooms =
        {
            ("Entrance Hall", new Vector2(0f, -1f), new Vector2(16f, 10f)),
            ("Throne Room", new Vector2(0f, 17f), new Vector2(22f, 14f)),
            ("West Wing", new Vector2(-20f, 0f), new Vector2(12f, 10f)),
            ("East Wing", new Vector2(20f, 0f), new Vector2(12f, 10f)),
            ("Treasure Room", new Vector2(21f, 17f), new Vector2(8f, 8f)),
        };

        public CastleDecorator(Scene scene, CastleMap map, Material particleMat)
        {
            this.scene = scene;
            this.map = map;
            this.particleMat = particleMat;
        }

        public void Run()
        {
            root = new GameObject("DECOR - Pixel Z").transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);
            decals = Child("Floor Decals");
            props = Child("Props");
            wallDeco = Child("Wall Decoration");
            lights = Child("Lights");
            particles = Child("Ambient Particles");

            bannerRef = Prefab("Banner Red")?.GetComponent<SpriteRenderer>();
            barrelRef = Prefab("Barrel")?.GetComponent<SpriteRenderer>();

            map.floor.CompressBounds();
            foreach (Vector3Int c in map.floor.cellBounds.allPositionsWithin)
                if (map.floor.HasTile(c)) floor.Add(new Vector2Int(c.x, c.y));

            // Objetos existentes que no hay que tapar.
            foreach (GameObject g in scene.GetRootGameObjects())
                foreach (SpriteRenderer sr in g.GetComponentsInChildren<SpriteRenderer>())
                {
                    if (sr.name.StartsWith("Rug")) continue;
                    if (sr.name.StartsWith("Window") || sr.name.StartsWith("Banner") || sr.name.StartsWith("Arch")) onWalls.Add(sr.bounds);
                    else occupied.Add(sr.bounds);
                }

            AddFlickerToExistingLights();
            WallProps();
            WallHangings();
            FloorDecals();
            TreasurePile();
            ThroneDetails();
            WindowLights();
            AmbientParticles();
        }

        private Transform Child(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        private static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Castle/" + name + ".prefab");

        private static Sprite Deco(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(DecoDir + name + ".png");

        private float Rand() => (float)rng.NextDouble();
        private T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];

        private bool IsFloor(int x, int y) => floor.Contains(new Vector2Int(x, y));
        private Vector2 Center(Vector2Int c) => map.floor.GetCellCenterWorld(new Vector3Int(c.x, c.y, 0));

        private bool InRoom(Vector2 p, out string room)
        {
            foreach (var r in Rooms)
            {
                if (Mathf.Abs(p.x - r.center.x) <= r.size.x * 0.5f && Mathf.Abs(p.y - r.center.y) <= r.size.y * 0.5f)
                {
                    room = r.name;
                    return true;
                }
            }
            room = null;
            return false;
        }

        private bool Overlaps(Bounds b) => occupied.Any(o => o.Intersects(b));
        private bool OverlapsWall(Bounds b) => Overlaps(b) || onWalls.Any(o => o.Intersects(b));

        private SpriteRenderer MakeSprite(Transform parent, string name, Sprite sprite, Vector2 pos, string layer, int order, bool flip = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            sr.flipX = flip;
            Count++;
            return sr;
        }

        private void AddFlickerToExistingLights()
        {
            foreach (GameObject g in scene.GetRootGameObjects())
                foreach (Light2D l in g.GetComponentsInChildren<Light2D>())
                    if (l.lightType != Light2D.LightType.Global && l.GetComponent<LightFlicker>() == null && l.transform.parent != null &&
                        (l.transform.parent.name.Contains("Candle") || l.transform.parent.name.Contains("Candelabra")))
                        l.gameObject.AddComponent<LightFlicker>();
        }

        // Jarrones, barriles, cubos y candelabros pegados a la pared norte de cada sala.
        private void WallProps()
        {
            string[] small = { "vase_green_l", "vase_green_m", "vase_brown_l", "vase_brown_m", "barrel_tall", "bucket" };
            string[] tiny = { "vase_green_s", "vase_brown_s", "candle" };

            foreach (Vector2Int c in floor.OrderBy(c => c.x).ThenBy(c => c.y))
            {
                if (IsFloor(c.x, c.y + 1) || !IsFloor(c.x, c.y - 1) || !IsFloor(c.x, c.y - 2)) continue;
                Vector2 p = Center(c);
                if (!InRoom(p, out string room)) continue;
                if (Mathf.Abs(p.x) < 2.5f && room == "Throne Room") continue;   // pasillo central al trono
                if (Rand() > 0.42f) continue;

                Vector2 basePos = p + new Vector2((Rand() - 0.5f) * 0.3f, 0.05f);
                bool storage = room == "West Wing";
                string pick = storage && Rand() < 0.5f ? Pick(new[] { "barrel_tall", "bucket", "vase_brown_l" }) : Pick(small);

                Sprite s = Deco(pick);
                var b = new Bounds(basePos + Vector2.up * s.bounds.size.y * 0.5f, s.bounds.size);
                if (Overlaps(b)) continue;

                SpriteRenderer sr = MakeSprite(props, pick, s, basePos, "Layer 1", 2, Rand() < 0.5f);
                var col = sr.gameObject.AddComponent<BoxCollider2D>();
                col.size = new Vector2(s.bounds.size.x * 0.8f, 0.3f);
                col.offset = new Vector2(0f, 0.15f);
                occupied.Add(b);
                used.Add(c);

                // Objetos pequeños al lado (sin colisión).
                if (Rand() < 0.5f)
                {
                    Sprite t = Deco(Pick(tiny));
                    Vector2 tp = basePos + new Vector2(Rand() < 0.5f ? -0.55f : 0.55f, -0.05f);
                    var tb = new Bounds(tp + Vector2.up * 0.25f, t.bounds.size);
                    if (!Overlaps(tb))
                    {
                        MakeSprite(props, t.name, t, tp, "Layer 1", 2);
                        occupied.Add(tb);
                    }
                }
            }

            // Candelabros extra en las esquinas de las alas y en los pasillos.
            GameObject candelabra = Prefab("Candelabra");
            foreach (Vector2 pos in new[] { new Vector2(-17.5f, 4.2f), new Vector2(-22.5f, 4.2f), new Vector2(17.5f, 4.2f), new Vector2(22.5f, 4.2f),
                                            new Vector2(-6.5f, 22.4f), new Vector2(6.5f, 22.4f), new Vector2(-11f, 2.6f), new Vector2(11f, 2.6f) })
            {
                Vector3Int cell = map.floor.WorldToCell(pos);
                if (!IsFloor(cell.x, cell.y) || candelabra == null) continue;
                var b = new Bounds(pos + Vector2.up * 0.8f, new Vector2(0.8f, 1.6f));
                if (Overlaps(b)) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(candelabra, scene);
                go.transform.SetParent(props, true);
                go.transform.position = pos;
                foreach (Light2D l in go.GetComponentsInChildren<Light2D>())
                    if (l.GetComponent<LightFlicker>() == null) l.gameObject.AddComponent<LightFlicker>();
                occupied.Add(b);
                Count++;
            }
        }

        // Estandartes y armas colgadas en los muros frontales.
        private void WallHangings()
        {
            string layer = bannerRef != null ? bannerRef.sortingLayerName : "Layer 1";
            int order = bannerRef != null ? bannerRef.sortingOrder : 1;
            PixelZDatabase db = AssetDatabase.LoadAssetAtPath<PixelZDatabase>(DatabasePath);

            int lastX = int.MinValue;
            foreach (Vector2Int c in floor.Where(c => !IsFloor(c.x, c.y + 1)).OrderBy(c => c.y).ThenBy(c => c.x))
            {
                Vector2 p = Center(c);
                if (!InRoom(p, out string room)) continue;
                if (c.x - lastX < 3 && lastX != int.MinValue) continue;
                if (Rand() > 0.7f) continue;

                Vector2 wallPos = p + new Vector2(0f, 0.85f);
                Sprite s;
                if (room == "East Wing" && db != null)
                {
                    s = Rand() < 0.5f ? db.shield : db.sword;
                    wallPos += Vector2.up * 0.5f;
                }
                else
                {
                    bool red = room == "Throne Room" || room == "Treasure Room" || (room == "Entrance Hall" && Rand() < 0.5f);
                    s = Deco(red ? (Rand() < 0.5f ? "banner_small_red" : "banner_mid_red") : (Rand() < 0.5f ? "banner_small_teal" : "banner_mid_teal"));
                }
                if (s == null) continue;

                var b = new Bounds((Vector3)wallPos + Vector3.up * s.bounds.size.y * 0.5f, s.bounds.size * 0.9f);
                if (OverlapsWall(b)) continue;
                SpriteRenderer sr = MakeSprite(wallDeco, s.name, s, wallPos, layer, order);
                if (room == "East Wing") sr.transform.position = (Vector2)sr.transform.position - Vector2.up * s.bounds.size.y * 0.5f;
                onWalls.Add(b);
                lastX = c.x;
            }
        }

        // Huesos, calaveras y escombros por el suelo (decorativos, sin colisión).
        private void FloorDecals()
        {
            string[] bones = { "bones_a", "bones_b", "bones_c", "bones_d", "bones_e", "bones_f", "bones_g", "skull", "skull_small" };
            string[] rubble = { "rubble_med", "rubble_spike", "rubble_tall" };

            foreach (Vector2Int c in floor.OrderBy(c => c.x).ThenBy(c => c.y))
            {
                if (!IsFloor(c.x + 1, c.y) || !IsFloor(c.x - 1, c.y) || !IsFloor(c.x, c.y + 1) || !IsFloor(c.x, c.y - 1)) continue;
                Vector2 p = Center(c) + new Vector2((Rand() - 0.5f) * 0.6f, (Rand() - 0.5f) * 0.6f);
                if (p.y < -4.5f && Mathf.Abs(p.x) < 3f) continue;   // entrada despejada
                InRoom(p, out string room);

                float chance = room == null ? 0.05f : room == "Throne Room" ? 0.08f : 0.06f;
                if (Rand() > chance) continue;

                bool isRubble = Rand() < 0.2f;
                Sprite s = Deco(isRubble ? Pick(rubble) : Pick(bones));
                var b = new Bounds(p + Vector2.up * 0.3f, s.bounds.size * 0.8f);
                if (Overlaps(b)) continue;
                SpriteRenderer sr = MakeSprite(decals, s.name, s, p, "Default", -85, Rand() < 0.5f);
                sr.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                occupied.Add(b);

                // Pequeños montones de huesos alrededor de escombros grandes.
                if (isRubble && Rand() < 0.5f)
                    MakeSprite(decals, "skull_small", Deco("skull_small"), p + new Vector2(0.5f, -0.1f), "Default", -84);
            }

            // Montones grandes de huesos en las salas de combate.
            foreach (Vector2 p in new[] { new Vector2(17f, -3.5f), new Vector2(23.5f, -3.2f), new Vector2(-9f, 13f), new Vector2(9.5f, 21f), new Vector2(-22f, -2f) })
            {
                Vector3Int cell = map.floor.WorldToCell(p);
                if (!IsFloor(cell.x, cell.y)) continue;
                MakeSprite(decals, "bone_pile", Deco("bone_pile"), p, "Default", -84, Rand() < 0.5f);
            }
        }

        // Montón de monedas y gemas brillantes en la cámara del tesoro.
        private void TreasurePile()
        {
            PixelZDatabase db = AssetDatabase.LoadAssetAtPath<PixelZDatabase>(DatabasePath);
            if (db == null) return;
            var pile = Child("Treasure Pile");
            Vector2 center = new Vector2(21f, 16.5f);
            for (int i = 0; i < 28; i++)
            {
                float a = Rand() * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Rand()) * 1.6f;
                Vector2 p = center + new Vector2(Mathf.Cos(a) * r * 1.3f, Mathf.Sin(a) * r * 0.7f);
                Sprite s = Rand() < 0.75f ? db.coin : db.gem;
                SpriteRenderer sr = MakeSprite(pile, s.name, s, p, "Default", -80 + (int)(-p.y * 4f));
                sr.transform.localScale = Vector3.one * (0.55f + Rand() * 0.25f);
            }
            AddLight(pile, center + Vector2.up * 0.3f, new Color(1f, 0.82f, 0.35f), 0.9f, 3.2f, true);
        }

        private void ThroneDetails()
        {
            GameObject candle = Prefab("Candle");
            if (candle == null) return;
            // Círculo ritual de velas donde aparece el jefe.
            Vector2 c = new Vector2(0f, 18.5f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector2 p = c + new Vector2(Mathf.Cos(a) * 2.6f, Mathf.Sin(a) * 1.6f);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(candle, scene);
                go.transform.SetParent(props, true);
                go.transform.position = p;
                foreach (Light2D l in go.GetComponentsInChildren<Light2D>())
                    if (l.GetComponent<LightFlicker>() == null) l.gameObject.AddComponent<LightFlicker>();
                Count++;
            }
            AddLight(props, c, new Color(0.75f, 0.25f, 0.9f), 0.6f, 4f, true);
            MakeSprite(decals, "rug_teal", Deco("rug_teal"), new Vector2(-20f, 2.5f), "Default", -89);
        }

        // Luz fría que entra por los ventanales.
        private void WindowLights()
        {
            foreach (GameObject g in scene.GetRootGameObjects())
                foreach (SpriteRenderer sr in g.GetComponentsInChildren<SpriteRenderer>())
                    if (sr.name.StartsWith("Window"))
                        AddLight(lights, (Vector2)sr.transform.position + Vector2.down * 1.6f, new Color(0.55f, 0.65f, 1f), 0.45f, 2.6f, false);
        }

        private void AddLight(Transform parent, Vector2 pos, Color color, float intensity, float radius, bool flicker)
        {
            var go = new GameObject("Light 2D");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            if (flicker) go.AddComponent<LightFlicker>();
            Count++;
        }

        // Polvo flotando en cada sala y chispas mágicas en el salón del trono.
        private void AmbientParticles()
        {
            foreach (var r in Rooms)
                MakeParticles(r.name + " Dust", r.center, r.size, new Color(1f, 0.9f, 0.7f, 0.22f), (int)(r.size.x * r.size.y * 0.25f), 0.06f, 0.12f, 0.15f);

            MakeParticles("Throne Embers", new Vector2(0f, 18.5f), new Vector2(6f, 3f), new Color(0.85f, 0.4f, 1f, 0.6f), 25, 0.06f, 0.1f, 0.5f);
            MakeParticles("Treasure Sparkles", new Vector2(21f, 16.5f), new Vector2(4f, 2f), new Color(1f, 0.9f, 0.5f, 0.7f), 18, 0.05f, 0.09f, 0.3f);
        }

        private void MakeParticles(string name, Vector2 center, Vector2 size, Color color, int maxParticles, float minSize, float maxSize, float rise)
        {
            var go = new GameObject(name);
            go.transform.SetParent(particles, false);
            go.transform.position = center;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = maxParticles / 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.y, 0.1f);

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(rise * 0.3f, rise);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 0.3f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMat;
            renderer.sortingLayerName = "Layer 1";
            renderer.sortingOrder = 60;
            ps.Play();
            Count++;
        }
    }
}
