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
            public GameObject hitFx, muzzleFx, slamFx, deathFx, explosionFx, grazeFx;
            public GameObject playerBullet, nail, enemyBullet;
            public GameObject coin, magnet, ball, healOrb, tracer;
            public WeaponDefinition pistol, shotgun, smg, paddle;
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
                "WASD — движение, мышь — прицел, ЛКМ — огонь, ПКМ — альт-огонь\n" +
                "(монетка/накачка/магнит/мячик), Shift — рывок (парирует),\n" +
                "Ctrl — подкат/слэм, Space — прыжок, 1-4 — оружие, Esc — пауза.\n\n" +
                "Грейзьте пули для заряда мощного выстрела, отбивайте подсвеченные\nснаряды, убивайте в упор — лечит. Вправо — арена волн и босс.",
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
            b.explosionFx = CreateEffectPrefab("FX_Explosion", new Color(1f, 0.6f, 0.2f), 2.2f, 0.5f);
            b.grazeFx = CreateEffectPrefab("FX_Graze", new Color(0.4f, 1f, 1f), 0.5f, 0.2f);
            b.tracer = CreateTracerPrefab();

            b.playerBullet = CreateProjectilePrefab("PlayerBullet", PlayerBulletColor, 0.35f, 45f, 7f, b.hitFx, attractable: false);
            b.nail = CreateProjectilePrefab("Nail", new Color(0.85f, 0.9f, 1f), 0.25f, 55f, 6f, b.hitFx, attractable: true);
            b.enemyBullet = CreateProjectilePrefab("EnemyBullet", EnemyBulletColor, 0.4f, 9f, 8f, b.hitFx, attractable: false);

            b.coin = CreateCoinPrefab();
            b.magnet = CreateMagnetPrefab();
            b.ball = CreateBallPrefab(b.hitFx);
            b.healOrb = CreateHealOrbPrefab();

            CreateWeapons(b);

            b.fan = CreatePattern("Pattern_Fan", BulletPatternType.Fan, b.enemyBullet, 7, 70f, 9f, 8f, 0f, parryable: 0.35f);
            b.spiral = CreatePattern("Pattern_Spiral", BulletPatternType.Spiral, b.enemyBullet, 14, 0f, 7.5f, 9f, 17f, parryable: 0.15f);

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
            var player = BuildPlayer(b);
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
            new GameObject("StyleSystem").AddComponent<StyleSystem>().transform.SetParent(go.transform);
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

        private static PlayerController BuildPlayer(Built b)
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
            weaponCtl.weapons.AddRange(new[] { b.pistol, b.shotgun, b.smg, b.paddle });

            // Грейз и вампиризм.
            var graze = root.AddComponent<GrazeSystem>();
            graze.grazeEffectPrefab = b.grazeFx;
            var vamp = root.AddComponent<Vampirism>();
            vamp.healOrbPrefab = b.healOrb;

            var controller = root.AddComponent<PlayerController>();
            controller.visual = visual.transform;
            controller.slamEffectPrefab = b.slamFx;
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
            float speed, float damage, GameObject hitFx, bool attractable)
        {
            var go = new GameObject(name);
            var proj = go.AddComponent<Projectile>();
            proj.speed = speed; proj.damage = damage; proj.lifeTime = 4f; proj.hitEffectPrefab = hitFx;
            proj.attractable = attractable; // гвозди притягиваются магнитом
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, color, new Vector2(size, size), 8);
            return SavePrefab(go, $"{PrefabDir}/{name}.prefab");
        }

        private static GameObject CreateTracerPrefab()
        {
            var go = new GameObject("FX_Tracer");
            var tracer = go.AddComponent<TracerEffect>();
            tracer.lifetime = 0.08f;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Square, new Color(1f, 0.95f, 0.6f, 0.9f), Vector2.one, 9);
            return SavePrefab(go, $"{PrefabDir}/FX_Tracer.prefab");
        }

        private static GameObject CreateCoinPrefab()
        {
            var go = new GameObject("Coin");
            go.AddComponent<Coin>();
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true; // обычные снаряды игнорируют, хитскан ищет специально
            col.radius = 0.3f;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, new Color(1f, 0.85f, 0.2f), new Vector2(0.35f, 0.35f), 9);
            return SavePrefab(go, $"{PrefabDir}/Coin.prefab");
        }

        private static GameObject CreateMagnetPrefab()
        {
            var go = new GameObject("NailMagnet");
            go.AddComponent<NailMagnet>();
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, new Color(0.35f, 0.55f, 1f), new Vector2(0.6f, 0.6f), 9);
            return SavePrefab(go, $"{PrefabDir}/NailMagnet.prefab");
        }

        private static GameObject CreateBallPrefab(GameObject hitFx)
        {
            var go = new GameObject("PongBall");
            var ball = go.AddComponent<PongBall>();
            ball.hitEffectPrefab = hitFx;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, Color.white, new Vector2(0.45f, 0.45f), 9);
            return SavePrefab(go, $"{PrefabDir}/PongBall.prefab");
        }

        private static GameObject CreateHealOrbPrefab()
        {
            var go = new GameObject("HealOrb");
            go.AddComponent<HealOrb>();
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, new Color(0.4f, 1f, 0.5f), new Vector2(0.3f, 0.3f), 9);
            return SavePrefab(go, $"{PrefabDir}/HealOrb.prefab");
        }

        private static GameObject CreateEffectPrefab(string name, Color color, float size, float lifetime)
        {
            var go = new GameObject(name);
            go.AddComponent<OneShotEffect>().lifetime = lifetime;
            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, color, new Vector2(size, size), 12);
            return SavePrefab(go, $"{PrefabDir}/{name}.prefab");
        }

        /// <summary>Создаёт и настраивает все 4 оружия (полная конфигурация до сохранения ассета).</summary>
        private static void CreateWeapons(Built b)
        {
            // --- Револьвер: хитскан + монетка + мощный выстрел от грейза ---
            var p = ScriptableObject.CreateInstance<WeaponDefinition>();
            p.displayName = "Револьвер";
            p.kind = WeaponKind.Hitscan;
            p.damage = 20f; p.fireRate = 3.5f; p.automatic = false; p.knockback = 2f;
            p.hitscanRange = 80f; p.tracerPrefab = b.tracer; p.hitEffectPrefab = b.hitFx;
            p.allowPowerShot = true; p.powerShotDamageMult = 3f;
            p.closeBonusMult = 1f; p.falloffStart = 60f; p.falloffEnd = 80f; p.farMinMult = 0.8f; // почти ровный урон
            p.altFire = AltFireKind.Coin; p.altFirePrefab = b.coin;
            p.altFireCooldown = 0.7f; p.altFireLaunchSpeed = 9f;
            p.cameraShake = 0.12f; p.muzzleEffectPrefab = b.muzzleFx;
            p.fireSound = SoundId.PlayerShootPistol; p.altFireSound = SoundId.CoinToss;
            b.pistol = CreateAsset(p, $"{WeaponDir}/Pistol.asset");

            // --- Дробовик: случайный веер дроби, накачка (Pump), вампиризм ---
            var s = ScriptableObject.CreateInstance<WeaponDefinition>();
            s.displayName = "Дробовик";
            s.kind = WeaponKind.Projectile;
            s.damage = 7f; s.fireRate = 1.6f; s.automatic = false; s.knockback = 6f;
            s.projectilePrefab = b.playerBullet; s.projectileSpeed = 45f;
            s.pelletsPerShot = 8; s.spreadAngle = 30f; s.randomSpread = true; // дробинки под случайным углом
            s.closeBonusMult = 1.7f; s.closeRange = 4f; s.falloffStart = 8f; s.falloffEnd = 18f; s.farMinMult = 0.25f;
            s.lifestealOnKill = true; s.lifestealRange = 9f;
            s.altFire = AltFireKind.Pump; s.altFireCooldown = 0.35f;
            s.pumpMaxSafe = 3; s.pumpExplosionDamage = 45f; s.pumpExplosionRadius = 5f;
            s.explosionEffectPrefab = b.explosionFx;
            s.cameraShake = 0.3f; s.muzzleEffectPrefab = b.muzzleFx;
            s.fireSound = SoundId.PlayerShootShotgun; s.altFireSound = SoundId.PumpReload;
            b.shotgun = CreateAsset(s, $"{WeaponDir}/Shotgun.asset");

            // --- Гвоздомёт: ограниченный самовосстанавливающийся боезапас + магнит ---
            var n = ScriptableObject.CreateInstance<WeaponDefinition>();
            n.displayName = "Гвоздомёт";
            n.kind = WeaponKind.Projectile;
            n.damage = 6f; n.fireRate = 12f; n.automatic = true; n.knockback = 0.5f;
            n.projectilePrefab = b.nail; n.projectileSpeed = 55f;
            n.pelletsPerShot = 1; n.spreadAngle = 5f; n.randomSpread = true;
            n.maxAmmo = 80; n.ammoPerShot = 1; n.ammoRegenPerSecond = 9f; // восполняется сам со временем
            n.closeBonusMult = 1.1f; n.falloffStart = 18f; n.falloffEnd = 35f; n.farMinMult = 0.6f;
            n.altFire = AltFireKind.Magnet; n.altFirePrefab = b.magnet;
            n.altFireCooldown = 2.5f; n.altFireLaunchSpeed = 16f;
            n.cameraShake = 0.05f; n.muzzleEffectPrefab = b.muzzleFx;
            n.fireSound = SoundId.PlayerShootSMG; n.altFireSound = SoundId.MagnetDeploy;
            b.smg = CreateAsset(n, $"{WeaponDir}/SMG.asset");

            // --- Ракетка: ближний бой с парированием + мячик на ПКМ ---
            var m = ScriptableObject.CreateInstance<WeaponDefinition>();
            m.displayName = "Ракетка";
            m.kind = WeaponKind.Melee;
            m.damage = 35f; m.fireRate = 2.4f; m.automatic = false; m.knockback = 8f;
            m.meleeRange = 2.6f; m.meleeCanParry = true; m.meleeBallImpulse = 14f;
            m.closeBonusMult = 1f; m.falloffStart = 99f; m.falloffEnd = 100f; m.farMinMult = 1f;
            m.altFire = AltFireKind.Ball; m.altFirePrefab = b.ball;
            m.altFireCooldown = 1.2f; m.altFireLaunchSpeed = 16f;
            m.cameraShake = 0.15f; m.muzzleEffectPrefab = b.muzzleFx;
            m.fireSound = SoundId.MeleeSwing; m.altFireSound = SoundId.BallLaunch;
            b.paddle = CreateAsset(m, $"{WeaponDir}/Paddle.asset");
        }

        private static BulletPattern CreatePattern(string assetName, BulletPatternType type,
            GameObject bullet, int count, float spread, float speed, float damage, float spin, float parryable)
        {
            var p = ScriptableObject.CreateInstance<BulletPattern>();
            p.type = type; p.bulletPrefab = bullet; p.count = count; p.spreadAngle = spread;
            p.bulletSpeed = speed; p.damage = damage; p.spinPerShot = spin;
            p.parryableChance = parryable;
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
