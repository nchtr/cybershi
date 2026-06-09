# Cybershi — динамичный шутер вид-сбоку с буллет-хеллом и сменой перспективы

Документация к каркасу игры на Unity: быстрый шутер с видом сбоку, мувсетом в духе **V1 из ULTRAKILL**
(рывок, подкат, высокий прыжок, прыжок от стен, граунд-слэм), элементами **bullet-hell как в Touhou**,
тремя видами оружия, динамичной музыкой, системой звуков и **сменой ракурса вплоть до перехода в 3D**.

Вся графика — на **плейсхолдерах** (цветные квадраты/круги, генерируются в коде) и заменяется на ваши
спрайты в один клик. Звуки и музыку вы привязываете сами — ничего не синтезируется.

---

## Содержание
1. [Что это и как устроено](#1-что-это-и-как-устроено)
2. [Требования и установка](#2-требования-и-установка)
3. [Быстрый старт — сборка демо-сцены](#3-быстрый-старт--сборка-демо-сцены)
4. [Управление](#4-управление)
5. [Архитектура и список скриптов](#5-архитектура-и-список-скриптов)
6. [Графика: замена плейсхолдеров на спрайты](#6-графика-замена-плейсхолдеров-на-спрайты)
7. [Анимации и циклы спрайтов](#7-анимации-и-циклы-спрайтов)
8. [Эффекты](#8-эффекты)
9. [Звуки (SFX)](#9-звуки-sfx)
10. [Динамическая музыка](#10-динамическая-музыка)
11. [Оружие](#11-оружие)
12. [Враги и паттерны буллет-хелла](#12-враги-и-паттерны-буллет-хелла)
13. [Камера: следование и статичные планы](#13-камера-следование-и-статичные-планы)
14. [Перспектива и переход в 3D](#14-перспектива-и-переход-в-3d)
15. [Здоровье, урон, фракции](#15-здоровье-урон-фракции)
16. [Тонкая настройка ощущений](#16-тонкая-настройка-ощущений)
17. [Известные ограничения и идеи развития](#17-известные-ограничения-и-идеи-развития)
18. [Решение проблем (FAQ)](#18-решение-проблем-faq)
19. [Ввод (Input System)](#19-ввод-input-system)
20. [Меню, настройки, пауза, HUD](#20-меню-настройки-пауза-hud)
21. [Волны врагов и босс](#21-волны-врагов-и-босс)

---

## 1. Что это и как устроено

Это **набор скриптов и редакторный сборщик сцены**, а не готовый `.unity`-проект. Папку `Assets/`
нужно положить в Unity-проект, после чего нажать пункт меню, который автоматически создаст все ассеты
(префабы, оружие, паттерны, библиотеку звуков) и соберёт играбельную демо-сцену.

Ключевые архитектурные решения (важно понимать при доработке):

- **Физика — 3D `Rigidbody`, но в режиме «вид сбоку» ось Z заморожена.** Так движение происходит в
  плоскости XY (классический сайд-скроллер), но при переходе в 3D ось Z **разблокируется** — перспектива
  меняется по-настоящему, а не косметически.
- **Гравитация — своя, не штатная** (`Rigidbody.useGravity = false`). Это нужно для отзывчивого
  «ультракилловского» ощущения: высокий прыжок, регулируемая высота, резкое падение.
- **Камера — перспективная, но в 2D стоит далеко с маленьким FOV**, что выглядит почти как ортография.
  Благодаря этому переход 2D↔3D — плавный облёт, а не резкое переключение проекции.
- **Спрайты-плейсхолдеры генерируются в рантайме** (`PlaceholderFactory`) и пересоздаются при загрузке,
  поэтому их не нужно хранить как ассеты. Замена на настоящую графику — присвоить свой `Sprite`.
- **Снаряды летят без коллайдеров, через `Raycast` по пути.** Это исключает «протыкание» быстрых пуль
  сквозь стены и делает буллет-хелл дешёвым (пули не сталкиваются между собой).
- **Пул объектов (`PoolManager`)** переиспользует снаряды и эффекты — сотни пуль на экране без мусора в GC.
- **Слои и теги не требуются** (кроме штатного `MainCamera`): проверки земли/стен и попаданий
  отсеивают собственные коллайдеры по иерархии.

---

## 2. Требования и установка

- **Unity 6 (6000.0+) рекомендуется.** В коде используется `Rigidbody.linearVelocity` — это API Unity 6.
  На Unity 2022 замените `linearVelocity` → `velocity` (в `PlayerController.cs` и `SimpleEnemy.cs`).
- **Пакет Input System** (`com.unity.inputsystem`) — игра работает на **новой системе ввода**.
- **Встроенный рендер-пайплайн (Built-in) или URP** — спрайты используют unlit-шейдер, свет не нужен.

Установка:
1. Создайте новый Unity-проект (шаблон 2D или 3D — без разницы).
2. **Установите пакет Input System**: Window → Package Manager → Unity Registry → *Input System* → Install.
3. **Включите новый ввод**: Project Settings → Player → **Active Input Handling = Both** (или
   *Input System Package (New)*). Unity предложит перезапуститься — согласитесь.
4. Скопируйте папку `Assets/` из этого репозитория в `Assets/` вашего проекта.
5. Дождитесь компиляции.

> **Про ввод и совместимость.** Весь ввод проходит через единый `InputReader`. Если пакет Input System
> установлен и активен (определён символ `ENABLE_INPUT_SYSTEM`) — используется новая система (опрос
> Keyboard/Mouse/Gamepad). Если нет — `InputReader` автоматически падает на классический `UnityEngine.Input`,
> поэтому проект компилируется в любом случае. Для кнопок меню под новой системой нужен Input System ≥ 1.4
> (метод `AssignDefaultActions`) — в Unity 6 он есть «из коробки».

---

## 3. Быстрый старт — сборка демо-сцены

1. В верхнем меню Unity выберите **Cybershi → Build Game (Menu + Arena)**.
2. Подтвердите создание. Скрипт:
   - создаст папки `Assets/Prefabs`, `Assets/Weapons`, `Assets/Patterns`, `Assets/Audio`, `Assets/Scenes`;
   - сгенерирует префабы снарядов, эффектов и **врагов (дрон + босс)**, 3 оружия, 2 паттерна пуль,
     пустую библиотеку звуков;
   - соберёт **две сцены**: `MainMenu.unity` (главное меню) и `SampleArena.unity` (игра с HUD, паузой,
     ареной волн, боссом и зоной 3D), пропишет обе в **Build Settings**;
   - откроет главное меню.
3. Нажмите **Play** → «Играть».

Доступны и отдельные пункты: **Build Sample Arena Only** (только арена) и **Build Main Menu Only**.

Что попробовать в демо:
- В **главном меню** — «Настройки» (громкости/полный экран сохраняются между запусками).
- В игре: свободный дрон у старта включает бой и боевую музыку (если привяжете треки).
- Уйти вправо в **арену волн** (~x = 27…53): камера зафиксируется на общем плане, пойдут **волны врагов**,
  последняя — **босс** (со своей полоской здоровья). Счётчик волн виден в HUD.
- Ещё правее (~x = 75) — **зона перспективы**: камера уходит за спину, открывается 3D-движение.
- Стены слева (x ≈ −14) и справа (x ≈ 18) — **прыжки от стен с обеих сторон**.
- **Esc** — пауза.

Повторный запуск сборщика безопасен — он перезаписывает свои ассеты и создаёт сцены заново.

---

## 4. Управление

| Действие | Клавиша |
|---|---|
| Движение | **A / D** (в 3D — **W/A/S/D**) |
| Прицеливание | **Мышь** |
| Огонь | **ЛКМ** (зажать для авто-оружия) |
| Смена оружия | **1 / 2 / 3** или **колесо мыши** |
| Прыжок (высокий, регулируемый) | **Space** |
| Прыжок от стены | **Space** у стены в воздухе |
| Рывок (Dash, с неуязвимостью, 3 заряда) | **Left Shift** |
| Подкат (Slide) — на земле | **Left Ctrl** / геймпад B |
| Граунд-слэм — в воздухе | **Left Ctrl** (контекст определяется автоматически) |
| Прыжок-отскок после слэма | **Space** сразу после приземления |
| Пауза | **Esc** / Start |

Поддерживается **геймпад** (левый стик — движение, ЛКМ↔правый триггер, A — прыжок, X — рывок,
B — подкат/слэм, бамперы — смена оружия). Раскладка задаётся в `InputReader` (см. раздел 19 ниже про ввод).

---

## 5. Архитектура и список скриптов

Все скрипты — в namespace `Cybershi` (редакторный сборщик — в `Cybershi.EditorTools`).

```
Assets/Scripts/
├── Core/
│   ├── Faction.cs          // enum Faction, struct DamageInfo, interface IDamageable
│   ├── Health.cs           // здоровье/урон/смерть, события, DamageEvent
│   └── PoolManager.cs      // пул объектов (снаряды/эффекты), IPoolable, PooledInstance
├── Input/
│   └── InputReader.cs      // единый ввод: новая Input System + фолбэк на старую
├── Player/
│   ├── PlayerController.cs  // весь мувсет: бег, прыжок, стены, рывок, подкат, слэм
│   ├── PlayerAiming.cs      // прицел мышью → точка и направление в мире
│   └── PlayerHealth.cs      // звук урона, вспышка, перезапуск сцены при смерти
├── Weapons/
│   ├── WeaponDefinition.cs  // ScriptableObject: параметры одного оружия
│   ├── WeaponController.cs  // переключение/стрельба, разброс дроби
│   └── Projectile.cs        // снаряд на raycast, фракции, эффект попадания
├── Enemies/
│   ├── SimpleEnemy.cs       // враг-летун: агр, дистанция, стрельба паттерном
│   └── WaveEncounter.cs     // волны врагов + босс по триггеру, фикс камеры, прогресс для HUD
├── BulletHell/
│   ├── BulletPattern.cs     // ScriptableObject: Aimed/Fan/Ring/Spiral
│   └── BulletEmitter.cs     // выпускает залп по паттерну
├── CameraSystem/
│   ├── CameraController.cs   // Follow/Static, упреждение, тряска, блендинг ракурса
│   └── CameraZone.cs         // триггер: фикс камеры и/или смена перспективы
├── Perspective/
│   └── PerspectiveManager.cs // режим Side2D/ThreeD + параметры камеры на каждый режим
├── Audio/
│   ├── SoundLibrary.cs       // ScriptableObject: SoundId → клипы
│   ├── AudioManager.cs       // воспроизведение SFX через пул AudioSource
│   ├── CombatStateTracker.cs // «в бою / не в бою»
│   └── DynamicMusicManager.cs// кроссфейд explore↔combat треков
├── Visuals/
│   ├── PlaceholderFactory.cs // генерация спрайтов-квадратов/кругов
│   ├── PlaceholderVisual.cs  // компонент: форма/цвет/размер плейсхолдера
│   ├── SpriteAnimation.cs    // ScriptableObject: кадры + fps + loop
│   ├── SpriteAnimator.cs     // покадровый проигрыватель
│   ├── Billboard.cs          // разворот спрайта к камере (для 3D)
│   └── OneShotEffect.cs      // одноразовый эффект (вспышка/искра/волна)
├── UI/
│   ├── GameSettings.cs       // настройки (громкости, фуллскрин) в PlayerPrefs
│   ├── UIBuilder.cs          // сборка uGUI из кода + EventSystem под новый ввод
│   ├── MainMenuController.cs // главное меню (Играть/Настройки/Выход)
│   ├── SettingsPanel.cs      // переиспользуемая панель настроек
│   ├── PauseMenu.cs          // пауза по Esc (Time.timeScale = 0)
│   └── GameHUD.cs            // HUD: здоровье, рывки, оружие, волны, босс, перекрестие
└── Editor/
    └── SceneBuilder.cs       // меню «Cybershi → Build Game / Arena / Menu»
```

Поток данных в двух словах: `InputReader` → `PlayerController` двигает Rigidbody; `PlayerAiming` даёт
направление → `WeaponController` спавнит `Projectile` через `PoolManager`; `Projectile` бьёт `IDamageable` →
`Health` уведомляет о смерти; `WaveEncounter`/`SimpleEnemy` сообщают `CombatStateTracker` →
`DynamicMusicManager` меняет музыку; `CameraZone`/`WaveEncounter` командуют `CameraController` и
`PerspectiveManager`; `GameHUD` читает состояние и рисует интерфейс.

---

## 6. Графика: замена плейсхолдеров на спрайты

Каждый видимый объект имеет дочерний объект **`Visual`** со `SpriteRenderer` и компонентом
`PlaceholderVisual`. Пока спрайт не задан — рисуется сгенерированный квадрат/круг нужного цвета.

Три способа поставить свою графику:
1. **Самый простой:** в компоненте `PlaceholderVisual` поле **`Sprite Override`** → перетащите свой `Sprite`.
   Цвет/форма плейсхолдера перестанут использоваться, размер (`size`) останется.
2. Либо задайте `SpriteRenderer.sprite` напрямую и **удалите** `PlaceholderVisual` с объекта.
3. Для пачки объектов — отредактируйте префаб (`Assets/Prefabs/*.prefab`): меняете `Visual` один раз,
   меняются все экземпляры.

Поля `PlaceholderVisual`:
- `Shape` — Square/Circle (для генерируемого плейсхолдера);
- `Color` — цвет/тон (работает и поверх настоящего спрайта через `SpriteRenderer.color`);
- `Size` — размер в юнитах (применяется к `localScale` объекта `Visual`);
- `Sorting Order` — порядок отрисовки.

---

## 7. Анимации и циклы спрайтов

Покадровая анимация сделана без Mecanim, на лёгком компоненте.

1. Создайте ассет: **Create → Cybershi → Sprite Animation**. Заполните `frames` (массив спрайтов),
   `fps`, `loop`.
2. На объекте `Visual` уже есть (или добавьте) компонент **`SpriteAnimator`**.
   Назначьте `Default Animation` — она заиграет на старте.
3. Переключение из кода:
   ```csharp
   spriteAnimator.Play(runAnimation);     // сменить состояние (idle/run/jump…)
   spriteAnimator.SetFlipX(facing < 0);   // отражение по направлению
   ```
   `PlayerController` уже сам зовёт `SetFlipX` по направлению взгляда, если на `visual` висит `SpriteAnimator`.

Если кадров нет — `SpriteAnimator` ничего не трогает, остаётся плейсхолдер. Так можно подключать анимации
по одной, не ломая остальное.

---

## 8. Эффекты

Эффекты (вспышка выстрела, искра попадания, взрыв смерти, ударная волна слэма) — это префабы с компонентом
**`OneShotEffect`**. Они спавнятся через пул и живут `lifetime` секунд.

- **Плейсхолдер**: растущий и затухающий круг (поля `startScale`/`endScale`/`fadeOut`).
- **Настоящие эффекты** — три варианта, можно комбинировать:
  - перетащите `ParticleSystem` в поле `particles` (проиграется при спавне);
  - повесьте рядом `SpriteAnimator` с анимацией взрыва;
  - назначьте `soundClip` — звук сыграется в точке эффекта.

Куда привязаны эффекты:
- попадание снаряда → `Projectile.hitEffectPrefab`;
- вспышка выстрела → `WeaponDefinition.muzzleEffectPrefab`;
- смерть → `Health.deathEffectPrefab`;
- ударная волна слэма → `PlayerController.slamEffectPrefab`.

---

## 9. Звуки (SFX)

Звуки **не синтезируются** — вы привязываете свои `AudioClip`.

1. Сборщик уже создал `Assets/Audio/SoundLibrary.asset` со строками под каждый `SoundId`.
2. Откройте его и в нужные строки перетащите клипы (можно несколько — будет играться случайный).
   Поля строки: `volume` и `pitchRandom` (разброс высоты тона, чтобы выстрелы не звучали одинаково).
3. Библиотека уже привязана к `AudioManager` на объекте `--- Managers ---`.

Список `SoundId`: `PlayerShootPistol/Shotgun/SMG`, `PlayerJump`, `PlayerWallJump`, `PlayerDash`,
`PlayerSlide`, `PlayerGroundSlam`, `PlayerLand`, `PlayerHurt`, `WeaponSwitch`, `EnemyShoot`, `EnemyHurt`,
`EnemyDeath`, `Impact`.

Вызов из кода (если расширяете):
```csharp
AudioManager.Instance.Play(SoundId.PlayerDash);                 // 2D
AudioManager.Instance.Play(SoundId.EnemyShoot, transform.position); // 3D в точке
```
Незаполненные `SoundId` просто молчат — это не ошибка, удобно настраивать постепенно.

---

## 10. Динамическая музыка

`DynamicMusicManager` держит **два зацикленных трека** и играет их синхронно, но перекрёстно меняет
громкость: вне боя слышен `explorationTrack`, в бою — `combatTrack`. Ритм не сбивается (оба крутятся всегда).

Настройка:
1. На объекте `DynamicMusicManager` назначьте `explorationTrack` и `combatTrack` (свои `AudioClip`).
2. Параметры: `masterVolume`, `fadeSpeed` (скорость кроссфейда), `idleLevel`
   (громкость трека, когда он «не у дел»; 0 — полностью затихает).

Как определяется бой: враги (`SimpleEnemy`) при обнаружении игрока вызывают
`CombatStateTracker.EnterCombat(this)`, при потере цели/смерти — `ExitCombat`. Пока хоть один враг
в бою — играет боевой трек. Поля можно оставить пустыми — тогда музыки просто нет.

---

## 11. Оружие

Оружие — это данные (`WeaponDefinition`, ScriptableObject), логику исполняет `WeaponController` на игроке.
Сборщик создал три штуки в `Assets/Weapons/`:

| Оружие | Тип | Особенности |
|---|---|---|
| **Револьвер** (Pistol) | полуавтомат | точный, сильный одиночный выстрел |
| **Дробовик** (Shotgun) | полуавтомат | 8 дробинок веером, отбрасывание |
| **Гвоздомёт** (SMG) | авто | высокий темп, малый урон, лёгкий разброс |

Параметры `WeaponDefinition`: `damage`, `projectileSpeed`, `fireRate` (выстр/сек), `pelletsPerShot`,
`spreadAngle`, `automatic`, `knockback`, `cameraShake`, `projectilePrefab`, `muzzleEffectPrefab`, `fireSound`.

Добавить новое оружие:
1. **Create → Cybershi → Weapon**, заполнить поля, указать `projectilePrefab`
   (можно `Assets/Prefabs/PlayerBullet.prefab`).
2. Перетащить ассет в список `WeaponController.weapons` на игроке.
3. Готово — выбор по цифре по индексу в списке.

Боезапас бесконечный (как у V1). Захотите патроны/перезарядку — добавьте поля в `WeaponDefinition`
и счётчик в `WeaponController.Fire`.

---

## 12. Враги и паттерны буллет-хелла

**`SimpleEnemy`** — парящий враг: висит без гравитации, держит `preferredDistance` от игрока, слегка
покачивается и через `BulletEmitter` стреляет паттерном. Поля: `aggroRange`, `deAggroRange`,
`preferredDistance`, `moveSpeed`, `fireInterval`, `aggroDelay`. Здоровье/смерть — компонент `Health`
(фракция `Enemy`, `deathEffectPrefab`).

**Паттерны** (`BulletPattern`, ScriptableObject, **Create → Cybershi → Bullet Pattern**):
- `Aimed` — одиночный точно в игрока;
- `Fan` — веер из `count` пуль по `spreadAngle`;
- `Ring` — кольцо на 360°;
- `Spiral` — кольцо, проворачивающееся на `spinPerShot` за залп (классика Touhou).

Параметры: `bulletPrefab` (обычно `Assets/Prefabs/EnemyBullet.prefab`), `count`, `spreadAngle`,
`bulletSpeed`, `damage`, `spinPerShot`.

Сделать своего врага:
1. Пустой объект → `Rigidbody`, `CapsuleCollider`, `Health` (faction = Enemy), `SimpleEnemy`.
2. Дочерний объект `Emitter` → `BulletEmitter`, в него — нужный `BulletPattern`.
3. В `SimpleEnemy.emitter` указать этот эмиттер. Добавить `Visual` со спрайтом.
4. (Совет) Сохранить как префаб для переиспользования.

Босс в демо — тот же `SimpleEnemy`, но крупнее, с большим HP и спиральным паттерном.

---

## 13. Камера: следование и статичные планы

`CameraController` (на `Main Camera`) имеет два режима:
- **Follow** — плавно следует за игроком (`followSmoothTime`), с упреждением по скорости
  (`velocityLookAhead`) и по прицелу (`aimLookAhead`);
- **Static** — стоит на месте, давая общий план (арена с боссом, динамичная секция).

Переключают режимы триггеры **`CameraZone`** на уровне (нужен `Collider` с `isTrigger`):
- `onEnter = StaticFraming` + `staticAnchor` (пустой объект, чья позиция/поворот = ракурс камеры) +
  опционально `staticFov`;
- `onEnter = Follow` — вернуть слежение;
- `revertToFollowOnExit` — вернуть слежение при выходе из зоны.

В демо ареной волн управляет `WaveEncounter` (раздел 21): на время боя он сам ставит камеру в Static по
своему `ArenaCamAnchor`, а по завершении возвращает слежение.

Тряску камеры дают выстрелы (`WeaponDefinition.cameraShake`) и граунд-слэм — через `CameraController.Shake(amount)`.

---

## 14. Перспектива и переход в 3D

`PerspectiveManager` хранит текущий режим — `Side2D` или `ThreeD` — и **параметры камеры на каждый режим**
(`side2DView` / `threeDView`: смещение, поворот, FOV). Сам он камеру не двигает: `CameraController`
плавно интерполирует свои параметры к `PerspectiveManager.CurrentView` (отсюда красивый облёт при смене),
а `PlayerController` читает `CurrentMode` и выбирает схему движения.

- **Side2D**: движение по X, ось Z заморожена, прыжок по Y. Доступны стены/подкат/слэм/рывок.
- **ThreeD**: ось Z разблокируется, WASD двигают по плоскости XZ относительно поворота камеры,
  прыжок/слэм по Y, рывок горизонтальный. Прыжок от стен в 3D отключён.

Сменить перспективу:
- из кода: `PerspectiveManager.Instance.SetMode(PerspectiveMode.ThreeD);` или `.Toggle();`
- на уровне: `CameraZone` с `changePerspective = true`, `perspectiveOnEnter`, опционально
  `revertPerspectiveOnExit`/`perspectiveOnExit`. В демо это зона `PerspectiveZone_3D` (~x = 75).

При возврате в 2D после 3D-секции игрок плавно стягивается обратно в плоскость z = 0
(см. `PlayerController.ApplyConstraints`).

---

## 15. Здоровье, урон, фракции

- `Faction` — `Player` / `Enemy` / `Neutral`. Снаряд бьёт всех, кроме своей фракции (нет дружественного огня).
- `IDamageable` реализован компонентом `Health`. Урон передаётся структурой `DamageInfo`
  (количество, точка, нормаль, отбрасывание, источник, фракция источника).
- `Health` шлёт события: `OnDamaged` (UnityEvent с `DamageInfo`), `OnHealed`, `OnDeathEvent`
  (привязывайте в инспекторе) и C#-событие `Died` (для кода).
- У игрока — `Health` (faction = Player, `destroyOnDeath = false`) + `PlayerHealth` (вспышка, звук,
  перезапуск сцены). Во время рывка `Health.Invulnerable` временно включается (i-frames).

---

## 16. Тонкая настройка ощущений

Главные «ручки» в `PlayerController`:
- **Прыжок:** `jumpHeight`, `gravity`, `fallGravityMult` (резкость падения), `jumpCutMultiplier`
  (регулируемая высота), `coyoteTime`, `jumpBuffer`.
- **Рывок:** `dashSpeed`, `dashDuration`, `maxDashCharges`, `dashRechargeTime`, `dashInvulnerable`.
- **Подкат:** `slideSpeed`, `slideFriction`, `slideMinSpeed`, `slideHeightScale`, `slideJumpBoost`.
- **Слэм:** `slamSpeed`, `slamRadius`, `slamDamage`, `slamKnockback`, `slamJumpBoost`.
- **Стены:** `wallJumpForce`, `wallSlideSpeed`, `wallJumpLockTime`, `wallCheckDistance`.
- **Бег:** `moveSpeed`, `groundAccel`, `airAccel`, `groundFriction`.

Подсказка: для «тяжёлого» ощущения увеличьте `gravity` и `fallGravityMult`; для «парящего» — уменьшите.

---

## 17. Известные ограничения и идеи развития

Это каркас, специально оставленный простым и расширяемым:
- **3D-режим — рабочая основа, не финал.** Реализованы движение/прыжок/рывок/слэм по плоскости;
  навигация врагов и буллет-хелл заточены под плоскость XY. Для полноценного 3D-боя враги/паттерны
  нужно доработать.
- **Точка вылета (FirePoint)** не зеркалится при развороте — для плейсхолдеров незаметно; при желании
  отражайте её по `PlayerController.FacingSign`.
- **Менеджеры живут в сцене** (кроме `InputReader` и корня пула — они `DontDestroyOnLoad`).
- **HUD/меню строятся из кода** (плейсхолдер uGUI на легаси-Text). Под продакшн замените на свои префабы
  или TextMeshPro.
- **Враги волн появляются у точек спавна** без проверки занятости — при желании добавьте антиперекрытие.

Куда расти: патроны и перезарядка, типы врагов (наземный, стрелок, таран), фазы босса,
параллакс-фоны, частицы вместо плейсхолдер-эффектов, прогрессия/прокачка оружия, чекпойнты и сохранения.

---

## 18. Решение проблем (FAQ)

- **Не работает управление / `InvalidOperationException` про Input.**
  Установите пакет **Input System** и поставьте Active Input Handling = **Both** (или *New*), перезапустите
  редактор. Если пакет ставить не хотите — оставьте *Input Manager (Old)*: `InputReader` сам переключится
  на классический ввод (но кнопки меню под старым модулем тоже работают).

- **Кнопки меню не нажимаются (новый ввод).** Нужен Input System ≥ 1.4 (есть в Unity 6). `UIBuilder`
  ставит `EventSystem` с `InputSystemUIInputModule` и вызывает `AssignDefaultActions()`.

- **Меню «Cybershi» не появилось.** Дождитесь компиляции; проверьте, что `SceneBuilder.cs` лежит в
  папке `Editor` и нет ошибок компиляции в Console.

- **«Сцена не найдена в Build Settings» при нажатии «Играть».** Запустите **Cybershi → Build Game**
  (он сам прописывает MainMenu и SampleArena в Build Settings) или добавьте их вручную (File → Build Settings).

- **Объекты невидимы / нет спрайтов.** Камера должна быть `MainCamera` (сборщик ставит тег сам).
  `PlaceholderVisual` пересоздаёт спрайт при загрузке — если удалили компонент, задайте `SpriteRenderer.sprite` вручную.

- **Игрок проваливается сквозь пол / не прыгает.** У пола должен быть `Collider`; маска
  `PlayerController.environmentMask` должна включать его слой (по умолчанию Everything).

- **Снаряды летят сквозь врагов.** У врага должен быть `Collider` и `Health` (через него работает `IDamageable`).
  У снаряда `collisionMask` должен включать слой цели (по умолчанию Everything).

- **Музыка не меняется в бою.** Назначьте оба трека в `DynamicMusicManager`; враг должен войти в
  `aggroRange`. Проверьте, что в сцене есть `CombatStateTracker`.

- **Ошибка `Rigidbody.linearVelocity` на Unity 2022.** Это API Unity 6. На 2022 замените
  `linearVelocity` → `velocity` в `PlayerController.cs` и `SimpleEnemy.cs`.

- **Босс/волны не запускаются.** Игрок должен войти в триггер `WaveArenaZone` (Collider с isTrigger).
  Враги в волнах — это **префабы**; проверьте, что они назначены в `WaveEncounter.waves`.

---

## 19. Ввод (Input System)

Весь ввод централизован в `InputReader` (`Assets/Scripts/Input/InputReader.cs`) — singleton, который
создаётся сам и переживает смену сцен (`DontDestroyOnLoad`). Игровые скрипты читают только его свойства:

| Свойство | Значение |
|---|---|
| `Move` | вектор движения (WASD/стрелки/левый стик) |
| `PointerPosition` | позиция курсора (экран) |
| `FireHeld` / `FirePressed` | огонь (ЛКМ / правый триггер) |
| `JumpPressed` / `JumpHeld` | прыжок (Space / A) |
| `DashPressed` | рывок (Shift / X) |
| `SlideSlamPressed` / `SlideSlamHeld` | подкат-слэм (Ctrl / B) |
| `PausePressed` | пауза (Esc / Start) |
| `WeaponCycle` | -1/0/+1 (колесо/бамперы) |
| `WeaponSlotPressed(i)` | клавиши 1..9 |

**Две реализации под капотом**, выбираются автоматически директивой `#if ENABLE_INPUT_SYSTEM`:
- пакет установлен и активен → **новая Input System** (опрос `Keyboard/Mouse/Gamepad`);
- иначе → **классический `UnityEngine.Input`** (фолбэк, чтобы проект всегда компилировался).

**Сменить раскладку** — правьте `InputReader` в одном месте (никакие игровые скрипты трогать не нужно).
Хотите полноценный ребайндинг — замените внутренности `InputReader` на `InputActionAsset` с теми же
свойствами наружу.

---

## 20. Меню, настройки, пауза, HUD

Весь интерфейс строится **из кода** (`UIBuilder`) — не нужно собирать Canvas руками. Легко заменить на
свои префабы/спрайты. `UIBuilder.EnsureEventSystem()` сам создаёт `EventSystem` с правильным модулем ввода.

- **Главное меню** (`MainMenuController`, сцена `MainMenu`): «Играть» (грузит `SampleArena`), «Настройки»,
  «Выход». Имя игровой сцены — поле `gameSceneName`.
- **Настройки** (`SettingsPanel` + `GameSettings`): общая громкость, музыка, эффекты, полный экран.
  Сохраняются в `PlayerPrefs` и применяются к `AudioListener`/`AudioManager`/`DynamicMusicManager`.
  Панель переиспользуется и в меню, и в паузе.
- **Пауза** (`PauseMenu`): по **Esc/Start**, ставит `Time.timeScale = 0`, меню «Продолжить / Настройки /
  В главное меню / Выход».
- **HUD** (`GameHUD`): полоска здоровья, заряды рывка (пипсы), текущее оружие, перекрестие у курсора,
  индикатор боя, счётчик волн и полоска здоровья босса. Данные тянет из `PlayerController`,
  `WeaponController`, `CombatStateTracker`, `WaveEncounter` — никакой ручной привязки.

Чтобы поставить свой стиль: замените методы в `UIBuilder` (шрифт, цвета, спрайты) — обновятся все экраны.

---

## 21. Волны врагов и босс

`WaveEncounter` (`Assets/Scripts/Enemies/WaveEncounter.cs`) — арена с волнами, запускаемая **по триггеру**
(Collider с `isTrigger`, реагирует на вход игрока).

Как это работает:
1. Игрок входит в зону → `Begin()`: включается боевая музыка (регистрация в `CombatStateTracker`),
   камера (если задан `cameraAnchor` и `lockCameraStatic`) фиксируется на общем плане арены.
2. Волны идут **последовательно**: следующая стартует, только когда уничтожены все враги предыдущей.
3. Враги каждой волны спавнятся в точках `spawnPoints` (по кругу; если их нет — у зоны со случайным
   разбросом). Каждый — это **префаб** с компонентом `Health`; энкаунтер подписывается на его смерть.
4. Помеченный `isBoss` враг попадает в `CurrentBoss` → в HUD появляется полоска здоровья босса.
5. После последней волны — `Finish()`: бой выключается, камера возвращается к слежению, срабатывает
   событие `Completed`.

Настройка волн (в инспекторе `WaveEncounter` или кодом):
```csharp
waves = new[] {
  new WaveDefinition { label = "Волна 1", startDelay = 0.6f,
      spawns = new[] { new SpawnEntry { enemyPrefab = drone, count = 3 } } },
  new WaveDefinition { label = "Босс", startDelay = 1f,
      spawns = new[] {
          new SpawnEntry { enemyPrefab = boss,  count = 1, isBoss = true },
          new SpawnEntry { enemyPrefab = drone, count = 2 } } },
};
```
Поля: `encounterName` (для HUD), `spawnPoints`, `timeBetweenWaves`, `lockCameraStatic`, `cameraAnchor`,
`cameraFov`, `triggerOnPlayerEnter`, `oneShot`. Запустить вручную (без триггера) — метод `Begin()`.

Прогресс читается из статического `WaveEncounter.Active` (его и опрашивает `GameHUD`):
`InProgress`, `CurrentWaveIndex`, `TotalWaves`, `AliveCount`, `CurrentBoss`, `BossName`.

В демо сборщик создаёт префабы `Enemy_Drone` и `Boss`, а арена (`WaveArenaZone`, ~x = 40) гоняет
3 волны: дроны → больше дронов → босс + сопровождение.

---

Приятной разработки. Стреляйте красиво. 🔫
