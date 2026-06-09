#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cybershi.EditorTools
{
    /// <summary>
    /// Сборщик демо: создаёт все ассеты (префабы снарядов/эффектов/врагов, 3 оружия, паттерны,
    /// библиотеку звуков) и собирает две сцены — главное меню и игровую арену с волнами врагов
    /// и боссом, HUD, паузой, зонами камеры и переходом в 3D. Прописывает обе сцены в Build Settings.
    ///
    /// Меню Unity:
    ///   • Cybershi → Build Game (Menu + Arena)  — собрать всё и открыть меню (рекомендуется);
    ///   • Cybershi → Build Sample Arena Only     — только арена;
    ///   • Cybershi → Build Main Menu Only         — только меню.
    /// </summary>
    public static class SceneBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string WeaponDir = "Assets/Weapons";
        private const string PatternDir = "Assets/Patterns";
        private const string AudioDir = "Assets/Audio";
        private const string SceneDir = "Assets/Scenes";
        private const string ArenaScene = "Assets/Scenes/SampleArena.unity";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";

        private static readonly Color PlayerColor = new Color(0.30f, 0.85f, 1.00f);
        private static readonly Color EnemyColor = new Color(1.00f, 0.35f, 0.45f);
        private static readonly Color BossColor = new Color(1.00f, 0.55f, 0.20f);
        private static readonly Color GroundColor = new Color(0.18f, 0.20f, 0.26f);
        private static readonly Color WallColor = new Color(0.28f, 0.30f, 0.38f);
        private static readonly Color PlayerBulletColor = new Color(1.00f, 0.95f, 0.40f);
        private static readonly Color EnemyBulletColor = new Color(1.00f, 0.30f, 0.85f);

        private class Built
        {
            public GameObject hitFx, muzzleFx, slamFx, deathFx;
            public GameObject playerBullet, enemyBullet;
            public WeaponDefinition pistol, shotgun, smg;
            public BulletPattern fan, spiral;
            public SoundLibrary soundLib;
            public GameObject drone, boss;
        }

        // ============================================================== пункты меню

        [MenuItem("Cybershi/Build Game (Menu + Arena)", priority = 0)]
        public static void BuildGame()
        {
            if (!Confirm("Создать ассеты и сцены (меню + арена)? Несохранённые изменения текущей сцены будут потеряны.")) return;

            var assets = CreateAllAssets();
            BuildArenaScene(assets);
            BuildMenuScene();
            SetBuildSettings(MenuScene, ArenaScene);
            EditorSceneManager.OpenScene(MenuScene);

            Debug.Log("<color=cyan>Cybershi:</color> игра собрана. Открыто главное меню — жмите Play. См. Docs/README.md");
            EditorUtility.DisplayDialog("Cybershi",
                "Готово! Открыто главное меню (MainMenu).\nНажмите Play → «Играть».\n\n" +
                "WASD — движение, мышь — прицел, ЛКМ — огонь,\nShift — рывок, Ctrl — подкат/слэм, Space — прыжок,\n" +
                "1/2/3 — оружие, Esc — пауза.\n\nЗайдите вправо в арену — пойдут волны врагов и босс.",
                "Ок");
        }

        [MenuItem("Cybershi/Build Sample Arena Only", priority = 1)]
        public static void BuildArenaOnly()
        {
            if (!Confirm("Создать ассеты и сцену арены?")) return;
            var assets = CreateAllAssets();
            BuildArenaScene(assets);
            EditorSceneManager.OpenScene(ArenaScene);
            EditorUtility.DisplayDialog("Cybershi", "Арена собрана и открыта. Нажмите Play.", "Ок");
        }

        [MenuItem("Cybershi/Build Main Menu Only", priority = 2)]
        public static void BuildMenuOnly()
        {
            if (!Confirm("Создать сцену главного меню?")) return;
            EnsureFolder(SceneDir);
            BuildMenuScene();
            SetBuildSettings(MenuScene, ArenaScene);
            EditorSceneManager.OpenScene(MenuScene);
        }

        private static bool Confirm(string msg) =>
            EditorUtility.DisplayDialog("Cybershi", msg, "Поехали", "Отмена");

        // ============================================================== создание ассетов

        private static Built CreateAllAssets()
        {
            EnsureFolder(PrefabDir);
            EnsureFolder(WeaponDir);
            EnsureFolder(PatternDir);
            EnsureFolder(AudioDir);
            EnsureFolder(SceneDir);

            var b = new Built();

            b.hitFx = CreateEffectPrefab("FX_Hit", PlayerBulletColor, 0.5f, 0.25f);
            b.muzzleFx = CreateEffectPrefab("FX_Muzzle", new Color(1f, 0.9f, 0.6f), 0.6f, 0.12f);
            b.slamFx = CreateEffectPrefab("FX_SlamShock", new Color(0.6f, 0.8f, 1f), 1.5f, 0.4f);
            b.deathFx = CreateEffectPrefab("FX_Death", EnemyColor, 1.2f, 0.45f);

            b.playerBullet = CreateProjectilePrefab("PlayerBullet", PlayerBulletColor, 0.35f, 30f, 14f, b.hitFx);
            b.enemyBullet = CreateProjectilePrefab("EnemyBullet", EnemyBulletColor, 0.4f, 9f, 8f, b.hitFx);

            b.pistol = CreateWeapon("Pistol", "Револьвер", b.playerBullet, b.muzzleFx, 18f, 34f, 4f, 1, 1.5f, false, 2f, 0.12f, SoundId.PlayerShootPistol);
            b.shotgun = CreateWeapon("Shotgun", "Дробовик", b.playerBullet, b.muzzleFx, 7f, 28f, 1.6f, 8, 32f, false, 6f, 0.3f, SoundId.PlayerShootShotgun);
            b.smg = CreateWeapon("SMG", "Гвоздомёт", b.playerBullet, b.muzzleFx, 6f, 40f, 12f, 1, 5f, true, 0.5f, 0.05f, SoundId.PlayerShootSMG);

            b.fan = CreatePattern("Pattern_Fan", BulletPatternType.Fan, b.enemyBullet, 7, 70f, 9f, 8f, 0f);
            b.spiral = CreatePattern("Pattern_Spiral", BulletPatternType.Spiral, b.enemyBullet, 14, 0f, 7.5f, 9f, 17f);

            b.soundLib = CreateSoundLibrary();

            // Префабы врагов (нужны паттерны и эффект смерти).
            b.drone = CreateEnemyPrefab("Enemy_Drone", EnemyColor, 1.2f, 40f, b.fan, b.deathFx, 1.1f, 8f, 5f);
            b.boss = CreateEnemyPrefab("Boss", BossColor, 2.4f, 240f, b.spiral, b.deathFx, 0.55f, 11f, 4f);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return b;
        }

        // ============================================================== игровая сцена

        private static void BuildArenaScene(Built b)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildManagers(b.soundLib);
            var player = BuildPlayer(new[] { b.pistol, b.shotgun, b.smg }, b.slamFx);
            BuildCamera(player.transform);
            BuildEnvironment();
            BuildUI();

            // Свободный враг у старта — сразу показывает бой и динамичную музыку.
            var freeEnemy = (GameObject)PrefabUtility.InstantiatePrefab(b.drone);
            freeEnemy.name = "Enemy_Drone (free)";
            freeEnemy.transform.position = new Vector3(11f, 3f, 0f);

            BuildWaveArena(b);
            BuildPerspectiveZone();

            EditorSceneManager.SaveScene(scene, ArenaScene);
        }

        private static void BuildManagers(SoundLibrary lib)
        {
            var go = new GameObject("--- Managers ---");

            var audio = new GameObject("AudioManager").AddComponent<AudioManager>();
            audio.transform.SetParent(go.transform);
            audio.library = lib;

            new GameObject("DynamicMusicManager").AddComponent<DynamicMusicManager>().transform.SetParent(go.transform);
            new GameObject("CombatStateTracker").AddComponent<CombatStateTracker>().transform.SetParent(go.transform);
            new GameObject("PerspectiveManager").AddComponent<PerspectiveManager>().transform.SetParent(go.transform);
            // InputReader создаётся сам (DontDestroyOnLoad) — отдельно ставить не нужно.
        }

        private static void BuildUI()
        {
            var go = new GameObject("--- UI ---");
            new GameObject("HUD").AddComponent<GameHUD>().transform.SetParent(go.transform);
            var pause = new GameObject("PauseMenu").AddComponent<PauseMenu>();
            pause.transform.SetParent(go.transform);
            pause.mainMenuSceneName = "MainMenu";
        }

        private static PlayerController BuildPlayer(WeaponDefinition[] weapons, GameObject slamFx)
        {
            var root = new GameObject("Player");
            root.transform.position = new Vector3(0f, 2f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.45f;

            var visual = MakeVisual(root.transform, PlaceholderVisual.Shape.Square, PlayerColor, new Vector2(1f, 2f), 10);
            var sr = visual.GetComponent<SpriteRenderer>();
            visual.AddComponent<Billboard>();
            visual.AddComponent<SpriteAnimator>();

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(root.transform);
            firePoint.localPosition = new Vector3(0.5f, 0.2f, 0f);

            var health = root.AddComponent<Health>();
            health.faction = Faction.Player;
            health.maxHealth = 100f;
            health.destroyOnDeath = false;

            root.AddComponent<PlayerHealth>().flashRenderer = sr;

            var aiming = root.AddComponent<PlayerAiming>();
            aiming.firePoint = firePoint;

            var weaponCtl = root.AddComponent<WeaponController>();
            weaponCtl.firePoint = firePoint;
            weaponCtl.ownerFaction = Faction.Player;
            weaponCtl.weapons.AddRange(weapons);

            var controller = root.AddComponent<PlayerController>();
            controller.visual = visual.transform;
            controller.slamEffectPrefab = slamFx;
            return controller;
        }

        private static void BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            cam.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 3f, -18f);
            go.AddComponent<CameraController>().target = target;
        }

        private static void BuildEnvironment()
        {
            var root = new GameObject("--- Level ---").transform;
            MakeBox(root, "Ground", new Vector3(30f, -0.5f, 0f), new Vector3(160f, 1f, 4f), new Vector2(160f, 1f), GroundColor, -5);
            MakeBox(root, "Platform_A", new Vector3(-6f, 2.5f, 0f), new Vector3(4f, 0.5f, 4f), new Vector2(4f, 0.5f), WallColor, -4);
            MakeBox(root, "Platform_B", new Vector3(4f, 4.5f, 0f), new Vector3(4f, 0.5f, 4f), new Vector2(4f, 0.5f), WallColor, -4);
            MakeBox(root, "Wall_Left", new Vector3(-14f, 4f, 0f), new Vector3(1f, 10f, 4f), new Vector2(1f, 10f), WallColor, -4);
            MakeBox(root, "Wall_Right", new Vector3(18f, 4f, 0f), new Vector3(1f, 10f, 4f), new Vector2(1f, 10f), WallColor, -4);
        }

        private static void BuildWaveArena(Built b)
        {
            var root = new GameObject("--- Wave Arena ---").transform;

            // Точки спавна по дуге над ареной.
            var spawnPoints = new List<Transform>();
            float[] xs = { 32f, 40f, 48f, 36f, 44f };
            float[] ys = { 7f, 9f, 7f, 11f, 11f };
            for (int i = 0; i < xs.Length; i++)
            {
                var sp = new GameObject($"Spawn_{i}").transform;
                sp.SetParent(root);
                sp.position = new Vector3(xs[i], ys[i], 0f);
                spawnPoints.Add(sp);
            }

            // Якорь камеры — общий план арены.
            var anchor = new GameObject("ArenaCamAnchor").transform;
            anchor.SetParent(root);
            anchor.position = new Vector3(40f, 6f, -26f);

            // Триггер-зона арены с волнами.
            var zoneGo = new GameObject("WaveArenaZone");
            zoneGo.transform.SetParent(root);
            zoneGo.transform.position = new Vector3(40f, 6f, 0f);
            var box = zoneGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(26f, 20f, 6f);

            var enc = zoneGo.AddComponent<WaveEncounter>();
            enc.encounterName = "АРЕНА";
            enc.spawnPoints = spawnPoints.ToArray();
            enc.lockCameraStatic = true;
            enc.cameraAnchor = anchor;
            enc.cameraFov = 52f;
            enc.timeBetweenWaves = 1.5f;
            enc.waves = new[]
            {
                new WaveDefinition { label = "Волна 1", startDelay = 0.6f,
                    spawns = new[] { new SpawnEntry { enemyPrefab = b.drone, count = 3 } } },
                new WaveDefinition { label = "Волна 2", startDelay = 0.5f,
                    spawns = new[] { new SpawnEntry { enemyPrefab = b.drone, count = 4 } } },
                new WaveDefinition { label = "Босс", startDelay = 1f,
                    spawns = new[]
                    {
                        new SpawnEntry { enemyPrefab = b.boss, count = 1, isBoss = true },
                        new SpawnEntry { enemyPrefab = b.drone, count = 2 }
                    } },
            };
        }

        private static void BuildPerspectiveZone()
        {
            var root = new GameObject("--- Perspective Zone (demo 3D) ---").transform;
            var zoneGo = new GameObject("PerspectiveZone_3D");
            zoneGo.transform.SetParent(root);
            zoneGo.transform.position = new Vector3(75f, 5f, 0f);
            var box = zoneGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(16f, 18f, 6f);

            var zone = zoneGo.AddComponent<CameraZone>();
            zone.onEnter = CameraZone.Action.Follow;
            zone.changePerspective = true;
            zone.perspectiveOnEnter = PerspectiveMode.ThreeD;
            zone.revertPerspectiveOnExit = true;
            zone.perspectiveOnExit = PerspectiveMode.Side2D;
            zone.revertToFollowOnExit = true;
        }

        // ============================================================== сцена меню

        private static void BuildMenuScene()
        {
            EnsureFolder(SceneDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            camGo.AddComponent<AudioListener>();

            var menu = new GameObject("MainMenu").AddComponent<MainMenuController>();
            menu.gameSceneName = "SampleArena";

            EditorSceneManager.SaveScene(scene, MenuScene);
        }

        private static void SetBuildSettings(string menuPath, string arenaPath)
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(menuPath, true),
                new EditorBuildSettingsScene(arenaPath, true),
            };
        }

        // ============================================================== фабрики ассетов

        private static GameObject CreateEnemyPrefab(string name, Color color, float size, float hp,
            BulletPattern pattern, GameObject deathFx, float fireInterval, float preferredDistance, float moveSpeed)
        {
            var root = new GameObject(name);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = size * 1.8f; col.radius = size * 0.5f;

            MakeVisual(root.transform, PlaceholderVisual.Shape.Square, color, new Vector2(size, size), 5);

            var health = root.AddComponent<Health>();
            health.faction = Faction.Enemy;
            health.maxHealth = hp;
            health.destroyOnDeath = true;
            health.deathEffectPrefab = deathFx;

            var emitterGo = new GameObject("Emitter");
            emitterGo.transform.SetParent(root.transform);
            emitterGo.transform.localPosition = Vector3.zero;
            var emitter = emitterGo.AddComponent<BulletEmitter>();
            emitter.pattern = pattern;
            emitter.ownerFaction = Faction.Enemy;

            var enemy = root.AddComponent<SimpleEnemy>();
            enemy.emitter = emitter;
            enemy.fireInterval = fireInterval;
            enemy.preferredDistance = preferredDistance;
            enemy.moveSpeed = moveSpeed;

            return SavePrefab(root, $"{PrefabDir}/{name}.prefab");
        }

        private static GameObject CreateProjectilePrefab(string name, Color color, float size,
            float speed, float damage, GameObject hitFx)
        {
            var go = new GameObject(name);
            var proj = go.AddComponent<Projectile>();
            proj.speed = speed; proj.damage = damage; proj.lifeTime = 4f; proj.hitEffectPrefab = hitFx;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, color, new Vector2(size, size), 8);
            return SavePrefab(go, $"{PrefabDir}/{name}.prefab");
        }

        private static GameObject CreateEffectPrefab(string name, Color color, float size, float lifetime)
        {
            var go = new GameObject(name);
            go.AddComponent<OneShotEffect>().lifetime = lifetime;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, color, new Vector2(size, size), 12);
            return SavePrefab(go, $"{PrefabDir}/{name}.prefab");
        }

        private static WeaponDefinition CreateWeapon(string assetName, string displayName,
            GameObject bullet, GameObject muzzle, float damage, float speed, float fireRate,
            int pellets, float spread, bool auto, float knockback, float shake, SoundId sound)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            w.displayName = displayName;
            w.projectilePrefab = bullet;
            w.muzzleEffectPrefab = muzzle;
            w.damage = damage; w.projectileSpeed = speed; w.fireRate = fireRate;
            w.pelletsPerShot = pellets; w.spreadAngle = spread; w.automatic = auto;
            w.knockback = knockback; w.cameraShake = shake; w.fireSound = sound;
            return CreateAsset(w, $"{WeaponDir}/{assetName}.asset");
        }

        private static BulletPattern CreatePattern(string assetName, BulletPatternType type,
            GameObject bullet, int count, float spread, float speed, float damage, float spin)
        {
            var p = ScriptableObject.CreateInstance<BulletPattern>();
            p.type = type; p.bulletPrefab = bullet; p.count = count; p.spreadAngle = spread;
            p.bulletSpeed = speed; p.damage = damage; p.spinPerShot = spin;
            return CreateAsset(p, $"{PatternDir}/{assetName}.asset");
        }

        private static SoundLibrary CreateSoundLibrary()
        {
            var lib = ScriptableObject.CreateInstance<SoundLibrary>();
            var entries = new List<SoundLibrary.Entry>();
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (id == SoundId.None) continue;
                entries.Add(new SoundLibrary.Entry { id = id, clips = Array.Empty<AudioClip>(), volume = 1f, pitchRandom = 0.05f });
            }
            lib.entries = entries.ToArray();
            return CreateAsset(lib, $"{AudioDir}/SoundLibrary.asset");
        }

        // ============================================================== помощники

        private static GameObject MakeVisual(Transform parent, PlaceholderVisual.Shape shape,
            Color color, Vector2 size, int sortingOrder)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<SpriteRenderer>();
            var pv = go.AddComponent<PlaceholderVisual>();
            pv.shape = shape; pv.color = color; pv.size = size; pv.sortingOrder = sortingOrder;
            pv.Apply();
            return go;
        }

        private static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 colliderSize,
            Vector2 visualSize, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            go.AddComponent<BoxCollider>().size = colliderSize;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Square, color, visualSize, sortingOrder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static T CreateAsset<T>(T obj, string path) where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null) AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
#endif
