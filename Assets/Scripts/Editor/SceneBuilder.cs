#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cybershi.EditorTools
{
    /// <summary>
    /// Собирает готовую демо-сцену одним кликом: меню <b>Cybershi → Build Sample Scene</b>.
    /// Создаёт все нужные ассеты (префабы снарядов и эффектов, 3 оружия, паттерн пуль,
    /// библиотеку звуков), расставляет игрока, врагов, землю/стены, зоны камеры и менеджеры,
    /// связывает все ссылки и сохраняет сцену в Assets/Scenes/SampleArena.unity.
    ///
    /// Это редакторный инструмент — он не входит в билд игры (папка Editor / #if UNITY_EDITOR).
    /// </summary>
    public static class SceneBuilder
    {
        private const string PrefabDir = "Assets/Prefabs";
        private const string WeaponDir = "Assets/Weapons";
        private const string PatternDir = "Assets/Patterns";
        private const string AudioDir = "Assets/Audio";
        private const string SceneDir = "Assets/Scenes";

        // Палитра плейсхолдеров.
        private static readonly Color PlayerColor = new Color(0.30f, 0.85f, 1.00f);
        private static readonly Color EnemyColor = new Color(1.00f, 0.35f, 0.45f);
        private static readonly Color BossColor = new Color(1.00f, 0.55f, 0.20f);
        private static readonly Color GroundColor = new Color(0.18f, 0.20f, 0.26f);
        private static readonly Color WallColor = new Color(0.28f, 0.30f, 0.38f);
        private static readonly Color PlayerBulletColor = new Color(1.00f, 0.95f, 0.40f);
        private static readonly Color EnemyBulletColor = new Color(1.00f, 0.30f, 0.85f);

        [MenuItem("Cybershi/Build Sample Scene", priority = 0)]
        public static void BuildSampleScene()
        {
            if (!EditorUtility.DisplayDialog("Cybershi",
                    "Создать демо-ассеты и новую сцену SampleArena? Несохранённые изменения текущей сцены будут потеряны.",
                    "Поехали", "Отмена"))
                return;

            EnsureFolder(PrefabDir);
            EnsureFolder(WeaponDir);
            EnsureFolder(PatternDir);
            EnsureFolder(AudioDir);
            EnsureFolder(SceneDir);

            // 1) Эффекты (нужны снарядам и игроку).
            var hitFx = CreateEffectPrefab("FX_Hit", PlayerBulletColor, 0.5f, 0.25f);
            var muzzleFx = CreateEffectPrefab("FX_Muzzle", new Color(1f, 0.9f, 0.6f), 0.6f, 0.12f);
            var slamFx = CreateEffectPrefab("FX_SlamShock", new Color(0.6f, 0.8f, 1f), 1.5f, 0.4f);
            var deathFx = CreateEffectPrefab("FX_Death", EnemyColor, 1.2f, 0.45f);

            // 2) Снаряды.
            var playerBullet = CreateProjectilePrefab("PlayerBullet", PlayerBulletColor, 0.35f, 30f, 14f, hitFx);
            var enemyBullet = CreateProjectilePrefab("EnemyBullet", EnemyBulletColor, 0.4f, 9f, 8f, hitFx);

            // 3) Оружие.
            var pistol = CreateWeapon("Pistol", "Револьвер", playerBullet, muzzleFx,
                damage: 18f, speed: 34f, fireRate: 4f, pellets: 1, spread: 1.5f, auto: false,
                knockback: 2f, shake: 0.12f, sound: SoundId.PlayerShootPistol);

            var shotgun = CreateWeapon("Shotgun", "Дробовик", playerBullet, muzzleFx,
                damage: 7f, speed: 28f, fireRate: 1.6f, pellets: 8, spread: 32f, auto: false,
                knockback: 6f, shake: 0.3f, sound: SoundId.PlayerShootShotgun);

            var smg = CreateWeapon("SMG", "Гвоздомёт", playerBullet, muzzleFx,
                damage: 6f, speed: 40f, fireRate: 12f, pellets: 1, spread: 5f, auto: true,
                knockback: 0.5f, shake: 0.05f, sound: SoundId.PlayerShootSMG);

            // 4) Паттерны пуль.
            var fanPattern = CreatePattern("Pattern_Fan", BulletPatternType.Fan, enemyBullet,
                count: 7, spread: 70f, speed: 9f, damage: 8f, spin: 0f);
            var spiralPattern = CreatePattern("Pattern_Spiral", BulletPatternType.Spiral, enemyBullet,
                count: 14, spread: 0f, speed: 7.5f, damage: 9f, spin: 17f);

            // 5) Библиотека звуков (пустая — клипы добавит пользователь).
            var soundLib = CreateSoundLibrary();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 6) Новая сцена.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildManagers(soundLib);
            var player = BuildPlayer(new[] { pistol, shotgun, smg }, slamFx);
            BuildCamera(player.transform);
            BuildEnvironment();
            BuildEnemy("Enemy_Drone", new Vector3(10f, 3f, 0f), EnemyColor, 1.2f, 40f, fanPattern, enemyBullet, deathFx, 1.1f);
            BuildBossArena(spiralPattern, enemyBullet, deathFx);
            BuildPerspectiveZone();

            // 7) Сохранить сцену.
            string scenePath = $"{SceneDir}/SampleArena.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.OpenScene(scenePath);

            Debug.Log("<color=cyan>Cybershi:</color> демо-сцена собрана → " + scenePath +
                      "\nНажмите Play. Управление см. в Docs/README.md");
            EditorUtility.DisplayDialog("Cybershi",
                "Готово! Сцена SampleArena открыта.\n\nНажмите Play.\nWASD — движение, мышь — прицел, ЛКМ — огонь,\nShift — рывок, Ctrl — подкат/слэм, Space — прыжок,\n1/2/3 — смена оружия.",
                "Ок");
        }

        // ============================================================== менеджеры

        private static void BuildManagers(SoundLibrary lib)
        {
            var go = new GameObject("--- Managers ---");

            var audio = new GameObject("AudioManager").AddComponent<AudioManager>();
            audio.transform.SetParent(go.transform);
            audio.library = lib;

            var music = new GameObject("DynamicMusicManager").AddComponent<DynamicMusicManager>();
            music.transform.SetParent(go.transform);
            // explorationTrack / combatTrack — пользователь назначит свои клипы.

            var combat = new GameObject("CombatStateTracker").AddComponent<CombatStateTracker>();
            combat.transform.SetParent(go.transform);

            var persp = new GameObject("PerspectiveManager").AddComponent<PerspectiveManager>();
            persp.transform.SetParent(go.transform);
        }

        // ============================================================== игрок

        private static PlayerController BuildPlayer(WeaponDefinition[] weapons, GameObject slamFx)
        {
            var root = new GameObject("Player");
            root.transform.position = new Vector3(0f, 2f, 0f);

            var rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.45f; col.center = Vector3.zero;

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

            var playerHealth = root.AddComponent<PlayerHealth>();
            playerHealth.flashRenderer = sr;

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

        // ============================================================== камера

        private static void BuildCamera(Transform target)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = false;
            cam.fieldOfView = 35f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 3f, -18f);

            var ctl = go.AddComponent<CameraController>();
            ctl.target = target;
        }

        // ============================================================== окружение

        private static void BuildEnvironment()
        {
            var root = new GameObject("--- Level ---").transform;

            // Пол.
            MakeBox(root, "Ground", new Vector3(20f, -0.5f, 0f), new Vector3(120f, 1f, 4f),
                new Vector2(120f, 1f), GroundColor, -5);

            // Несколько платформ для прыжков.
            MakeBox(root, "Platform_A", new Vector3(-6f, 2.5f, 0f), new Vector3(4f, 0.5f, 4f),
                new Vector2(4f, 0.5f), WallColor, -4);
            MakeBox(root, "Platform_B", new Vector3(4f, 4.5f, 0f), new Vector3(4f, 0.5f, 4f),
                new Vector2(4f, 0.5f), WallColor, -4);

            // Стены/пилоны для прыжка от стен (вид сбоку).
            MakeBox(root, "Wall_Left", new Vector3(-14f, 4f, 0f), new Vector3(1f, 10f, 4f),
                new Vector2(1f, 10f), WallColor, -4);
            MakeBox(root, "Wall_Right", new Vector3(16f, 4f, 0f), new Vector3(1f, 10f, 4f),
                new Vector2(1f, 10f), WallColor, -4);
        }

        // ============================================================== враги

        private static GameObject BuildEnemy(string name, Vector3 pos, Color color, float size,
            float hp, BulletPattern pattern, GameObject bulletPrefab, GameObject deathFx, float fireInterval)
        {
            var root = new GameObject(name);
            root.transform.position = pos;

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

            return root;
        }

        private static void BuildBossArena(BulletPattern spiral, GameObject bullet, GameObject deathFx)
        {
            var root = new GameObject("--- Boss Arena ---").transform;

            // Босс посильнее со спиральным паттерном.
            BuildEnemy("Boss", new Vector3(40f, 5f, 0f), BossColor, 2.2f, 220f, spiral, bullet, deathFx, 0.6f)
                .transform.SetParent(root);

            // Якорь статичного плана — общий вид арены.
            var anchor = new GameObject("BossCamAnchor").transform;
            anchor.SetParent(root);
            anchor.position = new Vector3(40f, 6f, -24f);
            anchor.rotation = Quaternion.identity;

            // Триггер-зона: при входе камера фиксируется на арене.
            var zoneGo = new GameObject("BossCameraZone");
            zoneGo.transform.SetParent(root);
            zoneGo.transform.position = new Vector3(40f, 5f, 0f);
            var box = zoneGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(24f, 18f, 6f);
            var zone = zoneGo.AddComponent<CameraZone>();
            zone.onEnter = CameraZone.Action.StaticFraming;
            zone.staticAnchor = anchor;
            zone.staticFov = 45f;
            zone.revertToFollowOnExit = true;
        }

        private static void BuildPerspectiveZone()
        {
            var root = new GameObject("--- Perspective Zone (demo 3D) ---").transform;

            var zoneGo = new GameObject("PerspectiveZone_3D");
            zoneGo.transform.SetParent(root);
            zoneGo.transform.position = new Vector3(70f, 5f, 0f);
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

        // ============================================================== фабрики ассетов

        private static GameObject CreateProjectilePrefab(string name, Color color, float size,
            float speed, float damage, GameObject hitFx)
        {
            var go = new GameObject(name);
            var proj = go.AddComponent<Projectile>();
            proj.speed = speed;
            proj.damage = damage;
            proj.lifeTime = 4f;
            proj.hitEffectPrefab = hitFx;

            MakeVisual(go.transform, PlaceholderVisual.Shape.Circle, color, new Vector2(size, size), 8);

            return SavePrefab(go, $"{PrefabDir}/{name}.prefab");
        }

        private static GameObject CreateEffectPrefab(string name, Color color, float size, float lifetime)
        {
            var go = new GameObject(name);
            var fx = go.AddComponent<OneShotEffect>();
            fx.lifetime = lifetime;

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
            w.damage = damage;
            w.projectileSpeed = speed;
            w.fireRate = fireRate;
            w.pelletsPerShot = pellets;
            w.spreadAngle = spread;
            w.automatic = auto;
            w.knockback = knockback;
            w.cameraShake = shake;
            w.fireSound = sound;
            return CreateAsset(w, $"{WeaponDir}/{assetName}.asset");
        }

        private static BulletPattern CreatePattern(string assetName, BulletPatternType type,
            GameObject bullet, int count, float spread, float speed, float damage, float spin)
        {
            var p = ScriptableObject.CreateInstance<BulletPattern>();
            p.type = type;
            p.bulletPrefab = bullet;
            p.count = count;
            p.spreadAngle = spread;
            p.bulletSpeed = speed;
            p.damage = damage;
            p.spinPerShot = spin;
            return CreateAsset(p, $"{PatternDir}/{assetName}.asset");
        }

        private static SoundLibrary CreateSoundLibrary()
        {
            var lib = ScriptableObject.CreateInstance<SoundLibrary>();
            var ids = (SoundId[])Enum.GetValues(typeof(SoundId));
            var entries = new System.Collections.Generic.List<SoundLibrary.Entry>();
            foreach (var id in ids)
            {
                if (id == SoundId.None) continue;
                entries.Add(new SoundLibrary.Entry { id = id, clips = Array.Empty<AudioClip>(), volume = 1f, pitchRandom = 0.05f });
            }
            lib.entries = entries.ToArray();
            return CreateAsset(lib, $"{AudioDir}/SoundLibrary.asset");
        }

        // ============================================================== низкоуровневые помощники

        private static GameObject MakeVisual(Transform parent, PlaceholderVisual.Shape shape,
            Color color, Vector2 size, int sortingOrder)
        {
            var go = new GameObject("Visual");
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            go.AddComponent<SpriteRenderer>();
            var pv = go.AddComponent<PlaceholderVisual>();
            pv.shape = shape;
            pv.color = color;
            pv.size = size;
            pv.sortingOrder = sortingOrder;
            pv.Apply();
            return go;
        }

        private static void MakeBox(Transform parent, string name, Vector3 pos, Vector3 colliderSize,
            Vector2 visualSize, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = pos;
            var box = go.AddComponent<BoxCollider>();
            box.size = colliderSize;
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
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        private static GameObject SavePrefab(GameObject go, string path)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                AssetDatabase.DeleteAsset(path);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
#endif
