# PirateSlop — карта проекта

Снимок файлов: 2026-10-09. Корень: `C:\Users\K\Project`.

## Как пользоваться

Сначала прочитай `AGENTS.md`, `lessons.md` и эту карту. Выбери механику ниже; если нужного файла нет среди точек входа, открой соответствующий каталог из таблицы. Ищи имя внутри этих Markdown-файлов, затем читай исходник по указанному пути. Не запускай обзор папок или поиск файлов по всему проекту при каждом новом чате.

Карта описывает сохранённые файлы, а не живую сцену и не результаты игровых проверок. Назначения механик взяты из поддерживаемого `Tools/Context/topics.json`; в полном каталоге указаны тип файла, объявленные C#-типы и известные тематические связи. Для остальных файлов семантика не угадывается по имени. Подключение компонента и актуальную реализацию проверяй только для затронутой задачи.

Все пути относительно корня проекта. Полный каталог разбит на приложения, чтобы не загружать тысячи строк в каждый чат.

## Полный каталог

Учтено 4252 файлов без `.meta`. Ещё 4241 файлов `.meta` сопровождают ассеты/папки: их путь — путь ассета или папки плюс `.meta`; сохраняй их GUID. Сама карта и её автоматически созданные приложения не входят в подсчёт.

| Раздел | Назначение | Файлов |
| --- | --- | ---: |
| [.agents](<Docs/ProjectMap/.agents.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.cursor](<Docs/ProjectMap/.cursor.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.opencode](<Docs/ProjectMap/.opencode.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [Art](<Docs/ProjectMap/Art.md>) | Исходники арта и Blender | 427 |
| [Assets](<Docs/ProjectMap/Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 2 |
| [Assets/Animations](<Docs/ProjectMap/Assets-Animations.md>) | Анимации | 27 |
| [Assets/Audio](<Docs/ProjectMap/Assets-Audio.md>) | Звуковые ресурсы и лицензии | 400 |
| [Assets/Branding](<Docs/ProjectMap/Assets-Branding.md>) | Оформление проекта | 1 |
| [Assets/Editor](<Docs/ProjectMap/Assets-Editor.md>) | Редакторские ресурсы | 1 |
| [Assets/Fog Particles](<Docs/ProjectMap/Assets-Fog Particles.md>) | Ресурсы раздела; точный состав — в каталоге | 3 |
| [Assets/Game](<Docs/ProjectMap/Assets-Game.md>) | Игровые подсистемы и эффекты | 66 |
| [Assets/Houidisoft technology](<Docs/ProjectMap/Assets-Houidisoft technology.md>) | Ресурсы раздела; точный состав — в каталоге | 12 |
| [Assets/JMO Assets](<Docs/ProjectMap/Assets-JMO Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 19 |
| [Assets/Materials](<Docs/ProjectMap/Assets-Materials.md>) | Материалы | 133 |
| [Assets/Mirza](<Docs/ProjectMap/Assets-Mirza.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [Assets/Models](<Docs/ProjectMap/Assets-Models.md>) | Модели и связанные ресурсы | 1334 |
| [Assets/Plugins](<Docs/ProjectMap/Assets-Plugins.md>) | Плагины | 5 |
| [Assets/Prefabs](<Docs/ProjectMap/Assets-Prefabs.md>) | Готовые игровые объекты | 120 |
| [Assets/Resources](<Docs/ProjectMap/Assets-Resources.md>) | Ресурсы, доступные для загрузки по имени | 74 |
| [Assets/Scenes](<Docs/ProjectMap/Assets-Scenes.md>) | Сохранённые сцены | 3 |
| [Assets/Scripts](<Docs/ProjectMap/Assets-Scripts.md>) | Игровой код и редакторские инструменты | 444 |
| [Assets/Settings](<Docs/ProjectMap/Assets-Settings.md>) | Настройки игровых систем и рендеринга | 51 |
| [Assets/Shaders](<Docs/ProjectMap/Assets-Shaders.md>) | Шейдеры | 15 |
| [Assets/StreamingAssets](<Docs/ProjectMap/Assets-StreamingAssets.md>) | Ресурсы раздела; точный состав — в каталоге | 5 |
| [Assets/Tests](<Docs/ProjectMap/Assets-Tests.md>) | Исходники проверок | 19 |
| [Assets/ThirdParty](<Docs/ProjectMap/Assets-ThirdParty.md>) | Сторонние ресурсы | 20 |
| [Assets/TutorialInfo](<Docs/ProjectMap/Assets-TutorialInfo.md>) | Ресурсы раздела; точный состав — в каталоге | 2 |
| [Assets/UI](<Docs/ProjectMap/Assets-UI.md>) | Ресурсы интерфейса | 39 |
| [Docs](<Docs/ProjectMap/Docs.md>) | Документы и сохранённые отчёты | 37 |
| [Packages](<Docs/ProjectMap/Packages.md>) | Манифест, lock-файл и встроенные пакеты | 924 |
| [ProjectSettings](<Docs/ProjectMap/ProjectSettings.md>) | Настройки Unity | 28 |
| [Root](<Docs/ProjectMap/Root.md>) | Корневые инструкции, планы и служебные файлы | 26 |
| [ThirdParty](<Docs/ProjectMap/ThirdParty.md>) | Сторонние ресурсы | 2 |
| [Tools](<Docs/ProjectMap/Tools.md>) | Инструменты разработки и загрузчик контекста | 9 |

Не индексируются генерируемые сборки, кэши и локальное состояние: `.git/`, `.idea/`, `.vs/`, `Builds/`, `Library/`, `Logs/`, `Temp/`, `UserSettings/`, `__pycache__/`, `bin/`, `node_modules/`, `obj/`. Зависимости из Unity PackageCache представлены манифестом/lock-файлом; встроенные пакеты из `Packages/` перечислены полностью. Скрытые конфиги вне исключённых папок включены только как пути, их содержимое не копируется.

## Механики и точки входа

### Улучшения рогалика из сундуков (`roguelike-upgrades`)

Ключевые слова: рогалик, улучшения.

Основа рогалика: первое открытие наполненного сундука начисляет очко каждому участнику команды; V открывает три личные карты с названием, описанием и редкостью. Очередь наград, выбор и исключение повторов принадлежат серверу; шансы фиксируются при начислении. Переподключение восстанавливает состояние по серверному токену в текущем матче. Все 41 эффекта подключены к выбору: серверные боевые эффекты, предсказываемые модификаторы движения, два бонусных слота, корабельные выдачи, рыбалка, подзорная труба и рероллы. UpgradeEffects.json хранит параметры и передаётся сервером клиентам. Монета и договор расходуются один раз за матч; ром и обезьянка не повторяются при reconnect. Common 14, Rare 12, Epic 9, Legendary 6. Шансы и каталог — JSON в StreamingAssets/Roguelike, загрузка при запуске матча. В Тестовой карте каждый новый корабль получает один наполненный TestUpgradeChest рядом с PlayerLocalSpawn через серверный спаун и поиск поверхности по активным коллайдерам; сундук закреплён на корабле и сразу доступен. Обычные матчи не меняются. Голос перенесён с V на B; ProtocolVersion 127. Компиляция и логика каталога проверены через Unity MCP; Play Mode, сборка и второй клиент не запускались.
UI рогалика построен на uGUI Canvas с CanvasScaler Expand: три физические игральные карты с бумажной фактурой, гравюрами, мастями и зеркальными мастями. При наведении карта поднимается и выпрямляется; клик по карте/кнопке или Enter выбирает её. Очки привлекают внимание плавным золотым сигналом с периодом 1,6 с до траты; пульсация отключается и настройка сохраняется. Коллекция — миниатюры карт с фильтрами, прокруткой и полным описанием при наведении. Resources/RoguelikeUI хранит два новых изображения, созданных imagegen; ссылки на референсы и гайдлайны в Docs/RoguelikeUIStyle.md. EnvironmentTestGallery скрывает подписи при открытом окне. Визуальная проверка — отдельная preview-сцена через Unity MCP, без Play Mode.
Повторная проверка: основная анимация сабли использует SabreSpeed для Slash/Slash2; эхо сохраняет seed и разброс первоначального выстрела; PendingPact восстанавливает смерть до сетевого спауна; автоматическая отметка не сокращает более долгую ручную.
Свечение выбора Rare/Epic/Legendary использует RarityGlow.shader: тонкая светлая кромка, узкий ореол с шумовым рассеиванием, редкие искры Epic/Legendary; alpha CardFace защищает бумагу. Resources включает shader в сборку, отдельные материалы уничтожаются вместе с UI. Common и коллекция без эффекта. Проверены только Unity preview 1080p/720p и hover, не игровой матч. Угловые цифры удалены, зеркальные масти сохранены. Glow не перехватывает мышь, не требует камерного Bloom и усиливается при выделении.

- [Assets/Scripts/Loot/RoguelikeCatalog.cs](<Assets/Scripts/Loot/RoguelikeCatalog.cs>) — Исходник C#: UpgradeRarity, UpgradeCard, RoguelikeCatalog, CardFile, ChanceFile, Milestone.
- [Assets/Scripts/Networking/NetworkPlayer.Roguelike.cs](<Assets/Scripts/Networking/NetworkPlayer.Roguelike.cs>) — Исходник C#: UpgradeReward, PlayerUpgradeState, UpgradeSnapshot, NetworkPlayer.
- [Assets/Scripts/Networking/SessionRoguelike.cs](<Assets/Scripts/Networking/SessionRoguelike.cs>) — Исходник C#: UpgradeIdentityMessage, SessionController.
- [Assets/Scripts/UI/RoguelikeUpgradeUI.cs](<Assets/Scripts/UI/RoguelikeUpgradeUI.cs>) — Исходник C#: RoguelikeUpgradeUI.
- [Assets/Scripts/UI/RoguelikeUpgradeUI.Presentation.cs](<Assets/Scripts/UI/RoguelikeUpgradeUI.Presentation.cs>) — Исходник C#: RoguelikeUpgradeUI.
- [Assets/Scripts/UI/RoguelikeUpgradeView.cs](<Assets/Scripts/UI/RoguelikeUpgradeView.cs>) — Исходник C#: RoguelikeUpgradeView, CardSlot, UpgradeCardPointer.
- [Assets/Resources/RoguelikeUI/RarityGlow.shader](<Assets/Resources/RoguelikeUI/RarityGlow.shader>) — Шейдер.
- [Assets/Resources/RoguelikeUI/CardFace.png](<Assets/Resources/RoguelikeUI/CardFace.png>) — Изображение / текстура.
- [Assets/Resources/RoguelikeUI/CardArt.png](<Assets/Resources/RoguelikeUI/CardArt.png>) — Изображение / текстура.
- [Assets/Scripts/UI/UpgradeCardPresentation.cs](<Assets/Scripts/UI/UpgradeCardPresentation.cs>) — Исходник C#: UpgradeCardPresentation.
- [Assets/StreamingAssets/Roguelike/UpgradeChances.json](<Assets/StreamingAssets/Roguelike/UpgradeChances.json>) — Конфигурация / данные JSON.
- [Assets/StreamingAssets/Roguelike/UpgradeCatalog.json](<Assets/StreamingAssets/Roguelike/UpgradeCatalog.json>) — Конфигурация / данные JSON.
- [Assets/Scripts/Loot/RoguelikeTuning.cs](<Assets/Scripts/Loot/RoguelikeTuning.cs>) — Исходник C#: UpgradeEffect, RoguelikeTuning.
- [Assets/Scripts/Networking/NetworkPlayer.UpgradeEffects.cs](<Assets/Scripts/Networking/NetworkPlayer.UpgradeEffects.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/AdvancedPlayerController.Upgrades.cs](<Assets/Scripts/AdvancedPlayerController.Upgrades.cs>) — Исходник C#: AdvancedPlayerController.
- [Assets/Scripts/Player/PlayerInventory.Upgrades.cs](<Assets/Scripts/Player/PlayerInventory.Upgrades.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Networking/NetworkShip.UpgradeMonkeys.cs](<Assets/Scripts/Networking/NetworkShip.UpgradeMonkeys.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Player/UpgradeCombat.cs](<Assets/Scripts/Player/UpgradeCombat.cs>) — Исходник C#: UpgradeCombat.
- [Assets/Scripts/Networking/NetworkWeapon.UpgradeCombat.cs](<Assets/Scripts/Networking/NetworkWeapon.UpgradeCombat.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Player/GhostSabreWave.cs](<Assets/Scripts/Player/GhostSabreWave.cs>) — Исходник C#: GhostSabreWave.
- [Assets/Scripts/Player/GunnersEyeView.cs](<Assets/Scripts/Player/GunnersEyeView.cs>) — Исходник C#: GunnersEyeView.
- [Assets/Scripts/Networking/NetworkPlayer.UpgradeSpyglass.cs](<Assets/Scripts/Networking/NetworkPlayer.UpgradeSpyglass.cs>) — Исходник C#: NetworkPlayer.
- [Assets/StreamingAssets/Roguelike/UpgradeEffects.json](<Assets/StreamingAssets/Roguelike/UpgradeEffects.json>) — Конфигурация / данные JSON.
- [Assets/Scripts/Player/SabreAnimation.cs](<Assets/Scripts/Player/SabreAnimation.cs>) — Исходник C#: SabreAnimation.
- [Assets/Animations/Player/PiratePlayer.controller](<Assets/Animations/Player/PiratePlayer.controller>) — Контроллер анимации.
- [Docs/RoguelikeApprovedUpgrades.md](<Docs/RoguelikeApprovedUpgrades.md>) — Документация.
- [Docs/RoguelikeMechanic.md](<Docs/RoguelikeMechanic.md>) — Документация.
- [Docs/RoguelikeUIStyle.md](<Docs/RoguelikeUIStyle.md>) — Документация.
- [Docs/RoguelikeEffects.md](<Docs/RoguelikeEffects.md>) — Документация.

### Инспектор объектов Unity (`inspector`)

Ключевые слова: инспектор, диагностика.

Окно: PirateSlop > Diagnostics > Focused Inspector. Только чтение выбранного объекта или префаба.
Для Codex вызвать FocusedInspector.CaptureSelection или CaptureAsset через Unity MCP. Сначала прочитать Tools/Context/inspector.md. Обрезанный отчёт не является полной проверкой.

- [Assets/Scripts/Editor/FocusedInspector.cs](<Assets/Scripts/Editor/FocusedInspector.cs>) — Исходник C#: FocusedInspector, Report.
- [Assets/Scripts/Editor/FocusedInspectorWindow.cs](<Assets/Scripts/Editor/FocusedInspectorWindow.cs>) — Исходник C#: FocusedInspectorWindow.
- [Tools/Context/inspector.md](<Tools/Context/inspector.md>) — Документация.

### Проект и точки входа (`overview`)

Ключевые слова: обзор, проект.

Windows, URP, FishNet; подключения по IP и Steam имеют отдельные ветки. Пакет file: в manifest — путь зависимости, а не её версия.
Запуск через NetworkMenu, игровой мир NetworkOcean; корабль и игрок — сетевые префабы. Старые записи о SampleScene исторические.
Очистка 2026-10-04: удалены старые визуальные отчёты Docs/StormCloudBakeoff, Docs/StormFinal, Docs/StormV2, Docs/StormV3; сцены Assets/_Recovery, изображения Assets/Screenshots и пять резервных .blend1 с сохранением основных .blend. Игровые модели, код, префабы, Resources и сетевой реестр не изменялись.
Очистка 2026-10-05 по Git-аудиту: удалены 73 основных кандидата высокой уверенности и 57 сопутствующих .meta, всего 130 файлов (180.11 МиБ). Удалены Docs/Reports.zip, дублирующий Docs/LootModels/loot.zip, Python-кэш, одноразовые отчёты и исправления, старая SampleScene, старый Ocean.shader и отдельные неподключённые демо/ассеты сторонних пакетов. По просьбе пользователя все звуковые клипы сохранены, включая 15 high-кандидатов и их .meta. Кандидаты средней и низкой уверенности не удалялись; сетевые коллекции и игровой C# не изменялись. Игровая проверка не выполнялась.
Ship V3 теперь единственный игровой корабль: SessionController.ShipPrefab в NetworkMenu указывает на Assets/Resources/Ships/ShipV3Test.prefab. Старый NetworkShip.prefab сохранён как архивный ассет и не создаётся в игре. Историческое имя ShipV3Test сохранено вместе с GUID и регистрацией FishNet.
Очистка 2026-10-05 по Git-аудиту, второй этап: удалены 1300 основных кандидатов средней уверенности и 3 сопутствующих инспектора удаляемых демо, с .meta — 2697 файлов (758.73 МиБ). Удалены неподключённые части прежних кораблей, старые локальные игровые скрипты, архивы версий обезьянки, старые анимации/иконки, демо FishNet/Cartoon FX/Mirza и отдельные отладочные материалы шторма. Все звуковые клипы и метаданные их папок сохранены, как и .meta папок с другими сохранёнными файлами. Низкие условные игровые кандидаты не удалялись. Сетевые коллекции не редактировались; игровой прогон не выполнялся.

- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionSpectator.cs](<Assets/Scripts/Networking/SessionSpectator.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/BotSpectatorCamera.cs](<Assets/Scripts/Networking/BotSpectatorCamera.cs>) — Исходник C#: BotSpectatorCamera.
- [Assets/Scripts/Networking/SessionConfig.cs](<Assets/Scripts/Networking/SessionConfig.cs>) — Исходник C#: SessionConfig.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Scenes/NetworkMenu.unity](<Assets/Scenes/NetworkMenu.unity>) — Сцена Unity.
- [Assets/Scenes/NetworkOcean.unity](<Assets/Scenes/NetworkOcean.unity>) — Сцена Unity.
- [Assets/Prefabs/Networking/NetworkShip.prefab](<Assets/Prefabs/Networking/NetworkShip.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/NetworkPlayer.prefab](<Assets/Prefabs/Networking/NetworkPlayer.prefab>) — Префаб Unity.
- [project.md](<project.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Движение корабля и палуба (`ship`)

Ключевые слова: корабль, палуба, helm, штурвал.

Основной корабль игроков, ботов и разработческого спавна — Ship V3. Начальный спавн экипажа и возрождение используют SessionConfig.PlayerLocalSpawn, взятый из ShipV3Features.RespawnPoint. F2 прежде всего переносит на Ship V3 своей команды. Старые модель и префаб сохранены в проекте, но не появляются в игре.
Корабль перемещается кинематически. Не переносить пассажира или предмет дважды вместе с кораблём; Rigidbody.GetPointVelocity не обязательно описывает это движение.
Управление и освобождение штурвала согласовывать с сервером; старые альтернативные контроллеры не выбирать только по имени.
ShipV3BindingRepair восстанавливает evaluated-позы механизмов в базисе родителя. У штурвала отдельный HelmWheelRotor: вращаются только колесо и его фрагменты вокруг авторского центра и оси плоскости меша. fixedWheelCenter сохраняет локальную позицию центра; несущая секция и точка захвата не вращаются. DoorAssembly включает шарнир, отдельную раму и перемычку; захват и серверная проверка используют всю дверную сборку, расстояние удержания считается от DoorGrip. Верёвка рынды имеет капсулу захвата; язычок реагирует на мышь с меньшим сопротивлением joint. Верх кабестана проходит равномерно весь ход 0.45 м по прогрессу якоря. Цепь следует Deck_Chain_Outlet/Starboard_Hawse/Swing_Pivot с шагом 0.085 м; физический якорь закреплён к корню, connectedAnchor следует AnchorTravel.
По запросу пользователя дверь Ship V3 исключена из игрового корабля: V16_Hold_Door_Mount неактивен вместе с полотном и коллайдерами, все Door interaction targets удалены, DoorHinge/DoorGrip очищены, DoorAssembly пуст. Рама проёма сохранена. RemoveDoor применяется к сохранённому префабу, полному импорту и ремонту привязок. Исходник Blender не изменён. Игровой запуск и проверка двумя игроками не выполнялись.
ShipV3GameplayRepair сохраняет увеличенную до 1.8 м маску выдачи ядер и рычаг из модели механизма мачты со стороны бывшей двери. ShipV3Features.PullDispenser выдаёт одно обычное ядро при полном опускании; сервер возвращает рычаг за 5 секунд, блокируя захват лишь до полного подъёма. Автоматическая выдача по таймеру удалена. Фонарь Hold Starboard отведён от маски; подвесы трюма имеют правильные connectedAnchor и качаются от движения корабля. Повторный импорт и RepairMechanismBindings сохраняют эти настройки.
У рычага выдачи ядер продольное направление ручки совпадает с DispenserDirection, ось встроена в раму маски со стороны прежнего дверного проёма. ConfigureLanterns переносит также V3_Lamp_Hold_Starboard_Bracket, который в исходном импорте был отдельным объектом вне Mount; кронштейны трюма присоединены к Mount. Joint.anchor фонаря находится у верхней петли модели, connectedAnchor рассчитан из той же точки. ConfigureGeometryBudget исключает Helm.Wheel из объединённой неподвижной геометрии.
Оптимизация 2026-10-03: неподвижные точные MeshCollider Ship V3 объединены в 49 пространственных групп ShipV3CollisionBatch; активных коллайдеров сохранённого корабля 187 вместо 2490. Исходные отключённые коллайдеры сохранены для связей разрушения. ResolveSection и Distance находят исходную секцию; повреждение перестраивает группу, оставшиеся фрагменты используют прежние ленивые коллайдеры. Секции и render batches обновляют геометрию по VisualChanged; полусекундная проверка остаётся для изменений без события. RepairReveal уведомляет объединённый рендеринг.
ShipV3RenderBudget оставляет тени только у ближайшего включённого фонаря в радиусе 8 м, общий лимит один на все корабли. PC_RPAsset: дистанция теней 80 м, два каскада, дополнительные тени 1024, medium soft shadows. Для восьми плотных непрозрачных render batches отдельная ShadowsOnly геометрия с шагом 2 см: 978208 -> 540961 треугольник; видимая модель сохраняется. При повреждении proxy получает актуальную геометрию группы, после полного ремонта возвращается облегчённая кэшированная тень.
Фонари Ship V3: AmberGlass Transparent alpha .22, ShadowCaster выключен только у стекла; металлический каркас сохраняет тени. Четыре оконных треугольника 9/13/201/202 ошибочно находились в metal: ShipLanternClearPanes сохраняет 572 вершины, распределение 385 metal/63 glass. Все 6 корпусов используют исправленную копию; FBX сохранён. Emission (1,.42,.10) x .6, point light (1,.57,.24), intensity1.05 +/- .06, range7.2м. RepairLanternLighting и повторный импорт сохраняют эти настройки. Общий бюджет одной ближайшей тени включает ручные фонари; игровой прогон не выполнялся.
Замеры на RTX 3050 8 ГБ / i5-9400F, тестовая карта, один корабль, Unity Editor 1471x714: до оптимизации около 31 FPS, CPU main 32.2 мс, Physics.SyncTransforms 14.0 мс; после объединения коллайдеров среднее 98.3 FPS за 571 кадр, медиана 119.6, P95 12.36 мс; Physics.SyncTransforms 0.48 мс и Simulate 0.22 мс. Отдельные shadow proxies и последние правки ручки/флагов добавлены после замера. Финальный игровой прогон прекращён по просьбе пользователя; стабильные 90–100 FPS, 1080p, билд и два клиента не подтверждены.
ConfigureDispenser разворачивает поперечный захват горизонтально, сохраняет направление стержня вдоль выхода ядра и удлиняет вынос стержня на 25% через DispenserLeverLength. Область взаимодействия следует за настоящим концом рукоятки. ConfigureFlags поднимает низ полотна на 2.3 м над площадкой гнезда, удлиняет неподвижный флагшток, удаляет с него ShipV3ClothMotion. Полотно имеет закреплённый край и только горизонтальное колыхание, промежуточная фаза ветра сглаживается между сетевыми тиками.
Исправления 2026-10-04: фонарь справа от маски использует геометрию и материалы второго фонаря трюма, зазор 0.18 м. Рычаг вынесен перед маской и движется в вертикальной плоскости вдоль выхода ядра; выдача ядра происходит наружу. Старый набор в стене скрыт, CannonPickup перепривязан к собственной crate. Отклик мыши у рынды усилен. Кости: F — занять свободный стакан, E — собрать, ЛКМ — двигать/трясти, E или отпускание ЛКМ — перевернуть стакан и высыпать. Три стакана работают независимо; сервер считает грани и сумму, показывает результаты всем и завершает бросок после выхода игрока. ProtocolVersion=112. Игровая приёмка остаётся пользователю.
Уточнения ручной проверки Ship V3: горизонтальная поперечная ручка рычага, ядро появляется внутри пасти над лотком. Посадка парных гарпунов учитывает нижнюю грань модели. Кости: только ближайшая кружка, камера с её стороны, CC0 Medieval Beer Mug и Wooden Candlestick, три сектора с физическими бортиками и серверным ограничением. E собирает кости; перевёрнутая кружка перемешивается только от мыши с ЛКМ, отпускание приоткрывает кружку и выпускает кости. Свеча включается/гасится на E вне игры. ShipV3DiceRepair настраивает импорт и стол; источники в DiceProps/SOURCES.md. Конец подъёма по последней ступени с выходом на платформу; V9_Telescope_Tube подключён к ShipSpyglass. CannonDismantle подключён к NetworkPlayer и CannonInventorySetup. ProtocolVersion 113.
Коррекции 2026-10-04 после ручной проверки: рычаг входит в поверхность стены по bounds несущей доски; ядро создаётся глубже во рту. Видимость станции костей учитывает исходные поверхности объединённых коллайдеров; свеча имеет приоритет наведения и свободный центральный зазор 0.105 м. ShipV3DiceContact озвучивает физические столкновения и скольжение; тряска кружки озвучивается только при движении мыши. Горизонтальные центры колеса и неподвижного блока определяются по круговой геометрии; Rotor совпадает с геометрическим центром, визуальный угол сглаживается в LateUpdate. Рында качается только вправо-влево вокруг продольной оси; встроенный неподвижный шток укорочен. Три дополнительных звона чередуются без повтора подряд. Зажигание и тушение общие для свечи и фонарей. ProtocolVersion 114; игровой прогон не выполнялся.
Исправление F/E у костей: DiceSupport находится отдельно от DiceTable.parent; CanSeeDice исключает обе ветки и их исходные поверхности в ShipV3CollisionBatch на клиенте и сервере. MeshCollider бочки соответствует DiceBarrelWithoutHandle.asset; пересохранён кеш ShipV3Collision_38. Центр штурвала совмещён с передней втулкой колонки, а не соседней круговой геометрией. Плавное вращение сохранено; изменения сохранены в ShipV3Test.prefab.
Ввод костей: ShipV3PlayerInteraction.ConsumedInput исключает обработанные E/F из AdvancedPlayerController.pending.Use и PlayerInventory. CanReachDice проверяет ближайшую RestCup + 0.2 м, а переполнение RaycastNonAlloc повторяет полный запрос вместо ложного отказа. При наведении на свечу показаны E и F, фокус свечи имеет допуски 0.12/0.18 м. Подсказки занятого места рисуются отдельно в OnGUI: блокировка движения ShipActivityLocked больше не скрывает их. Недоступные Candle/Dice не выдаются обходным прямым лучом или SphereCast. Компиляция проверена; игровой прогон не выполнялся.
Кости: правая ось кружки совпадает с камерой ближайшего места. DiceInput передаёт абсолютное смещение и скорость в плоскости стола; локальная кружка предсказывается каждый кадр, физическая позиция сглаживается в FixedUpdate, CupVisual сглаживается в LateUpdate отдельно от Rigidbody. Кубики наследуют скорость кружки при отпускании; наклон, длительность приоткрывания и вращение зависят от жеста. При покое нет постоянного горизонтального толчка. UI показывает только номер зоны и сумму; прежняя сумма сохраняется до завершения повторного броска этого места. ConfigurePresentation сохраняет отдельные визуалы кружек и золотые номера 1–3 с тёмным контуром в центрах секторов. ProtocolVersion 115 для нового DiceInput; игровой прогон не выполнялся.
Коррекции рассинхрона костей: отдельные DiceVisuals следуют за общей позой CupVisual в фазах загрузки, тряски и приоткрывания; кубики в покое рисуются в точных локальных координатах корабля без интерполяции Rigidbody. Клиенты получают фазу вместе с локальными физическими позами через существующее поле ShipV3PhysicsPose.Phase. Последнее движение мыши сохраняет скорость 0.1 с до отпускания, включая ограничение позиции у края; смена направления сразу меняет импульс. Выходные позиции разнесены на 0.056 м, KeepDiceOnTable переставляет тело только при фактическом выходе за границы. FirstPersonModelVisibility скрывает своего пирата для отдельной DiceTableCamera и восстанавливает видимость при выходе. SeatTable привязывает сборку к фактическим треугольникам крышки с перекрытием 0.003 м, ставит кружки и кубики на поверхность и пересохраняет затронутый ShipV3Batch_106. Сохранены 3 CupVisual и 15 DiceVisuals в ShipV3Test.prefab. Формат RPC и ProtocolVersion=115 сохранены; игровой прогон не выполнялся.
Качка ShipController использует ShipBuoyancy:9 продольных сечений×3 поперечных точки, weighted plane-fit по площади измеренного корпуса. Высота берётся из intercept плоскости в центре корабля с учётом смещённого weighted meanZ; pitch/roll из её уклонов. Четыре края±18/±5.5 раньше давали spatial alias на волне32м и могли менять знак наклона. Сохранены сглаживаниеexp, clamps12/15deg, Flooding/Cannon и сетевой ShipState. Проверены16 аналитических плоскостей при yaw0/90/180/270: height error<7.2e−7, normal dot≥.99999994. Дляsin(2πz/32) oldPitch+1.218deg, new−3.528deg.
Исправления 2026-10-06: Dispense вызывает Cannonball.Eject без привязки к палубе. Толчок наружу 3 м/с плюс скорость корабля; коллизия с маской и прикрепление к лотку подавлены 0.4 с, затем обычная физика. ShipV3CollisionBatch отключает UseFastMidphase как обход native-сбоя CharacterController.Move из Player.log; устранение игрового краша не подтверждено.

- [Assets/Scripts/ShipController.cs](<Assets/Scripts/ShipController.cs>) — Исходник C#: ShipController.
- [Assets/Scripts/ShipDeckPassenger.cs](<Assets/Scripts/ShipDeckPassenger.cs>) — Исходник C#: ShipDeckPassenger.
- [Assets/Scripts/HelmInteraction.cs](<Assets/Scripts/HelmInteraction.cs>) — Исходник C#: HelmInteraction.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Interaction/DirectShipControls.cs](<Assets/Scripts/Interaction/DirectShipControls.cs>) — Исходник C#: DirectShipControls.
- [Assets/Scripts/Stations/CapstanStation.cs](<Assets/Scripts/Stations/CapstanStation.cs>) — Исходник C#: CapstanStation.
- [Assets/Scripts/Networking/NetworkShip.Anchor.cs](<Assets/Scripts/Networking/NetworkShip.Anchor.cs>) — Исходник C#: NetworkShip.
- [Assets/Prefabs/Networking/NetworkShip.prefab](<Assets/Prefabs/Networking/NetworkShip.prefab>) — Префаб Unity.
- [Assets/Scripts/Ships/ShipV3VisualRig.cs](<Assets/Scripts/Ships/ShipV3VisualRig.cs>) — Исходник C#: ShipV3Pose, ShipV3Motion, ShipV3VisualRig.
- [Assets/Scripts/Ships/ShipV3Features.cs](<Assets/Scripts/Ships/ShipV3Features.cs>) — Исходник C#: ShipV3TargetKind, ShipV3Lantern, ShipV3DiceSlot, ShipV3PhysicsPose, ShipV3Support, ShipV3Attachment, ShipV3Features.
- [Assets/Scripts/Ships/ShipV3RenderBatch.cs](<Assets/Scripts/Ships/ShipV3RenderBatch.cs>) — Исходник C#: ShipV3RenderBatch.
- [Assets/Scripts/Ships/ShipV3CollisionBatch.cs](<Assets/Scripts/Ships/ShipV3CollisionBatch.cs>) — Исходник C#: ShipV3CollisionBatch.
- [Assets/Scripts/Ships/ShipV3RenderBudget.cs](<Assets/Scripts/Ships/ShipV3RenderBudget.cs>) — Исходник C#: ShipV3RenderBudget.
- [Assets/Scripts/Editor/ShipV3PerformanceSetup.cs](<Assets/Scripts/Editor/ShipV3PerformanceSetup.cs>) — Исходник C#: ShipV3PerformanceSetup.
- [Assets/Models/Ships/ShipV3/RuntimeMeshes/Collision/ShipV3Collision_0.asset](<Assets/Models/Ships/ShipV3/RuntimeMeshes/Collision/ShipV3Collision_0.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/RuntimeMeshes/Shadows/ShipV3Batch_12.asset](<Assets/Models/Ships/ShipV3/RuntimeMeshes/Shadows/ShipV3Batch_12.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Ships/ShipV3ChainInstances.cs](<Assets/Scripts/Ships/ShipV3ChainInstances.cs>) — Исходник C#: ShipV3ChainInstances.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Scripts/Editor/ShipV3BindingRepair.cs](<Assets/Scripts/Editor/ShipV3BindingRepair.cs>) — Исходник C#: ShipV3BindingRepair.
- [Assets/Scripts/Editor/ShipV3GameplayRepair.cs](<Assets/Scripts/Editor/ShipV3GameplayRepair.cs>) — Исходник C#: ShipV3GameplayRepair.
- [Assets/Scripts/Ships/ShipV3PlayerInteraction.cs](<Assets/Scripts/Ships/ShipV3PlayerInteraction.cs>) — Исходник C#: ShipV3PlayerInteraction.
- [Assets/Scripts/Editor/ShipV3DiceRepair.cs](<Assets/Scripts/Editor/ShipV3DiceRepair.cs>) — Исходник C#: ShipV3DiceRepair.
- [Assets/Models/Ships/ShipV3/DiceProps/SOURCES.md](<Assets/Models/Ships/ShipV3/DiceProps/SOURCES.md>) — Документация.
- [Assets/Scripts/Networking/NetworkCannonDismantle.cs](<Assets/Scripts/Networking/NetworkCannonDismantle.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Cannons/CannonDismantle.cs](<Assets/Scripts/Cannons/CannonDismantle.cs>) — Исходник C#: CannonDismantle.
- [Assets/Scripts/Ships/ShipV3DiceContact.cs](<Assets/Scripts/Ships/ShipV3DiceContact.cs>) — Исходник C#: ShipV3DiceContact.
- [Assets/Scripts/Player/FirstPersonModelVisibility.cs](<Assets/Scripts/Player/FirstPersonModelVisibility.cs>) — Исходник C#: FirstPersonModelVisibility.
- [Assets/Models/Ships/ShipV3/RuntimeMeshes/Batches/ShipV3Batch_106.asset](<Assets/Models/Ships/ShipV3/RuntimeMeshes/Batches/ShipV3Batch_106.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/ShipV3InteractionAudioSetup.cs](<Assets/Scripts/Editor/ShipV3InteractionAudioSetup.cs>) — Исходник C#: ShipV3InteractionAudioSetup.
- [Assets/Models/Ships/ShipV3/RuntimeMeshes/BellShortInnerStem.asset](<Assets/Models/Ships/ShipV3/RuntimeMeshes/BellShortInnerStem.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber1.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber1.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline1.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline1.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber2.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber2.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline2.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline2.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber3.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumber3.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline3.asset](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline3.asset>) — Настройки или данные Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberGold.mat](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberGold.mat>) — Материал Unity.
- [Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline.mat](<Assets/Models/Ships/ShipV3/DiceProps/ZoneNumberOutline.mat>) — Материал Unity.
- [Assets/Scripts/ShipBuoyancy.cs](<Assets/Scripts/ShipBuoyancy.cs>) — Исходник C#: ShipBuoyancy.
- [Assets/Models/Ships/ShipV3/RuntimeMeshes/Lanterns/ShipLanternClearPanes.asset](<Assets/Models/Ships/ShipV3/RuntimeMeshes/Lanterns/ShipLanternClearPanes.asset>) — Настройки или данные Unity.
- [unity.md](<unity.md>) — Документация.
- [multiplayer-plan.md](<multiplayer-plan.md>) — Документация.
- [Assets/Audio/ShipInteractions/SOURCES.md](<Assets/Audio/ShipInteractions/SOURCES.md>) — Документация.

### Корабельная слот-машина (`ship-slot-machine`)

Ключевые слова: слот-машина, слот машина, рыбная ставка, барабаны, slot machine.

Слот-машина из моделей пользователя у противоположной стены, справа при взгляде из прохода в трюм ShipV3Test: (-3.6054,4.12,-18.0709), yaw58. E в верхний слот принимает одну обычную рыбу, видимая модель втягивается за 1с. Рычаг справа при взгляде на автомат, хват дочерний LeverPivot; удержание ЛКМ и мышь вниз, как выдача ядер, запускают только после полного хода. Дополнительная рука не показывается; Q отпускает, Q без захвата возвращает ставку. ServerRpc проверяет плательщика, номер состояния, дистанцию и скорость тяги; heartbeat .65с отпускает застрявший захват. Автовозврат30с. Барабаны Z=.12 вместо .235, печатные символы сохраняют пропорции PNG и получают свет/тени URP. Шансы: проигрыш60%, рыба18.33%, предмет8.33%, особое ядро7.5%, огнестрел4.17%, пушка1.25%, очко.42%. Огнестрел только Musket/DoubleBarrel, пистолет исключён. Награды физически на палубу, очко только личному UpgradeState через V. Звуки: восемь смонтированных WAV в Assets/Audio/SlotMachine; приём1с, рычаг: один щелчок.046с на каждые6.5градусов ручного движения, темп следует скорости, без звука в покое, вращение4.9с pitch1/безloop, стоп.195с, возврат.62с, рыба.72с, выдача1.6с, любой выигрыш2.72с один Win/Jackpot CC BY 4.0 Free Sounds Library. Личное очко использует штатный UpgradeAward из сундука через UI, без дубля TargetRpc. HTML содержит обработанные клипы и исходные36вариантов; SlotLose/SlotReject без выбора. Protocol130. Компиляция/импорт и сохранённые ссылки через Unity MCP; PlayMode/build/два клиента не запускались.
Шесть Reel*.png нарисованы imagegen по CardArt карт прокачки: единая тушевая гравюра, латунь/бирюза/орех. Alpha PNG, aspect по видимому контуру. FBX белая полоса X=-.070..+.044, центр-.014: глиф до.095м, разделитель.098м, толщина3мм на каждом60-градусном стыке. Mesh.Clear/vertex assignments обновляют GPU buffers вместо CopySerialized. LeverPivot X=-.635 прилегает к боковине, рука не добавлена. Опора4точекY4.11; минимальный зазор всей геометрии до стены3.19см. Статичные editor-render сделаны, игровые проверки остаются пользователю. Независимый visual_reviewer: APPROVED для 6 символов, посадки, разделителей и крепления; это не проверка игрового поведения.
Автомат увеличен на20% (scale1.2, высота2.28м), позиция(-3.6054,4.12,-18.0709),yaw58. После своей рыбной ставки захват ЛКМ доступен рядом без попадания лучом в LeverGrip; подсказка только у приёмника. SlotMachinePlayer order-26 и consumed input исключают перехват ShipV3PlayerInteraction. Обезьянка: бесплатный ход раз180–300с, только свободный автомат, подход спереди по палубному графу с обходом шкафа, Work/рычаг0.8с, Idle/взгляд до окончания спина и выдачи. Проигрыш80%, SkillPoint не выигрывается; оставшиеся20% пропорциональны прежним пяти категориям. При отмене бесплатной ставки не появляется возврат рыбы. Начатый спин не отменяется уходом, помощь раненому может прервать только подход. Игрок: проигрыш60%. Нативный импорт/компиляция без ошибок, PlayMode/build/клиент не запускались.
Mystery допускает все включенные строки ChestLoot.json кроме оружия, включая рыбу, пушку, ядра и утилитарный крюк. Вопросик исправлен горизонтальным UV-разворотом SymbolSurface2.

- [Assets/Scripts/Ships/ShipSlotMachine.cs](<Assets/Scripts/Ships/ShipSlotMachine.cs>) — Исходник C#: ShipSlotMachine.
- [Assets/Scripts/Ships/ShipSlotMachineSettings.cs](<Assets/Scripts/Ships/ShipSlotMachineSettings.cs>) — Исходник C#: SlotSymbol, ShipSlotMachineSettings.
- [Assets/Scripts/Ships/ShipSlotMachineTarget.cs](<Assets/Scripts/Ships/ShipSlotMachineTarget.cs>) — Исходник C#: ShipSlotMachineTarget.
- [Assets/Scripts/Ships/ShipSlotMachinePlayer.cs](<Assets/Scripts/Ships/ShipSlotMachinePlayer.cs>) — Исходник C#: ShipSlotMachinePlayer.
- [Assets/Scripts/Networking/NetworkShip.SlotMachine.cs](<Assets/Scripts/Networking/NetworkShip.SlotMachine.cs>) — Исходник C#: ShipSlotSnapshot, NetworkShip.
- [Assets/Scripts/Networking/NetworkWeapon.SlotMachine.cs](<Assets/Scripts/Networking/NetworkWeapon.SlotMachine.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/SlotPrizeFlight.cs](<Assets/Scripts/Networking/SlotPrizeFlight.cs>) — Исходник C#: SlotPrizeFlight.
- [Assets/Scripts/Networking/SessionRoguelike.cs](<Assets/Scripts/Networking/SessionRoguelike.cs>) — Исходник C#: UpgradeIdentityMessage, SessionController.
- [Assets/Scripts/Networking/NetworkPlayer.Roguelike.cs](<Assets/Scripts/Networking/NetworkPlayer.Roguelike.cs>) — Исходник C#: UpgradeReward, PlayerUpgradeState, UpgradeSnapshot, NetworkPlayer.
- [Assets/Scripts/Loot/ChestLootTable.cs](<Assets/Scripts/Loot/ChestLootTable.cs>) — Исходник C#: ChestLootStack, ChestLootTable, Table, ChestSettings, Entry.
- [Assets/Scripts/Editor/ShipSlotMachineSetup.cs](<Assets/Scripts/Editor/ShipSlotMachineSetup.cs>) — Исходник C#: ShipSlotMachineSetup.
- [Assets/Shaders/SlotReelIcon.shader](<Assets/Shaders/SlotReelIcon.shader>) — Шейдер.
- [Assets/Prefabs/Props/SlotMachine.prefab](<Assets/Prefabs/Props/SlotMachine.prefab>) — Префаб Unity.
- [Assets/Settings/SlotMachine/DefaultSlotMachine.asset](<Assets/Settings/SlotMachine/DefaultSlotMachine.asset>) — Настройки или данные Unity.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/NetworkPlayer.prefab](<Assets/Prefabs/Networking/NetworkPlayer.prefab>) — Префаб Unity.
- [Assets/Scripts/Audio/GameAudioBank.cs](<Assets/Scripts/Audio/GameAudioBank.cs>) — Исходник C#: SoundCue, GameAudioBank, Entry.
- [Assets/UI/SlotMachine/SkillPoint.png](<Assets/UI/SlotMachine/SkillPoint.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/Mystery.png](<Assets/UI/SlotMachine/Mystery.png>) — Изображение / текстура.
- [Assets/Models/SlotMachine/Cabinet.fbx](<Assets/Models/SlotMachine/Cabinet.fbx>) — Модель / анимации FBX.
- [Assets/Models/SlotMachine/Reel.fbx](<Assets/Models/SlotMachine/Reel.fbx>) — Модель / анимации FBX.
- [Assets/Models/SlotMachine/Lever.fbx](<Assets/Models/SlotMachine/Lever.fbx>) — Модель / анимации FBX.
- [Art/Blender/SlotMachine/SlotMachine.blend](<Art/Blender/SlotMachine/SlotMachine.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Networking/NetworkFishing.cs](<Assets/Scripts/Networking/NetworkFishing.cs>) — Исходник C#: NetworkFishing.
- [Assets/Scripts/Player/WeaponArmRig.cs](<Assets/Scripts/Player/WeaponArmRig.cs>) — Исходник C#: WeaponArmRig, Arm.
- [Assets/Scripts/Ships/ShipV3PlayerInteraction.cs](<Assets/Scripts/Ships/ShipV3PlayerInteraction.cs>) — Исходник C#: ShipV3PlayerInteraction.
- [Assets/Scripts/Player/PlayerInventory.cs](<Assets/Scripts/Player/PlayerInventory.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Audio/GameAudio.Selected.cs](<Assets/Scripts/Audio/GameAudio.Selected.cs>) — Исходник C#: GameAudio.
- [Assets/Scripts/UI/RoguelikeUpgradeUI.cs](<Assets/Scripts/UI/RoguelikeUpgradeUI.cs>) — Исходник C#: RoguelikeUpgradeUI.
- [Assets/Resources/GameAudioBank.asset](<Assets/Resources/GameAudioBank.asset>) — Настройки или данные Unity.
- [Assets/Audio/SlotMachine/SOURCES.md](<Assets/Audio/SlotMachine/SOURCES.md>) — Документация.
- [Assets/Audio/SlotMachine/FishInsert.wav](<Assets/Audio/SlotMachine/FishInsert.wav>) — Аудио.
- [Assets/Audio/SlotMachine/LeverPull.wav](<Assets/Audio/SlotMachine/LeverPull.wav>) — Аудио.
- [Assets/Audio/SlotMachine/ReelSpin.wav](<Assets/Audio/SlotMachine/ReelSpin.wav>) — Аудио.
- [Assets/Audio/SlotMachine/ReelStop.wav](<Assets/Audio/SlotMachine/ReelStop.wav>) — Аудио.
- [Assets/Audio/SlotMachine/LeverReturn.wav](<Assets/Audio/SlotMachine/LeverReturn.wav>) — Аудио.
- [Assets/Audio/SlotMachine/FishPayout.wav](<Assets/Audio/SlotMachine/FishPayout.wav>) — Аудио.
- [Assets/Audio/SlotMachine/PrizePayout.wav](<Assets/Audio/SlotMachine/PrizePayout.wav>) — Аудио.
- [Assets/Audio/SlotMachine/Win.wav](<Assets/Audio/SlotMachine/Win.wav>) — Аудио.
- [Assets/UI/SlotMachine/ReelFish.png](<Assets/UI/SlotMachine/ReelFish.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/ReelSkillPoint.png](<Assets/UI/SlotMachine/ReelSkillPoint.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/ReelMystery.png](<Assets/UI/SlotMachine/ReelMystery.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/ReelCannonball.png](<Assets/UI/SlotMachine/ReelCannonball.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/ReelWeapon.png](<Assets/UI/SlotMachine/ReelWeapon.png>) — Изображение / текстура.
- [Assets/UI/SlotMachine/ReelCannon.png](<Assets/UI/SlotMachine/ReelCannon.png>) — Изображение / текстура.
- [Assets/Audio/SlotMachine/LeverRatchet.wav](<Assets/Audio/SlotMachine/LeverRatchet.wav>) — Аудио.
- [Assets/Models/SlotMachine/ReelDividers.asset](<Assets/Models/SlotMachine/ReelDividers.asset>) — Настройки или данные Unity.
- [Assets/Models/SlotMachine/ReelInk.mat](<Assets/Models/SlotMachine/ReelInk.mat>) — Материал Unity.
- [Assets/Scripts/Ships/ShipMonkey.SlotMachine.cs](<Assets/Scripts/Ships/ShipMonkey.SlotMachine.cs>) — Исходник C#: ShipMonkey.
- [Docs/ShipSlotMachine.md](<Docs/ShipSlotMachine.md>) — Документация.
- [Docs/SlotMachineAudio.json](<Docs/SlotMachineAudio.json>) — Конфигурация / данные JSON.
- [Docs/SlotMachineReelArt.md](<Docs/SlotMachineReelArt.md>) — Документация.

### Паруса и канаты (`sails`)

Ключевые слова: паруса, парус, канаты, rigging.

Разделять управление натяжением и визуальную геометрию канатов. Изменение модели не должно менять сетевую занятость.
Настройка канатов и обновление их арта — отдельные редакторские операции; перед повторным запуском изучить соответствующий setup.
У четырёх маршрутов Ship V3 к гнёздам включён ShipLadder.BothSides: AdvancedPlayerController выбирает смещение и направление взгляда по стороне захвата. У боковых сеток PinTop вершины смещаются вместе с весом по высоте, сохраняя верхнее крепление; деформация вдоль нормалей, раздувавшая сетку, убрана.
ShipGripAim расширяет наведение на парусные ручки до 0.38 м от ближайшей точки коллайдера; ShipControlHandle.Active даёт поиск без обязательного попадания лучом в мелкий меш. Видимость, InRange и серверное владение сохранены. Для рынды и маски выдачи ShipV3 применяется тот же допуск, триггеры увеличены до 0.22 м.
Ship V3: RopeTubeVisual восстанавливает постоянное круглое сечение RunningRope по центрам UV-колец деформированного меша; UV пересчитываются по длине. ConfigureSailRopes добавляет пять вращений шкивов в ShipV3VisualRig.Motions, исключая их из статических render batches.
Кастомизация Ship V3 восстановлена: пять парусов и два флага, два слоя изображений, цвет и износ. Семь старых индексов сохранены, фор-марсель добавлен как индекс 7; архивный третий флаг скрыт. У V3 UV 0..1 и лицевая/обратная сторона определяются ориентацией грани. Коллайдеры выбора создаются только в меню. Название до 28 текстовых элементов сохраняется с пресетом и передаётся вместе с кастомизацией; сервер принимает данные первого участника команды и раздаёт изображения поздним клиентам.
Название корабля собирается из настоящих мешей букв Georgia Bold, кириллица/латиница/цифры. Для читаемости с 5 м заполнение светло-золотистое, штрихи слегка утолщены, латунный кант расширен до 0.032 cap с ограничением по толщине штриха; отверстия реальные. Непрозрачный URP Lit закрывается геометрией корабля. ShipNameGlyphLibrary содержит меши, advance и kerning; ShipNameplate объединяет имя в два submesh, вписывает в 4.2×0.48 м, сжимает ширину глифов до 0.88 и обновляет только при изменении строки. Face/rim имеют металлическость 0.15/0.35, отдельный материал канта не меняет рамку доски. Бизань получает runtime-копию меша с ортогональной UV2, UV0/FBX/анимации сохранены. Обычные картинки сохраняют RGB при тёплом свете; размеры исходного изображения и физические размеры парусов задают правильные пропорции.

- [Assets/Scripts/SailSystem.cs](<Assets/Scripts/SailSystem.cs>) — Исходник C#: SailSystem.
- [Assets/Scripts/Interaction/DirectShipControls.cs](<Assets/Scripts/Interaction/DirectShipControls.cs>) — Исходник C#: DirectShipControls.
- [Assets/Scripts/Interaction/ShipControlHandle.cs](<Assets/Scripts/Interaction/ShipControlHandle.cs>) — Исходник C#: ShipControlHandle.
- [Assets/Scripts/Interaction/SailRopeVisual.cs](<Assets/Scripts/Interaction/SailRopeVisual.cs>) — Исходник C#: SailRopeVisual.
- [Assets/Scripts/Interaction/SailRopeMesh.cs](<Assets/Scripts/Interaction/SailRopeMesh.cs>) — Исходник C#: SailRopeMesh.
- [Assets/Scripts/Editor/SailRopeSetup.cs](<Assets/Scripts/Editor/SailRopeSetup.cs>) — Исходник C#: SailRopeSetup.
- [Assets/Scripts/Editor/SailRiggingArtSetup.cs](<Assets/Scripts/Editor/SailRiggingArtSetup.cs>) — Исходник C#: SailRiggingArtSetup.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Customization/SailCustomizer.cs](<Assets/Scripts/Customization/SailCustomizer.cs>) — Исходник C#: SailCustomizer.
- [Assets/Scripts/Customization/SailCustomizationUI.cs](<Assets/Scripts/Customization/SailCustomizationUI.cs>) — Исходник C#: SailCustomizationUI, GizmoMode.
- [Assets/Scripts/Customization/SailCustomizationStorage.cs](<Assets/Scripts/Customization/SailCustomizationStorage.cs>) — Исходник C#: SailData, ShipCustomizationData, SailCustomizationStorage.
- [Assets/Scripts/Customization/SailImageLoader.cs](<Assets/Scripts/Customization/SailImageLoader.cs>) — Исходник C#: SailImageLoader, OpenFileName.
- [Assets/Shaders/Sail.shader](<Assets/Shaders/Sail.shader>) — Шейдер.
- [Assets/Scripts/Ships/ShipV3VisualRig.cs](<Assets/Scripts/Ships/ShipV3VisualRig.cs>) — Исходник C#: ShipV3Pose, ShipV3Motion, ShipV3VisualRig.
- [Assets/Scripts/Interaction/ShipGripAim.cs](<Assets/Scripts/Interaction/ShipGripAim.cs>) — Исходник C#: ShipGripAim.
- [Assets/Scripts/Interaction/RopeTubeVisual.cs](<Assets/Scripts/Interaction/RopeTubeVisual.cs>) — Исходник C#: RopeTubeVisual, Centerline.
- [Assets/Scripts/Customization/SailNetworkSync.cs](<Assets/Scripts/Customization/SailNetworkSync.cs>) — Исходник C#: SailNetworkSync, ChunkAssemblyBuffer.
- [Assets/Scripts/Customization/ShipNameplate.cs](<Assets/Scripts/Customization/ShipNameplate.cs>) — Исходник C#: ShipNameplate.
- [Assets/Scripts/Editor/ShipCustomizationSetup.cs](<Assets/Scripts/Editor/ShipCustomizationSetup.cs>) — Исходник C#: ShipCustomizationSetup, GlyphMetadata, KerningMetadata, GlyphMetadataSet.
- [Assets/Resources/Ships/ShipV3Menu.prefab](<Assets/Resources/Ships/ShipV3Menu.prefab>) — Префаб Unity.
- [Assets/Models/Ships/ShipNameplate/ShipNameplate.fbx](<Assets/Models/Ships/ShipNameplate/ShipNameplate.fbx>) — Модель / анимации FBX.
- [Assets/Scripts/Customization/ShipNameGlyphLibrary.cs](<Assets/Scripts/Customization/ShipNameGlyphLibrary.cs>) — Исходник C#: ShipNameGlyphLibrary, Glyph, Kerning.
- [Assets/Resources/Customization/ShipNameGlyphs.asset](<Assets/Resources/Customization/ShipNameGlyphs.asset>) — Настройки или данные Unity.
- [unity.md](<unity.md>) — Документация.
- [blender.md](<blender.md>) — Документация.

### Пушки, ядра и лафеты (`cannons`)

Ключевые слова: пушки, пушка, cannon, ядра, мортира.

Проверять серверные условия выстрела, загрузки и занятости; локальные эффекты не подтверждают сетевой выстрел.
Для движения ядра и лафета учитывать движение корабля. Баланс брать из текущих полей и ассетов, а не старых записей.
ShipSpyglassView показывает прогноз траекторий пушек своего корабля через стационарную и ручную подзорные трубы, обновляет каждые 0.2 с и скрывает линии при выходе.
CannonSmokeTrail: дым в мировых координатах по пройденным сегментам, затухает за 3.3 с и сохраняется после уничтожения ядра; общий более тёмный серый материал частиц, масштаб 0.3 для огнестрельного оружия. CannonShotDamage: обычные, ледяные и толкающие ядра рикошетят от окружения с потерей скорости; корабли и живые цели сохраняют урон, огненные и мортирные снаряды — взрыв. WorldStructureCollision учитывает готовые соседние _COL, добавляет недостающий MeshCollider только для читаемой статической геометрии; для нечитаемой использует BoxCollider. Ревизия 2 входит в CatalogHash.
Новые лутаемые модели из Blender/Лутабельные подключены через LootModelReplacementSetup к прежним игровым префабам. Пушка собрана из CannonBase (исходная «люлька»), CannonMount («Лафет»), CannonBarrel и четырёх CannonWheel. SimpleCannon.TraversePivot поворачивает ложе отдельно от наклона BarrelPivot; при отсутствии TraversePivot сохранена прежняя схема. CannonWheelVisual вращает колёса от перемещения относительно корабля, включая сетевое движение и отдачу. Коллайдер ствола и Breech следуют новым частям. Стрельба, боеприпасы и параметры лафета сохранены.
Кинематическое качение Cannonball учитывает столкновения с другими свободными ядрами: SphereCast движения, устранение перекрытий через OverlapSphereNonAlloc, импульс с учётом массы и скорости корабля. Опорная палуба ищется без других ядер. Серверный NetworkLooseCannonball передаёт итоговые позы прежним способом; загруженные ядра, удержание и механика выстрела сохранены.
Парный BoardingHook: BoardingShotFlight плавно разводит два гарпуна, моделирует два попадания и вытягивание верёвок. Хост хранит два BoardingCable с общим Shot и отдельными Hook/Hits; каждому нужны два удара саблей. BoardingWalkSurface создаёт поперечины и поверхность для бега; при потере одного крепления остаётся один проходимый трос. Не создавать повторное крепление от устаревшего выстрела после следующего выстрела этой пушки. Протокол 111; игровая и сетевая проверка остаются пользователю.
NetworkCannon отправляет изменения placements каждые 0.05 с; удалённый клиент сглаживает положение, вращение лафета, наклон и поворот механизма каждый кадр между полученными состояниями. Собственное управление и серверная физика сохраняются. Подаваемое ядро также получает промежуточные положения; NetworkLooseCannonball SyncVars настроены на 0.05 с. Буферы очищаются при OnStopClient. Онлайн-проверка двумя клиентами не выполнялась.
Абордажные ядра лежат без качения относительно корабля; короткая проверка опоры включает падение при разрушении палубы. Оба троса выстрела идут из центра дула и расширяются к гарпунам. Крепление хранит точную секцию и ближайший фрагмент; его разрушение снимает трос и поперечины. RopeTubeVisual рисует круглые тросы с UV по длине. HookRope использует цветовую и нормальную карты пенькового каната Ship V3.
Огненное ядро использует ограниченное распространение по связным фрагментам Ship V3. Бюджет очагов ceil(число первично повреждаемых обычным ядром фрагментов × 1.5), с учётом оставшейся геометрии. Очаги горят 5–9 с, прогоревшие фрагменты удаляются штатными масками разрушения и потерей опор. Огонь поджигает проходящий экипаж на 5–9 с, урон 15 здоровья/с; море и ледяные попадания тушат огонь, мокрые цели защищены 3 с. ShipFireVfx: анимированное пламя, мировой дым, искры, пар и один общий ближайший свет без теней. Отдельный ShipFire shader выбирает red-маску для пламени/искр и alpha для дыма/пара.
Огненное ядро 2026-10-06: прежний округлённый бюджет очагов удвоен, максимальный квадрат дистанции распространения увеличен 36→72 м². Остальные время горения, урон и тушение сохранены.
Морозное ядро: NetworkShip.FreezeFromShot останавливает корабль на 5 с и очищает все fireExposure/firePatches, включая ожидающее распространение. Живые игроки с фактической опорой ShipDeckPassenger.Ship==Body замораживаются на 1 с через NetworkHealth.frozenUntil (синхронизированный серверный Tick). Урон сохраняется; движение, отдача, оружие и взаимодействия запрещены, текущие механизмы/работы освобождаются. Повторный удар обновляет сроки; огненное попадание немедленно снимает заморозку корабля и начинает обычное горение. Протокол 126.
ShipFreezeVfx: дополнительная освещённая островковая оболочка по текущим meshes без копирования и замены базовых материалов, трещины и 256 instanced кристаллов. Source renderers из ShipV3RenderBatch исключены; батчи/штурвал/мачты имеют приоритет, максимум 144 shell draw. V3 seeds: 64/64 борта, 40 основная палуба, 20 бак, 20 квартердек, 40 мачты, 8 штурвал; Section/RemovedFragments и renderer visibility не позволяют оставлять лёд в пустотах. Рост ~0.4 с, таяние последние 0.5 с. Короткий ледяной burst +18 холодных пылинок, дымовой атлас 2×2. PlayerFreezeScreen показывает морозные края на 1 с, центр остаётся видимым. Ресурсы/привязки проверены native Unity MCP; компиляция без ошибок, игровой внешний вид/сетевая проверка не выполнялись.
Огонь против льда: Ignite снимает Motor.freezeRemaining и синхронизированный frozen до расчёта очагов; защита shipWetUntil удалена. Обычные wetFragments от тушения водой сохраняются. ShipFreezeVfx захватывает текущие age/origin/fade и плавно убирает оболочку и уменьшает кристаллы за 0.25 с; повторные Present(0) не продлевают таяние, новый мороз отменяет таяние, ClearAmmo выключает эффект сразу. Заморозка игроков на 1 с не изменяется. VFX review APPROVED по коду и native Unity данным; игра не запускалась.
Подводные снаряды 2026-10-06: все типы ядер, включая мортиру и огненные/ледяные/толкающие/бумеранг/абордажные, проходят поверхность OceanSurface, тормозятся квадратичным сопротивлением и сохраняют попадания. Подводный срок 6 с; бумеранг после погружения переходит на баллистику. MortarTrajectory показывает продолжение в воде, взрыв только при контакте с целью. Runtime ProjectileWaterFlight и UnderwaterProjectileTrail используют общий пузырьковый материал Underwater/Bubbles. Компиляция проверена; игра и второй клиент не запускались.
CannonCarriage.Kick даёт небольшой толчок0.52м/с с обычным RollDrag и существующими проверками опоры/препятствий. При RollDrag2.6 свободный путь около20см.
Отталкивание обычным ядром 2026-10-09: Impact раньше наносил ближайшим игрокам урон, но KnockDown вызывался только при прямом контакте. PushPlayersNearImpact на сервере перебирает CombatHealth.Active и расстояние до капсулы, вызывает KnockDown2.8с с22м/с наружу и8м/с вверх в StandardPushRadius3м; радиус урона0.8м сохранён. Для обычного мортирного ядра используется максимум радиуса взрыва и3м. Прежнее ограничение толчка союзников сохранено. Компиляция проверена, игровой/сетевой результат проверяет пользователь. Рэгдолл ядра: обычный Cannonball уже использует общий KnockDown → PlayerKnockdown → DeathRagdoll.Build. PushCannonball переведён с PushByUpgrade на тот же физический KnockDown, включая прямое попадание в игрока. ImpactPushVelocity задаёт единое направление от точки контакта к игроку и8м/с вверх; при совпадении центров используется направление движения ядра. Радиусы,22м/с и командные фильтры сохранены; модификатор HeavyCannonball применяется только к обычному ядру. Импорт/компиляция проверены, игровой результат проверяет пользователь.

- [Assets/Scripts/Cannons/SimpleCannon.cs](<Assets/Scripts/Cannons/SimpleCannon.cs>) — Исходник C#: SimpleCannon.
- [Assets/Scripts/Cannons/NetworkCannon.cs](<Assets/Scripts/Cannons/NetworkCannon.cs>) — Исходник C#: CannonPlacement, NetworkCannon, RemoteCarriagePose.
- [Assets/Scripts/Cannons/Cannonball.cs](<Assets/Scripts/Cannons/Cannonball.cs>) — Исходник C#: Cannonball.
- [Assets/Scripts/Cannons/CannonShotDamage.cs](<Assets/Scripts/Cannons/CannonShotDamage.cs>) — Исходник C#: CannonShotDamage.
- [Assets/Scripts/Cannons/CannonSmokeTrail.cs](<Assets/Scripts/Cannons/CannonSmokeTrail.cs>) — Исходник C#: CannonSmokeTrail.
- [Assets/Scripts/World/WorldStructureCollision.cs](<Assets/Scripts/World/WorldStructureCollision.cs>) — Исходник C#: WorldStructureCollision.
- [Assets/Scripts/Cannons/CannonCarriage.cs](<Assets/Scripts/Cannons/CannonCarriage.cs>) — Исходник C#: CannonCarriage.
- [Assets/Scripts/Cannons/MortarTrajectory.cs](<Assets/Scripts/Cannons/MortarTrajectory.cs>) — Исходник C#: MortarTrajectory.
- [Assets/Scripts/Networking/NetworkLooseCannonball.cs](<Assets/Scripts/Networking/NetworkLooseCannonball.cs>) — Исходник C#: NetworkLooseCannonball.
- [Assets/Scripts/Editor/CannonInventorySetup.cs](<Assets/Scripts/Editor/CannonInventorySetup.cs>) — Исходник C#: CannonInventorySetup.
- [Assets/Scripts/Cannons/CannonWheelVisual.cs](<Assets/Scripts/Cannons/CannonWheelVisual.cs>) — Исходник C#: CannonWheelVisual.
- [Assets/Scripts/Editor/LootModelReplacementSetup.cs](<Assets/Scripts/Editor/LootModelReplacementSetup.cs>) — Исходник C#: LootModelReplacementSetup.
- [Assets/Scripts/Cannons/BoardingShotFlight.cs](<Assets/Scripts/Cannons/BoardingShotFlight.cs>) — Исходник C#: BoardingShotFlight.
- [Assets/Scripts/Cannons/BoardingWalkSurface.cs](<Assets/Scripts/Cannons/BoardingWalkSurface.cs>) — Исходник C#: BoardingWalkSurface.
- [Assets/Scripts/Cannons/BoardingHookTarget.cs](<Assets/Scripts/Cannons/BoardingHookTarget.cs>) — Исходник C#: BoardingHookTarget.
- [Assets/Scripts/Networking/NetworkBoarding.cs](<Assets/Scripts/Networking/NetworkBoarding.cs>) — Исходник C#: BoardingCable, NetworkCannon.
- [Assets/Scripts/Interaction/RopeTubeVisual.cs](<Assets/Scripts/Interaction/RopeTubeVisual.cs>) — Исходник C#: RopeTubeVisual, Centerline.
- [Assets/Scripts/Cannons/ShipFireVfx.cs](<Assets/Scripts/Cannons/ShipFireVfx.cs>) — Исходник C#: ShipFireVfx.
- [Assets/Scripts/Editor/ShipFireVfxSetup.cs](<Assets/Scripts/Editor/ShipFireVfxSetup.cs>) — Исходник C#: ShipFireVfxSetup.
- [Assets/Scripts/Networking/NetworkShipAmmo.cs](<Assets/Scripts/Networking/NetworkShipAmmo.cs>) — Исходник C#: ShipFirePatch, ShipFreezeState, NetworkShip, FireExposure.
- [Assets/Resources/VFX/ShipFireVfx.prefab](<Assets/Resources/VFX/ShipFireVfx.prefab>) — Префаб Unity.
- [Assets/Resources/VFX/ShipFireFlame.mat](<Assets/Resources/VFX/ShipFireFlame.mat>) — Материал Unity.
- [Assets/Resources/VFX/ShipFireSmoke.mat](<Assets/Resources/VFX/ShipFireSmoke.mat>) — Материал Unity.
- [Assets/Resources/VFX/ShipFireEmber.mat](<Assets/Resources/VFX/ShipFireEmber.mat>) — Материал Unity.
- [Assets/Resources/ShipFire.shader](<Assets/Resources/ShipFire.shader>) — Шейдер.
- [Assets/Scripts/World/WaterImpactPhysics.cs](<Assets/Scripts/World/WaterImpactPhysics.cs>) — Исходник C#: WaterImpactKind, WaterImpactEvent, WaterImpactPhysics.
- [Assets/Scripts/World/WaterImpactBody.cs](<Assets/Scripts/World/WaterImpactBody.cs>) — Исходник C#: WaterImpactBody.
- [Assets/Scripts/Cannons/ShipFreezeVfx.cs](<Assets/Scripts/Cannons/ShipFreezeVfx.cs>) — Исходник C#: ShipFreezeVfx, IceSeed, Surface.
- [Assets/Scripts/Player/PlayerFreezeScreen.cs](<Assets/Scripts/Player/PlayerFreezeScreen.cs>) — Исходник C#: PlayerFreezeScreen.
- [Assets/Scripts/Editor/ShipFreezeVfxSetup.cs](<Assets/Scripts/Editor/ShipFreezeVfxSetup.cs>) — Исходник C#: ShipFreezeVfxSetup, Candidate.
- [Assets/Scripts/Networking/NetworkHealth.cs](<Assets/Scripts/Networking/NetworkHealth.cs>) — Исходник C#: NetworkHealth.
- [Assets/Resources/VFX/ShipIce.shader](<Assets/Resources/VFX/ShipIce.shader>) — Шейдер.
- [Assets/Resources/VFX/ShipIceShell.mat](<Assets/Resources/VFX/ShipIceShell.mat>) — Материал Unity.
- [Assets/Resources/VFX/ShipIceCrystal.mat](<Assets/Resources/VFX/ShipIceCrystal.mat>) — Материал Unity.
- [Assets/Resources/VFX/ShipIceCrystalMesh.asset](<Assets/Resources/VFX/ShipIceCrystalMesh.asset>) — Настройки или данные Unity.
- [Assets/Resources/VFX/PlayerFreezeScreen.shader](<Assets/Resources/VFX/PlayerFreezeScreen.shader>) — Шейдер.
- [Assets/Resources/VFX/PlayerFreezeScreen.mat](<Assets/Resources/VFX/PlayerFreezeScreen.mat>) — Материал Unity.
- [Assets/Scripts/World/ProjectileWaterFlight.cs](<Assets/Scripts/World/ProjectileWaterFlight.cs>) — Исходник C#: ProjectileWaterFlight.
- [Assets/Scripts/World/UnderwaterProjectileTrail.cs](<Assets/Scripts/World/UnderwaterProjectileTrail.cs>) — Исходник C#: UnderwaterProjectileTrail.
- [combat-balance.md](<combat-balance.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Персонаж, камера и анимации (`player`)

Ключевые слова: игрок, персонаж, камера, анимации.

AdvancedPlayerController.Spectator: через 1.5 с после смерти автоматически наблюдает живого союзника, ЛКМ переключает; после Eliminated — свободный полёт WASD/Space/Ctrl, Shift ускоряет. Камера перемещается локально, тело остаётся на месте; возрождение возвращает обычное управление. ShipObserverCondition передаёт умершим/выбывшим объекты вне обычного радиуса видимости. Ввод и камера принадлежат локальному игроку; движение наблюдателей и анимации сверять с сетевым состоянием. Камера NetworkPlayer использует сферическое отсечение по слоям на 1000 м; дальняя плоскость вынесена до 5000 м, чтобы поворот камеры не менял дальность видимости. AdvancedPlayerController сохраняет эту настройку при запуске.
Проверять привязку компонентов к NetworkPlayer. Совпадение имён костей не гарантирует совместимость анимаций.
NetworkPlayer использует Tripo/Mixamo Walking.fbx из NewPirate. Лицо и борода имеют исправленные веса Head; исходник и способ сохранения FBX — Art/Blender/Characters/NewPirate/README.md.
AdvancedPlayerController: новое нажатие пробела у поверхности воды (глубина ног <= 1.4 м) запускает прыжок с высотой jumpHeight над водой; при подъёме персонаж остаётся в воздушной симуляции. В глубине удержание пробела по-прежнему поднимает пловца. Используются существующие сетевые Swimming/VerticalVelocity без новых полей состояния.
F2 при наличии сетевого ShipV3Features переносит живого локального игрока на RespawnPoint нового корабля в текущей сцене. ServerRpc на NetworkPlayer и ObserversRpc сбрасывают состояние перемещения через Teleport, освобождают механизмы и прикрепляют пассажира к новому кораблю; домашний корабль не меняется.
Новый корабль использует FollowRopePath у ShipLadder: четыре боковых маршрута к гнёздам и две посадочные сетки, от настоящих нижних вершин мешей до выхода над ограждением. TopSideOffset и TopLean учитывают наклон в двух направлениях; RopeStandOff оставляет пловца снаружи сетки. Для этих маршрутов не создаётся сплошная вертикальная стенка. У посадочных сеток высота маршрута заканчивается на фактическом верхнем ряду без прибавки 1.1 м; выход расположен на поверхности палубы, вычисленной по треугольникам коллайдеров. AdvancedPlayerController использует те же маршруты в существующей сетевой симуляции; прежние лестницы основного корабля сохраняют настройки по умолчанию. После правки проверены только компиляция и edit-mode привязки, игровой онлайн-прогон не выполнялся.
ShipDeckPassenger учитывает BoardingWalkSurface: перенос по текущей кривой троса между двумя кораблями, обычное управление движением сохранено. При превращении лестницы в одиночную верёвку игрок сохраняет мировое положение; потерявший опору падает. Положение продолжает передаваться существующим состоянием NetworkPlayer относительно корабля пушки.
Заморозка от корабельного морозного попадания: NetworkHealth синхронизирует конечный серверный Tick на 1 с. AdvancedPlayerController.IsFrozen блокирует InputActive/LocomotionLocked и саму Simulate до применения движения, включая knockback/grapple; получение урона не меняется. PlayerFreezeScreen даёт плавный мороз по краям только экрану владельца; повторный удар продлевает эффект без мигания. Авторитетные ворота предметов, механизмов, инвентаря и BotActionExecutor также учитывают заморозку.
Пинок на X: серверный КД 3 с, контакт через 0.16 с, одна ближайшая цель до 1.9 м в конусе перед игроком с проверкой препятствий. Союзники и враги получают отдельный импульс11.3м/с с затуханием6/с и подброс3.6м/с: расчётный свободный сдвиг около1.7м и подъём около0.2м при30Hz, с базовым уроном5 и без обычного нокдауна; KickPushVelocity и VerticalVelocity включены в prediction/reconcile. Пинок блокируется в меню, плавании, лазании и занятиях. PlayerKickVisual сгибает правую ногу существующего Mixamo-рига, KickLeg показывает ту же ногу владельцу от первого лица. У края сервер проверяет опору под целью и отсутствие опоры за ней по направлению пинка, исключает препятствия выше1.6м. При выходе наружу вызывает штатный KnockDown на2.8с, отцепляет пассажира и учитывает высоту низкого ограждения для перелёта. Высота отдельной секции в объединённом collider берётся из ShipV3CollisionBatch.SourceBounds. Protocol134; игровая/мультиплеерная проверка оставлена пользователю.
Пинок включает trigger-hitbox обезьянки, остальные trigger-коллайдеры не становятся целями. Попадание запускает ShipMonkey.ReceiveKick и звук KickBody.
Падение 2026-10-09 после повторной игровой жалобы: PlayerKnockdown.Begin использует тот же DeathRagdoll.Build и CorpsePhysicsWorld, что смерть. Физический рэгдолл получает скорость один раз; перенос его сегментов к CharacterController полностью удалён. Во время KnockDown motor.Simulate следует физическому тазу, а не двигает параллельную капсулу; сервер передаёт эту позицию существующими snapshots/reconcile. При окончании позиция восстановления берётся из фактического таза с ближайшей поверхностью под ним. CreateKnockdown разрешён на headless-сервере; активные тела нокдауна не удаляются лимитом декоративных трупов. У края пинок даёт5м/с наружу и минимум6.5м/с вверх; подъём считается по Physics.gravity и высоте ограждения, без управляемой дуги. Protocol134. Проверена компиляция; PlayMode/второй клиент проверяет пользователь. Доработка после подтверждения пользователем: проверка отсутствия палубы вынесена на radius+2м, чтобы выброс включался примерно за два метра от края. Пинок игроку наносит5 базового урона через CombatHealth.Damage с прежними командными фильтрами/улучшениями; при смертельном ударе дополнительный KnockDown не вызывается.
Исправление исчезающего рэгдолла 2026-10-09: DeathRagdoll копирует skin независимо от activeInHierarchy исходного контейнера, активирует его цепочку, сохраняет layer/renderingLayerMask и не скрывает игрока при пустой копии. RagdollRendererBounds следует за тазом, чтобы физическое тело не отсекалось по старым границам. Проверка Build на NetworkPlayer через Unity MCP без PlayMode: 1 активный skin, 52 кости, BakeMesh26304 вершины, 11 Rigidbody и10 CharacterJoint; компиляция без ошибок. Вид/движение на втором клиенте остаются непроверенными. Камера первого лица при нокдауне следует позиции и quaternion головы физического рэгдолла с сохранением исходного направления взгляда; фиксированный наклон камеры больше не перезаписывает её. Общий DeathRagdoll.Build для нокдауна и смерти с вероятностью50% добавляет кувырок5–8рад/с вокруг поперечной оси и небольшой крен/поворот; таз, грудь и голова получают одинаковое вращение, конечности слегка различаются без встречного вращения. Компиляция проверена; ощущение камеры и сетевой результат проверяет пользователь. PlayerKnockdown после окончания нокдауна отключает физику копии и за0.95с смешивает позу её костей к текущему BodyRig; исходная модель и руки возвращаются после перехода. Камера начинает с последней позы головы, сначала выравнивается у земли, затем плавно поднимается к обычной высоте. Начальная поза хранится относительно игрока для движущейся палубы; повторный нокдаун/смерть прерывает вставание. Локальный InputActive блокируется на время перехода; сетевые таймеры нокдауна сохранены. Компиляция проверена, игровую плавность проверяет пользователь.

- [Assets/Scripts/AdvancedPlayerController.cs](<Assets/Scripts/AdvancedPlayerController.cs>) — Исходник C#: AdvancedPlayerController.
- [Assets/Scripts/AdvancedPlayerController.Spectator.cs](<Assets/Scripts/AdvancedPlayerController.Spectator.cs>) — Исходник C#: AdvancedPlayerController.
- [Assets/Scripts/Networking/ShipObserverCondition.cs](<Assets/Scripts/Networking/ShipObserverCondition.cs>) — Исходник C#: ShipObserverCondition.
- [Assets/Scripts/ShipDeckPassenger.cs](<Assets/Scripts/ShipDeckPassenger.cs>) — Исходник C#: ShipDeckPassenger.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Player/PlayerAnimatorDriver.cs](<Assets/Scripts/Player/PlayerAnimatorDriver.cs>) — Исходник C#: PlayerAnimatorDriver.
- [Assets/Scripts/Player/FirstPersonModelVisibility.cs](<Assets/Scripts/Player/FirstPersonModelVisibility.cs>) — Исходник C#: FirstPersonModelVisibility.
- [Assets/Scripts/Editor/PirateAnimationRetargeter.cs](<Assets/Scripts/Editor/PirateAnimationRetargeter.cs>) — Исходник C#: PirateAnimationRetargeter.
- [Assets/Prefabs/Networking/NetworkPlayer.prefab](<Assets/Prefabs/Networking/NetworkPlayer.prefab>) — Префаб Unity.
- [Assets/Models/Characters/NewPirate/Walking.fbx](<Assets/Models/Characters/NewPirate/Walking.fbx>) — Модель / анимации FBX.
- [Art/Blender/Characters/NewPirate/Pirate_HeadWeights.blend](<Art/Blender/Characters/NewPirate/Pirate_HeadWeights.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Characters/NewPirate/repair_head_weights.py](<Art/Blender/Characters/NewPirate/repair_head_weights.py>) — Инструмент Python.
- [Art/Blender/Characters/NewPirate/README.md](<Art/Blender/Characters/NewPirate/README.md>) — Документация.
- [Assets/Scripts/Networking/NetworkPlayer.TestShip.cs](<Assets/Scripts/Networking/NetworkPlayer.TestShip.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/Ships/ShipV3PlayerInteraction.cs](<Assets/Scripts/Ships/ShipV3PlayerInteraction.cs>) — Исходник C#: ShipV3PlayerInteraction.
- [Assets/Scripts/Cannons/BoardingWalkSurface.cs](<Assets/Scripts/Cannons/BoardingWalkSurface.cs>) — Исходник C#: BoardingWalkSurface.
- [Assets/Scripts/World/WaterImpactPhysics.cs](<Assets/Scripts/World/WaterImpactPhysics.cs>) — Исходник C#: WaterImpactKind, WaterImpactEvent, WaterImpactPhysics.
- [Assets/Scripts/World/WaterImpactBody.cs](<Assets/Scripts/World/WaterImpactBody.cs>) — Исходник C#: WaterImpactBody.
- [Assets/Scripts/Networking/NetworkHealth.cs](<Assets/Scripts/Networking/NetworkHealth.cs>) — Исходник C#: NetworkHealth.
- [Assets/Scripts/Player/PlayerFreezeScreen.cs](<Assets/Scripts/Player/PlayerFreezeScreen.cs>) — Исходник C#: PlayerFreezeScreen.
- [Assets/Scripts/Networking/NetworkPlayer.Kick.cs](<Assets/Scripts/Networking/NetworkPlayer.Kick.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/Player/PlayerKickVisual.cs](<Assets/Scripts/Player/PlayerKickVisual.cs>) — Исходник C#: PlayerKickVisual.
- [Assets/Resources/KickLeg.asset](<Assets/Resources/KickLeg.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Player/DeathRagdoll.cs](<Assets/Scripts/Player/DeathRagdoll.cs>) — Исходник C#: DeathRagdoll, RagdollRendererBounds.
- [Assets/Scripts/Player/PlayerKnockdown.cs](<Assets/Scripts/Player/PlayerKnockdown.cs>) — Исходник C#: PlayerKnockdown.
- [animation-review.md](<animation-review.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Личное оружие и урон (`weapons`)

Ключевые слова: оружие, пистолет, бой, combat, мортира, hand-mortar, взрывное ядро, explosive-ball.

Разделять локальный отклик оружия и серверное подтверждение урона/расхода боеприпасов.
Настройки оружия искать через FirearmDefinition и используемые ссылки; не копировать числовой баланс из истории.
PistolBullet создаёт мини-дым CannonSmokeTrail по фактическому движению визуальной пули для пистолета, мушкета и каждой дробины двустволки, включая предсказанные и удалённые выстрелы. Масштаб 0.3, шаг 0.12 м, затухание 3.3 с; дым сохраняется при попадании и повторном использовании пула трассеров.
SabreAnimation использует отдельный хват Mixamo и переносит движение игровой сабли в координаты камеры для первого лица. Слой SabreCombat активен при выбранной сабле; Ready использует New_SabreReady. NewPirateAnimationBatch сохраняет эту стойку при переимпорте.
Три модели из Blender/Лутабельные/Оружие заменены сборками Art/Blender/Firearms/FirearmAssemblies.blend и Assets/Models/Firearms. Один спуск на оружие; кремнёвые замки справа у пистолета/снайперки и зеркально с обеих сторон дробовика. FirearmMechanism анимирует спуск, удар через 0.065 с, искры FirearmVfx и возврат на -12° до перезарядки. Существующие GUID префабов и баланс сохранены. FirearmModelReplacementSetup подключает модели и палубные точки; ExperimentalShipEquipment повторяет спавн снайперки/дробовика сразу после подбора. Пикапы неподвижны относительно корабля через NetworkFish, без механики качения ядер. Пистолет остаётся в стартовом инвентаре. Play Mode и сетевая приёмка не выполнялись.
Исправление хвата 2026-10-04: старые MusketReady/ShotgunReady и Reload используют пути PirateRig, игровой персонаж — Mixamo. NetworkPlayer.Models теперь назначает канонические MusketVisual/ShotgunVisual с GripSocket_Firearm и SupportSocket_Firearm. CharacterActions для Mixamo не переносит старый ActionProp; Ready/Aim/Reload используют New_PistolAim, огонь — New_Shooting. WeaponArmRig совмещает ладони с точками хвата, ориентирует кисти и ограничивает положение оружия длиной обеих рук; отдача/перезарядка NetworkEquipment сохранены. NewPirateAnimationBatch сохраняет назначения поз. Сабля центрирована по фактической древесине рукояти, смещение первого лица отодвинуто от камеры. SabreWoodHit использует проверенный camera-eye и направление прицела с физической дальностью 2.4 м и контролем препятствий. Зарубки увеличены до 26–34×3.5–5 см; SabreWoodImpact проецирует сетку13×5 на активный collider той же SectionId и обрезает клетки на краях/разрывах, включая округлые перила. След30+3с и сетевое событие с дедупликацией сохранены. Игровые/визуальные проверки и Play Mode не запускались.
Уточнение размеров и хвата 2026-10-04: итоговые длины корпуса пистолета 0.324 м, снайперки 1.062 м, дробовика 0.648 м (Visual scale 0.72/0.9/0.9). Подтверждённый хват длинного оружия сохранён; пистолет получил GripSocket_Firearm, наклон кисти по рукояти и позу 20 правых пальцев из New_PistolAim. Сабля уменьшена до 0.735 м и развёрнута на 180° вокруг продольной оси клинка в первом/третьем лице и сброшенном визуале. Сабля разбивает Fog/Vortex через прежний проверенный контакт прицела; огнестрел использует IWeaponTarget. Glass добавлен в конец BulletSurfaceKind; бутылочное попадание не создаёт обычный след/звук пули и не передаёт удаляемый Anchor. Компиляция и сохранённые ссылки проверены; Play Mode и игровые/визуальные проверки не запускались.
Снайперка имеет ShoulderSocket_Firearm на затыльнике, GripSocket_Firearm на рукояти и SupportSocket_Firearm на цевье. NetworkEquipment передаёт смещение плеча и текущую отдачу/движение. WeaponArmRig в мировой стойке закрепляет приклад на плече текущего скелета; при недостижимом цевье левый хват сдвигается вдоль оружия до доступной длины руки, без переноса приклада и без соседней точки правого хвата. При перезарядке освобождается плечевой контакт. FirearmModelReplacementSetup сохраняет сокеты при повторном импорте; масштаб и баланс не изменены.
NetworkHealth синхронизирует горение экипажа и считает урон на сервере. Контакт с корабельным пламенем поджигает игрока, экологический урон действует и на союзников; погружение в море и ледяное попадание тушат, смерть/возрождение очищают эффект.
Прицел снайперки 2026-10-06: маска рисуется только в Repaint и заполняется одним SetPixels32. Лог пользовательского краша показывает native CharacterController.Move; для объединённых корабельных коллайдеров добавлен обход без UseFastMidphase. Повторное воспроизведение краша оставлено пользователю.
Подводная стрельба 2026-10-06: пистолет, снайперка и дробь переходят от воздушного hitscan к серверному UnderwaterFirearmProjectile на 3 с; квадратичное торможение, текущие коллайдеры пловцов, общий DamageCap на залп. PistolBullet показывает пузырьковую траекторию и завершается по серверному результату, без повторного дульного эффекта. FirearmShot передаёт WaterVelocity/ProjectileId; отдельный observer RPC для подводного попадания. SessionConfig.ProtocolVersion128, клиентам нужна одна версия. Компиляция проверена; игровой/сетевой тест не запускался.
HandMortar=27: готовые четыре модели собраны с исходными PBR-текстурами и отдельными pivots. ЛКМ: спуск, удар кремня через .065с, верхний фитиль и выстрел через .2с; механизмы возвращаются в покой. При успешном серверном вылете ядра отдача стрелку14м/с назад по горизонтали и0.8м/с вверх через NetworkPlayer.KnockDown с рэгдоллом стрелка2.5с и сетевым уведомлением; ShooterKnockback/ShooterLift/ShooterKnockdownSeconds в HandMortar.asset. Один заряд, R базовая перезарядка 10с без отдельного расходного предмета как у существующих ружей. Серверный NetworkHandMortarBall: скорость22м/с, радиус.09м, отскоки .6/.8 относительно движущейся палубы, подводное сопротивление, взрыв через2.5с или при прямом попадании в живую цель. Радиус4м, прямой урон100, splash100 с линейным спадом; урон только игрокам и ботам с NetworkPlayer, штатные командные фильтры CombatHealth. Корабли и остальные объекты не получают урона или импульса от взрыва; отскоки от них сохранены. Настройки HandMortar.asset; модель, pickup, ball, icon, реестр, каталог, ChestLoot вес5 и F8 подключены; тестовая палуба Loot_HandMortar. Protocol137. Компиляция/ссылки проверяются в редакторе, Play/онлайн не запускались.
ExplosiveBall=28: отдельное ручное взрывное ядро из предоставленного FBX, исходный PBR, диаметр24см. ЛКМ сразу бросает и расходует один предмет; до броска фитиль не горит, после броска сокращается по серверной SyncVar, имеет оранжевый огонек и редкие искры. Полет14м/с, отскок.4, таймер2.5с, взрыв при входе в воду как фугу; прямое касание игрока не сокращает таймер. Урон40 только NetworkPlayer (включая ботов), радиус3м, без спада и с проверкой преград/команд; корабли и объекты без урона/импульса. Общий серверный NetworkHandMortarBall с отдельными ExplosiveBall.asset settings. Pickup и projectile отдельно зарегистрированы, model/icon/F8/DefaultLoot/ChestLoot вес10/тестовая палуба подключены; Protocol138. Компиляция и ссылки проверены; Play/онлайн не запускались.

- [Assets/Scripts/Player/PirateWeapon.cs](<Assets/Scripts/Player/PirateWeapon.cs>) — Исходник C#: IWeaponTarget, PirateWeapon.
- [Assets/Scripts/Networking/NetworkWeapon.cs](<Assets/Scripts/Networking/NetworkWeapon.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Player/FirearmDefinition.cs](<Assets/Scripts/Player/FirearmDefinition.cs>) — Исходник C#: FirearmDefinition, FirearmCombat.
- [Assets/Scripts/Player/FirearmHandling.cs](<Assets/Scripts/Player/FirearmHandling.cs>) — Исходник C#: FirearmHandling.
- [Assets/Scripts/Player/PistolBullet.cs](<Assets/Scripts/Player/PistolBullet.cs>) — Исходник C#: PistolBullet.
- [Assets/Scripts/Cannons/CannonSmokeTrail.cs](<Assets/Scripts/Cannons/CannonSmokeTrail.cs>) — Исходник C#: CannonSmokeTrail.
- [Assets/Scripts/Player/CombatHealth.cs](<Assets/Scripts/Player/CombatHealth.cs>) — Исходник C#: CombatHealth.
- [Assets/Scripts/Networking/NetworkHealth.cs](<Assets/Scripts/Networking/NetworkHealth.cs>) — Исходник C#: NetworkHealth.
- [Assets/Scripts/Editor/FirearmSetup.cs](<Assets/Scripts/Editor/FirearmSetup.cs>) — Исходник C#: FirearmSetup.
- [Assets/Scripts/Player/SabreAnimation.cs](<Assets/Scripts/Player/SabreAnimation.cs>) — Исходник C#: SabreAnimation.
- [Assets/Scripts/Editor/NewPirateAnimationBatch.cs](<Assets/Scripts/Editor/NewPirateAnimationBatch.cs>) — Исходник C#: NewPirateAnimationBatch.
- [Assets/Scripts/Player/FirearmMechanism.cs](<Assets/Scripts/Player/FirearmMechanism.cs>) — Исходник C#: FirearmMechanism.
- [Assets/Scripts/Player/FirearmVfx.cs](<Assets/Scripts/Player/FirearmVfx.cs>) — Исходник C#: FirearmVfx.
- [Assets/Scripts/Editor/FirearmModelReplacementSetup.cs](<Assets/Scripts/Editor/FirearmModelReplacementSetup.cs>) — Исходник C#: FirearmModelReplacementSetup.
- [Assets/Scripts/Networking/ExperimentalShipEquipment.cs](<Assets/Scripts/Networking/ExperimentalShipEquipment.cs>) — Исходник C#: ExperimentalShipEquipment.
- [Assets/Models/Firearms/PistolVisual.prefab](<Assets/Models/Firearms/PistolVisual.prefab>) — Префаб Unity.
- [Assets/Models/Firearms/MusketVisual.prefab](<Assets/Models/Firearms/MusketVisual.prefab>) — Префаб Unity.
- [Assets/Models/Firearms/ShotgunVisual.prefab](<Assets/Models/Firearms/ShotgunVisual.prefab>) — Префаб Unity.
- [Assets/Scripts/Player/PirateWeaponAnimation.cs](<Assets/Scripts/Player/PirateWeaponAnimation.cs>) — Исходник C#: PirateWeapon.
- [Assets/Scripts/Player/SabreWoodHit.cs](<Assets/Scripts/Player/SabreWoodHit.cs>) — Исходник C#: SabreWoodHit, PirateWeapon.
- [Assets/Scripts/Player/SabreWoodImpact.cs](<Assets/Scripts/Player/SabreWoodImpact.cs>) — Исходник C#: SabreWoodImpact.
- [Assets/Scripts/Networking/NetworkWeapon.Sabre.cs](<Assets/Scripts/Networking/NetworkWeapon.Sabre.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Editor/SabreModelReplacementSetup.cs](<Assets/Scripts/Editor/SabreModelReplacementSetup.cs>) — Исходник C#: SabreModelReplacementSetup.
- [Assets/Models/Sabre/SabreVisual.prefab](<Assets/Models/Sabre/SabreVisual.prefab>) — Префаб Unity.
- [Assets/Models/Sabre/SabreAssembly.fbx](<Assets/Models/Sabre/SabreAssembly.fbx>) — Модель / анимации FBX.
- [Assets/Prefabs/Networking/DroppedSabre.prefab](<Assets/Prefabs/Networking/DroppedSabre.prefab>) — Префаб Unity.
- [Assets/Resources/SabreCut.shader](<Assets/Resources/SabreCut.shader>) — Шейдер.
- [Assets/Resources/SabreWoodChip.shader](<Assets/Resources/SabreWoodChip.shader>) — Шейдер.
- [Assets/Resources/SabreCut.mat](<Assets/Resources/SabreCut.mat>) — Материал Unity.
- [Assets/Resources/SabreWoodChip.mat](<Assets/Resources/SabreWoodChip.mat>) — Материал Unity.
- [Art/Blender/SabreReplacement/ImportSabre.py](<Art/Blender/SabreReplacement/ImportSabre.py>) — Инструмент Python.
- [Art/Blender/SabreReplacement/SabreAssembly.blend](<Art/Blender/SabreReplacement/SabreAssembly.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Player/CharacterActions.cs](<Assets/Scripts/Player/CharacterActions.cs>) — Исходник C#: CharacterActions.
- [Assets/Scripts/Player/WeaponArmRig.cs](<Assets/Scripts/Player/WeaponArmRig.cs>) — Исходник C#: WeaponArmRig, Arm.
- [Assets/Scripts/Networking/NetworkEquipment.cs](<Assets/Scripts/Networking/NetworkEquipment.cs>) — Исходник C#: NetworkEquipment.
- [Assets/Scripts/Player/BulletSurface.cs](<Assets/Scripts/Player/BulletSurface.cs>) — Исходник C#: BulletSurfaceKind, BulletSurface.
- [Assets/Scripts/Player/FirearmImpact.cs](<Assets/Scripts/Player/FirearmImpact.cs>) — Исходник C#: FirearmImpact.
- [Assets/Scripts/Player/FirearmTrace.cs](<Assets/Scripts/Player/FirearmTrace.cs>) — Исходник C#: FirearmSettings, FirearmShot, FirearmTrace.
- [Assets/Scripts/Player/FirearmPrediction.cs](<Assets/Scripts/Player/FirearmPrediction.cs>) — Исходник C#: FirearmPrediction.
- [Assets/Scripts/Player/UnderwaterFirearmProjectile.cs](<Assets/Scripts/Player/UnderwaterFirearmProjectile.cs>) — Исходник C#: FirearmDamageBatch, UnderwaterFirearmProjectile.
- [Assets/Scripts/World/ProjectileWaterFlight.cs](<Assets/Scripts/World/ProjectileWaterFlight.cs>) — Исходник C#: ProjectileWaterFlight.
- [Assets/Scripts/World/UnderwaterProjectileTrail.cs](<Assets/Scripts/World/UnderwaterProjectileTrail.cs>) — Исходник C#: UnderwaterProjectileTrail.
- [Assets/Scripts/Player/HandMortarSettings.cs](<Assets/Scripts/Player/HandMortarSettings.cs>) — Исходник C#: HandMortarSettings.
- [Assets/Scripts/Player/HandMortarVisual.cs](<Assets/Scripts/Player/HandMortarVisual.cs>) — Исходник C#: HandMortarVisual.
- [Assets/Scripts/Networking/NetworkEquipment.HandMortar.cs](<Assets/Scripts/Networking/NetworkEquipment.HandMortar.cs>) — Исходник C#: NetworkEquipment.
- [Assets/Scripts/Networking/NetworkHandMortarBall.cs](<Assets/Scripts/Networking/NetworkHandMortarBall.cs>) — Исходник C#: NetworkHandMortarBall.
- [Assets/Scripts/Editor/HandMortarSetup.cs](<Assets/Scripts/Editor/HandMortarSetup.cs>) — Исходник C#: HandMortarSetup.
- [Assets/Settings/Weapons/HandMortar.asset](<Assets/Settings/Weapons/HandMortar.asset>) — Настройки или данные Unity.
- [Assets/Models/HandMortar/HandMortarAssembly.fbx](<Assets/Models/HandMortar/HandMortarAssembly.fbx>) — Модель / анимации FBX.
- [Assets/Models/HandMortar/HandMortarVisual.prefab](<Assets/Models/HandMortar/HandMortarVisual.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/HandMortarPickup.prefab](<Assets/Prefabs/Networking/HandMortarPickup.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/HandMortarBall.prefab](<Assets/Prefabs/Networking/HandMortarBall.prefab>) — Префаб Unity.
- [Art/Blender/HandMortar/HandMortarAssembly.blend](<Art/Blender/HandMortar/HandMortarAssembly.blend>) — Редактируемая сцена Blender.
- [Assets/UI/Inventory/HandMortar.png](<Assets/UI/Inventory/HandMortar.png>) — Изображение / текстура.
- [Assets/Scripts/Networking/NetworkWeapon.ExplosiveBall.cs](<Assets/Scripts/Networking/NetworkWeapon.ExplosiveBall.cs>) — Исходник C#: NetworkEquipment, NetworkWeapon.
- [Assets/Scripts/Player/ExplosiveBallFuse.cs](<Assets/Scripts/Player/ExplosiveBallFuse.cs>) — Исходник C#: ExplosiveBallFuse.
- [Assets/Scripts/Editor/ExplosiveBallSetup.cs](<Assets/Scripts/Editor/ExplosiveBallSetup.cs>) — Исходник C#: ExplosiveBallSetup.
- [Assets/Settings/Weapons/ExplosiveBall.asset](<Assets/Settings/Weapons/ExplosiveBall.asset>) — Настройки или данные Unity.
- [Assets/Models/ExplosiveBall/ExplosiveBall.fbx](<Assets/Models/ExplosiveBall/ExplosiveBall.fbx>) — Модель / анимации FBX.
- [Assets/Models/ExplosiveBall/ExplosiveBallVisual.prefab](<Assets/Models/ExplosiveBall/ExplosiveBallVisual.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/ExplosiveBallPickup.prefab](<Assets/Prefabs/Networking/ExplosiveBallPickup.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/ExplosiveBallProjectile.prefab](<Assets/Prefabs/Networking/ExplosiveBallProjectile.prefab>) — Префаб Unity.
- [Art/Blender/ExplosiveBall/ExplosiveBall.blend](<Art/Blender/ExplosiveBall/ExplosiveBall.blend>) — Редактируемая сцена Blender.
- [Assets/UI/Inventory/ExplosiveBall.png](<Assets/UI/Inventory/ExplosiveBall.png>) — Изображение / текстура.
- [firearm-foundation.md](<firearm-foundation.md>) — Документация.
- [combat-balance.md](<combat-balance.md>) — Документация.
- [Art/Blender/Firearms/README.md](<Art/Blender/Firearms/README.md>) — Документация.
- [Art/Blender/SabreReplacement/README.md](<Art/Blender/SabreReplacement/README.md>) — Документация.

### Предметы, лут и инвентарь (`loot`)

Ключевые слова: лут, предметы, инвентарь, inventory.

Подбор и расход предметов подтверждает сервер. Сверять идентификаторы предметов, иконки и каталог. Одинаковые предметы складываются в стак без игрового лимита количества; ядра сохраняют отдельный слот на 2 ядра одного типа. NetworkWeapon.stackCounts хранит количество оружия/снаряжения; рыба и ром используют прежние счётчики без лимитов 20/6. Расход, сброс, установка пушек и TransferInventoryTo сохраняют остаток стака. DeveloperMenu содержит ползунок скорости корабля 100–500%: команда 20 применяет к текущему кораблю под игроком либо его собственному. Сервер меняет NetworkShip.DeveloperSpeedMultiplier (SyncVar); ShipController масштабирует максимальную скорость и разгон/торможение, сохраняя ограничения якоря, повреждений, затопления и буксировки. DeveloperMenu содержит выпадающий список пяти морских ивентов и спаун перед текущим/собственным кораблём через команду 23 NetworkDeveloperTools. Сервер проверяет свободную воду, наполняет сундук из ChestLoot.json и учитывает его при удалении тестовых объектов. Надписи морских лутовых ивентов скрыты по умолчанию; локальный переключатель DeveloperMenu показывает все доступные клиенту ивенты без ограничения расстояния. Чит-окно F8 разделено на вкладки Игрок, Лут, Корабль и Мир; выдача предметов, все улучшения и морские ивенты выбираются компактными выпадающими списками с текстовым поиском. Каталог улучшений кэшируется при первом открытии окна. Команда 21 создаёт наполненный сундук в 3 м перед игроком на свободной земле или палубе, закрепляя его на корабле; команда 22 выдаёт выбранное улучшение через серверное состояние и штатные эффекты, без расходования очков и повторной выдачи уже полученного. Оба спауна учитывают лимит и удаление тестовых объектов. Команда 19 оставлена переключателю кракена.
NetworkWeapon имеет отдельные partial-файлы; для морского лута начать с NetworkWeapon.SeaLoot.cs.
Состав сундуков и число разных типов (до 10) задаёт Assets/StreamingAssets/Loot/ChestLoot.json; любой новый лут сундуков обязательно подключать туда. Сервер перечитывает таблицу при наполнении; инструкция рядом в ChestLoot.README.md.
Плот движется на сервере со скоростью 20% MaxSpeed корабля, поворачивает внутрь за 150 м от границы зоны; ограничение радиуса оставляет 108 м от центра плота до границы при сужении. RaftPlatform и ShipDeckPassenger переносят игрока, сетевой Platform ссылается на сундук. ShipSpyglassView.LootHint создаёт один полупрозрачный золотистый столб света с сечением 1×1 м и высотой 300 м над ближайшим активным морским ивентом из ClientChests. Выбор по расстоянию от игрока не зависит от направления взгляда; переносимые, пустые и закреплённые на корабле сундуки исключаются. LootEventBeam.shader использует мягкие края, затухание к вершине и depth test. Render callbacks показывают столб только камере подзорной трубы, на выходе скрывают. Экранное пятно удалено. В обеих подзорных трубах туман сохраняется первую секунду, затем за 3 с SmoothStep ослабляет его плотность до 1% исходной. ShipSpyglassView.FogMultiplier применяется к обычному и SeaMist туману только камеры трубы; выход восстанавливает значения. Экранный взлом: мышь/A/D задают угол, ЛКМ/пробел вращают замок, три отмычки на попытку; секретный угол и успех проверяет сервер.
Подводный сундук: только сундук и верёвка от дна с витками вокруг корпуса. E схватывает верёвку; сервер считает реальное плавание вокруг сундука, по умолчанию два круга (LootCatalog.SunkenUnwrapTurns). Обратное движение наматывает обратно; E/Q/Esc отпускают с сохранением прогресса. После полного разматывания сундук всплывает. Боты плывут по орбите, прежние буй и три крепления удалены. Две процедурные чайки с взмахами крыльев кружат на высоте 10–12 м над водой по радиусам 6–8 м до завершения всплытия, обозначая место сундука.
VortexBottle=23 — бутылка с вращающимся вихрем. ЛКМ бросает через NetworkEquipment и NetworkWeapon.VortexBottle. NetworkVortexBottle считает полёт и столкновения на сервере; удар по NetworkShip даёт 500% обычной скорости на 10 с, повторный удар обновляет таймер без сложения. NetworkShip.VortexBoost хранит SyncVar, ShipController за 0.35 с разгоняет до 500% базовой максимальной скорости самостоятельной тягой, включая старт с нуля, закрытые паруса и опущенный якорь. На время эффекта обычные ограничения тяги и буксировки не задают скорость, якорь перемещается вместе с кораблём через anchorSeabedPoint; после 10 с возвращаются обычные правила, опущенный якорь останавливает корабль в новой точке. DeveloperSpeedMultiplier сохраняется. VortexBottleSetup регистрирует pickup/model/icon/DefaultLoot и тестовую палубу. ChestLoot.json содержит отдельный настраиваемый entry. VortexBottleVisual создаёт локальный объём шести дымчатых потоков внутри прозрачной бутылки при Awake.
FogBottle=24 — бутылка тумана, бросок по ЛКМ через NetworkWeapon.FogBottle и NetworkEquipment. NetworkFogBottle считает попадание на сервере, включая геометрию и первую точку пересечения CPU-поверхности воды. При разбитии создаёт отдельный сетевой FogCloud в мировой точке удара. NetworkFogCloud живёт 15 с, синхронизирует возраст каждые 0.2 с и даёт клиентам плавное время, включая позднее появление. FogCloudVisual/BottleFog.shader рисуют объём эллипсоида радиусом 50 м и высотой 44 м, цвет .23/.28/.29, плотность .18; raymarch с depth clipping работает снаружи и изнутри, независимо от обычного тумана, читов и подзорной трубы. Облако разворачивается за .55 с и исчезает в последние 1.1 с. FogBottleVisual использует тот же эффект внутри стеклянной бутылки с отдельными локальными осями, плотностью 10 и очередью 3000 перед стеклом. FogBottleSetup сохраняет модель/pickup/cloud, иконку, каталог, сетевой реестр, тестовую палубу; ChestLoot.json имеет отдельную настраиваемую строку без изменения прежних настроек. ProtocolVersion=108.
Огненный череп — пятый морской ивент. NetworkSkullEvent вращает платформу и череп 8°/с; сервер принимает первое фронтальное попадание ядром в увеличенный объём рта 25.2×13.68×20 м, включая зубы и края, через SkullMouthTarget или коллайдер черепа, гасит огонь и фиксирует угол. Через 3 с создаёт наш сундук из ChestLoot.json и запускает дугой 2.5 с на свободную поверхность палубы корабля стрелявшего. TryFindRewardDeckPoint использует коллайдеры палубы, включая секции разрушения; траектория следует за локальной точкой корабля через существующие support/anchor, после посадки сундук остаётся закреплён на корабле. При отсутствии пригодной палубы или потере корабля награда падает в воду. Платформа остаётся потушенной. SkullFireVfx: девять ParticleSystem, три источника света, URP SkullFire; затухание .55 с, late join восстанавливает состояние. SeaLootSpawner, F8 и подсказка подзорной трубы поддерживают ивент. SkullEventSetup сохраняет префаб, каталог и FishNet registry. ProtocolVersion=109; игровая и сетевая приёмка выполняется пользователем. После правки размеры: череп высотой 54 м, платформа шириной 96 м; текущий масштаб ивента ×2 (последний размер уменьшен в 1.5 раза) через RotatingRoot префаба, исходная модель FBX/BLEND сохраняет авторский масштаб; посадка вычислена по контакту нижней поверхности черепа с местной поверхностью платформы. Позиция ивента фиксирована как у острова, Rigidbody FreezeAll. Сокеты, область рта и пространственные параметры огня увеличены ×3; радиус платформы 50 м учитывается при спауне и выбросе сундука. ParticleSystemScalingMode.Hierarchy применяет масштаб иерархии один раз; дальность света и SoftDistance увеличены отдельно. Сундук остаётся обычного размера.
Заменены модели разобранной пушки, обычного/огненного/ледяного/толкающего ядра, рома, крюка-кошки и святой гранаты: Assets/Models/Loot/Replacement. Обновлены лут, оборудование в руках, снаряды, запас основного корабля и восемь иконок; прежние GUID, сетевые предметы и механики сохранены. ShipV3Test имеет RumShelf на четырёх авторских полках: 12 бутылок общего NetworkShip.RumCount. ExperimentalShipEquipment.SpawnPoints размещает восемь типов на палубе нового корабля; Items задаёт сетевой тип особых ядер до Spawn. Повторный полный импорт ShipV3 сохраняет это размещение через ConfigureImportedShip. Игровая и сетевая приёмка остаются пользователю.
На новом ShipV3Test обычное ядро исключено из ExperimentalShipEquipment: осталось семь палубных предметов. Обычные ядра выдаёт ShipV3Features.Dispense из маски в трюме, до шести свободных экземпляров с прежним интервалом 4 с. ConfigureDispenserBindings привязывает рот к V15_Cannonball_Spawn со смещением внутрь и зазором по радиусу ядра; MeshCollider модели сохраняет физическую поверхность языка. Основной корабль и его источник ядер не изменены.
BoardingEquipmentSetup заменяет три модели из Blender/Лутабельные/New: абордажный снаряд, Wine и Spyglass. Прежние игровые префабы, GUID, механики и ссылки инвентаря сохранены. BoardingHookVisual содержит одиночный гарпун; BoardingHarpoonPairVisual — две копии с верёвкой, индекс 5 в Cannonball.AmmoModels. Обновлены три предметные иконки и запасы обоих кораблей. Модели и PBR-материалы находятся в Assets/Models/Loot/Replacement.
На ShipV3Test к семи прежним предметам добавлены BoardingHook, Wine и Spyglass: всего десять маркеров на настоящей палубе. ExperimentalShipEquipment сохраняет авторитетный серверный спавн и цикл повторного появления; обычные ядра по-прежнему выдаёт маска. BoardingHookAmmo и CannonHands показывают парный снаряд ещё до загрузки в пушку.
FogBottle и VortexBottle используют пустую Whisky Bottle: дым ограничен внутренним объёмом, вихрь состоит из мягких вращающихся лент. BottleFog сохраняет прежний большой игровой туман отдельной веткой InsideBottle; BottleVortex — шейдер внутреннего вихря. ExperimentalShipEquipment содержит две дополнительные точки на центральной палубе ShipV3Test: (-1.1,4.11,0.35) и (1.1,4.11,0.35). После подбора или разбития бутылка восстанавливается через 5 секунд в своей точке; остальные предметы сохраняют прежние интервалы.
Исправления 2026-10-04: сервер подбора проверяет доступную поверхность коллайдера предмета, чтобы origin набора пушки под палубой не создавал ложную преграду. VortexBottleVisual рисует локальный объём шести дымчатых потоков с пустым центром и неоднородным движением. BottleFog меняет только InsideBottle: восходящие клубы, domain warp, внутреннее затенение, плотность 10. Игровой FogCloud сохраняет прежнюю ветку; стекло имеет очередь 3010, эффекты — 3000.
Полка рома принимает точку наведения и слот в одном RPC, с проверкой поверхности и прямой видимости. Бутылочные VFX используют устойчивую базовую плотность и текущий render-transform: Fog Density22, Vortex Density14/Rotation90. Рыба-меч направлена носом +Z, хват перенесён к телу; фугу перед взрывом раздувается поперёк тела до ~2.1x, обрабатываются все Renderer. Игровая проверка остаётся за пользователем.
FogBottle и VortexBottle на палубе и в полёте разбиваются выстрелом или проверенным контактом сабли через IWeaponTarget/TryBreakFromWeapon. Общий серверный Break защищён от повторов, запускает существующий FogCloud или VortexBoost до Despawn и рассылает один BottleBreakVfx со звуком стекла и осколками. Для палубной бутылки вихря NetworkFish.SupportingShip определяет корабль-носитель; стрелявший корабль не подставляется. Вода сохраняет прежнее поведение. BottleBreakVfx использует пул 16 коротких частиц без Rigidbody и света. Внутренний BottleVortex имеет синий HDR-цвет (0.05,0.38,1,0.85) и EmissionStrength 3.5 без ограничения saturate; плотность 14, форма, глубина и RotationSpeed 90 сохранены. Повторный ConfigureBottles сохраняет новые настройки. Игровая и сетевая приёмка остаются за пользователем.
ExperimentalShipEquipment отсчитывает индивидуальную серверную задержку 5 секунд для FogBottle и VortexBottle с момента исчезновения предыдущего экземпляра после подбора или разбития. Первый запас появляется сразу; повторный спавн использует прежний SpawnPoint корабля и NetworkFish.Place. Таймер не зависит от общего 20-секундного пополнения; мгновенная выдача снайперки/дробовика сохранена.
Баррикада: палубный пикап ExperimentalShipEquipment появляется повторно сразу после подбора. Установка на свободной поверхности корабля — удерживать ЛКМ и смотреть в радиус 0.38 м от закреплённой точки 3 секунды; сервер проверяет непрерывные сообщения каждые 0.12 с, сбрасывает при перерыве более 0.5 с. R/колесо поворачивают, ПКМ отменяет. Предпросмотр по прогрессу переходит из зелёного в исходную текстуру снизу вверх в локальной высоте. Shift+E 7 секунд собирает установленную баррикаду в инвентарь. Оружие игроков не повреждает; прямое обычное ядро уничтожает целиком с BarricadeBreak и 16 косметическими фрагментами. Исходник Art/Blender/Barricade/Barricade.blend, размер 1.4×0.68×2.1 м, проём свободен в 1.42–1.69 м. Протокол 122; Play Mode и мультиплеер не проверены.
Lantern=26: ручной фонарь из корабельного Body без Hanger, Y-up и исходный scale100 запечены вместе с коэффициентом .65; высота .56м. ExperimentalShipEquipment создаёт один на палубе Loot_Lantern (1.1,4.11,-1.3), повтор через обычные20с. StackSlot всегда EmptySlot:1фонарь=1слот. NetworkEquipment.Lantern хранит отдельный lit-bit каждого слота; E переключает при свободном вводе, подбор/взаимодействие имеют приоритет. NetworkLantern переносит состояние через drop/pickup, G кладёт предмет. World visual даёт единственный свет7.2м, view visual только модель; HandLanternVisual плавно качает корпус вокруг ручки по gravity/acceleration, ограничение24градуса. Подключены DropPrefabs[26], Models[13], LoosePrefabs[26], icon[26], сетевой реестр и Protocol125; ChestLoot.json строка Lantern с шансом0. HandLanternSetup.Configure сохраняет все привязки, полный импорт сохраняет палубный маркер. Компиляция без ошибок; игру/онлайн проверяет пользователь.
Процедурные варианты фактуры баррикады отменены по новой жалобе пользователя: актуальный путь — исходный PBR и BarricadeAtlasSetup (см. ниже). Ручной фонарь: в первом лице единственный свет у видимой модели, в третьем/чужом виде у мировой; слабое колебание яркости, без самозатенения металлическим корпусом. Выбор общей тени использует гистерезис.
Сундук 2026-10-06: предоставленные ZIP основы и крышки импортированы через Unity MCP в Assets/Models/Loot/Chest. ChestModelSetup.Configure повторяемо сохраняет новые модели, URP материалы с basecolor/normal и упакованным metallic + smoothness, исходный GUID NetworkLootChest.prefab и ссылки каталога сохранены. Основа шириной 1.2 м, крышка 1.23 м с задней петлёй; NetworkLootChest плавно открывает её на 105° за 1.1 с и воспроизводит пространственный ChestLidCreak (ShipCreakA) один раз при открытии. Поздний клиент сразу видит текущее положение крышки. F8: команда 21 создаёт сундук с Fill/ChestLootTable по общим правилам POI на свободном участке носовой палубы текущего/собственного корабля; проверяет опору под углами и свободный объём, PlaceOnDeck использует существующие support/anchor/facing, сундук следует кораблю, переносится обычным способом и учитывается среди удаляемых тестовых объектов. Игра не запускалась.
Предыдущую замену древесины шумом пользователь отклонил; актуальные исходные PBR-карты и UV описаны ниже.
Фикс F8 сундука 2026-10-06 по жалобе: прежний bowLimit вычислялся по всему ShipV3CollisionBatch с выступами. Native чтение сохранённого корабля: actualBow=19.75, combinedBow=26.24579, старый bowLimit=20.74579 — впереди любой палубы. TryFindDeckPoint теперь собирает реальные палубные источники по явному SectionFor/Type Deck, исключает общий batch из расчёта bounds; bounds выключенных MeshCollider Sources берёт из sharedMesh и матрицы. TryDeckHit делает raycast по действующей физике и проверяет точную секцию попадания через batch.Resolve. Этим же путём проверяются четыре угла опоры. Свободный объём, Fill/ChestLootTable, PlaceOnDeck и сеть сохранены. Компиляция без ошибок, новый TryDeckHit загружен в Unity; игровой спавн не запускался.
Баррикада 2026-10-06, новый фикс по исходной модели: материал URP Lit с оригинальными BarricadeBaseColor/Normal/MetalSmooth, maxTextureSize4096 и BumpScale1. BarricadeWood.hlsl больше не заменяет albedo/normal/smoothness; строительный preview использует исходный PBR. BarricadeAtlasSetup сохраняет отдельные копии meshes в BarricadeAtlas.asset, выполняет положительно взвешенную cotangent UV relaxation внутри островов по уже деформированной геометрии; границы закреплены, перевёрнутые острова откатываются. Полная карта MetalSmooth4096 и консервативный max-pool закрепляют вершины и треугольники с металлическими деталями без усреднения мелких гвоздей. Исправлены 568 внутренних UV вершин, UV перенесены на 3617 вершин фрагментов в физических координатах импортного FBX; геометрия/силуэт 1.4×0.68×2.1 неизменны. Оригинальные FBX/текстуры/GUID сохранены. Игровой вид не проверялся.
Поворот при строительстве 2026-10-06: PlayerInventory.RotatePlacement общий для пушки и баррикады, колёсико в обе стороны по5° за нормализованный шаг. Исправлено ошибочное повторное деление на120: установлен InputSystem1.20/UniformAcrossAllPlatforms. Деление Windows120 выполняется только при KeepPlatformSpecificInputRange. Вращение наR удалено, подсказки обновлены, zoom третьего лица блокируется через PlacementActive. Компиляция без ошибок; игровой ввод не проверялся.
FogBottle/VortexBottle: удержание ЛКМ показывает штатную траекторию гранаты, отпускание бросает. NetworkWeapon.GetBottleLaunch и NetworkFogBottle.Advance общие для предпросмотра и серверного полета: скорость корабля, гравитация, радиус0.12м, геометрия и поверхность воды. Переключение предмета, меню и корабельное взаимодействие отменяют прицел.
Пинок сдвигает свободный лут через серверный swept-box шаг, гравитацию и скольжение; положение остаётся ship-local до падения с палубы. Удерживаемый/несомый/летящий лут исключён. Свободные ядра получают импульс через существующую физику Cannonball.

- [Assets/Scripts/Player/PlayerInventory.cs](<Assets/Scripts/Player/PlayerInventory.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Player/PlayerInventory.RaftLockpick.cs](<Assets/Scripts/Player/PlayerInventory.RaftLockpick.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Player/DeveloperMenu.cs](<Assets/Scripts/Player/DeveloperMenu.cs>) — Исходник C#: DeveloperMenu.
- [Assets/Scripts/Networking/NetworkDeveloperTools.cs](<Assets/Scripts/Networking/NetworkDeveloperTools.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/World/SeaLootSpawner.cs](<Assets/Scripts/World/SeaLootSpawner.cs>) — Исходник C#: SeaLootSpawner.
- [Assets/Scripts/Loot/LootCatalog.cs](<Assets/Scripts/Loot/LootCatalog.cs>) — Исходник C#: LootCatalog, Entry.
- [Assets/Scripts/Loot/ChestLootTable.cs](<Assets/Scripts/Loot/ChestLootTable.cs>) — Исходник C#: ChestLootStack, ChestLootTable, Table, ChestSettings, Entry.
- [Assets/StreamingAssets/Loot/ChestLoot.json](<Assets/StreamingAssets/Loot/ChestLoot.json>) — Конфигурация / данные JSON.
- [Assets/StreamingAssets/Loot/ChestLoot.README.md](<Assets/StreamingAssets/Loot/ChestLoot.README.md>) — Документация.
- [Assets/Scripts/Loot/InventoryIcons.cs](<Assets/Scripts/Loot/InventoryIcons.cs>) — Исходник C#: InventoryIcons.
- [Assets/Scripts/Networking/NetworkWeapon.cs](<Assets/Scripts/Networking/NetworkWeapon.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkWeapon.SeaLoot.cs](<Assets/Scripts/Networking/NetworkWeapon.SeaLoot.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkLootChest.cs](<Assets/Scripts/Networking/NetworkLootChest.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.Ocean.cs](<Assets/Scripts/Networking/NetworkLootChest.Ocean.cs>) — Исходник C#: SeaLootKind, SeaLootState, NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.OceanVisual.cs](<Assets/Scripts/Networking/NetworkLootChest.OceanVisual.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.Raft.cs](<Assets/Scripts/Networking/NetworkLootChest.Raft.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.Sunken.cs](<Assets/Scripts/Networking/NetworkLootChest.Sunken.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.Seagulls.cs](<Assets/Scripts/Networking/NetworkLootChest.Seagulls.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkLootChest.ObjectivePresentation.cs](<Assets/Scripts/Networking/NetworkLootChest.ObjectivePresentation.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Networking/NetworkWeapon.Roster.cs](<Assets/Scripts/Networking/NetworkWeapon.Roster.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/RaftPlatform.cs](<Assets/Scripts/Networking/RaftPlatform.cs>) — Исходник C#: RaftPlatform.
- [Assets/Scripts/ShipDeckPassenger.cs](<Assets/Scripts/ShipDeckPassenger.cs>) — Исходник C#: ShipDeckPassenger.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Settings/Loot/DefaultLoot.asset](<Assets/Settings/Loot/DefaultLoot.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/LootSetup.cs](<Assets/Scripts/Editor/LootSetup.cs>) — Исходник C#: LootSetup.
- [Assets/Scripts/Player/ShipSpyglassView.cs](<Assets/Scripts/Player/ShipSpyglassView.cs>) — Исходник C#: ShipSpyglassView.
- [Assets/Scripts/Player/ShipSpyglassView.LootHint.cs](<Assets/Scripts/Player/ShipSpyglassView.LootHint.cs>) — Исходник C#: ShipSpyglassView.
- [Assets/Resources/LootEventBeam.shader](<Assets/Resources/LootEventBeam.shader>) — Шейдер.
- [Assets/Scripts/Editor/SpyglassSetup.cs](<Assets/Scripts/Editor/SpyglassSetup.cs>) — Исходник C#: SpyglassSetup.
- [Assets/Scripts/Networking/NetworkVortexBottle.cs](<Assets/Scripts/Networking/NetworkVortexBottle.cs>) — Исходник C#: NetworkVortexBottle.
- [Assets/Scripts/Networking/NetworkWeapon.VortexBottle.cs](<Assets/Scripts/Networking/NetworkWeapon.VortexBottle.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkShip.VortexBoost.cs](<Assets/Scripts/Networking/NetworkShip.VortexBoost.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Loot/VortexBottleVisual.cs](<Assets/Scripts/Loot/VortexBottleVisual.cs>) — Исходник C#: VortexBottleVisual.
- [Assets/Scripts/Editor/VortexBottleSetup.cs](<Assets/Scripts/Editor/VortexBottleSetup.cs>) — Исходник C#: VortexBottleSetup.
- [Assets/Prefabs/Loot/VortexBottle.prefab](<Assets/Prefabs/Loot/VortexBottle.prefab>) — Префаб Unity.
- [Assets/Prefabs/Loot/VortexBottlePickup.prefab](<Assets/Prefabs/Loot/VortexBottlePickup.prefab>) — Префаб Unity.
- [Assets/Scripts/Networking/NetworkFogBottle.cs](<Assets/Scripts/Networking/NetworkFogBottle.cs>) — Исходник C#: NetworkFogBottle.
- [Assets/Scripts/Networking/NetworkFogCloud.cs](<Assets/Scripts/Networking/NetworkFogCloud.cs>) — Исходник C#: NetworkFogCloud.
- [Assets/Scripts/Networking/NetworkWeapon.FogBottle.cs](<Assets/Scripts/Networking/NetworkWeapon.FogBottle.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Loot/FogBottleVisual.cs](<Assets/Scripts/Loot/FogBottleVisual.cs>) — Исходник C#: FogBottleVisual.
- [Assets/Scripts/Loot/FogCloudVisual.cs](<Assets/Scripts/Loot/FogCloudVisual.cs>) — Исходник C#: FogCloudVisual.
- [Assets/Scripts/Editor/FogBottleSetup.cs](<Assets/Scripts/Editor/FogBottleSetup.cs>) — Исходник C#: FogBottleSetup.
- [Assets/Resources/BottleFog.shader](<Assets/Resources/BottleFog.shader>) — Шейдер.
- [Assets/Prefabs/Loot/FogBottle.prefab](<Assets/Prefabs/Loot/FogBottle.prefab>) — Префаб Unity.
- [Assets/Prefabs/Loot/FogBottlePickup.prefab](<Assets/Prefabs/Loot/FogBottlePickup.prefab>) — Префаб Unity.
- [Assets/Prefabs/Loot/FogCloud.prefab](<Assets/Prefabs/Loot/FogCloud.prefab>) — Префаб Unity.
- [Assets/Scripts/Networking/NetworkSkullEvent.cs](<Assets/Scripts/Networking/NetworkSkullEvent.cs>) — Исходник C#: NetworkSkullEvent.
- [Assets/Scripts/Networking/SkullMouthTarget.cs](<Assets/Scripts/Networking/SkullMouthTarget.cs>) — Исходник C#: SkullMouthTarget.
- [Assets/Scripts/Networking/NetworkLootChest.RewardFlight.cs](<Assets/Scripts/Networking/NetworkLootChest.RewardFlight.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Scripts/Loot/SkullFireVfx.cs](<Assets/Scripts/Loot/SkullFireVfx.cs>) — Исходник C#: SkullFireVfx.
- [Assets/Scripts/Editor/SkullEventSetup.cs](<Assets/Scripts/Editor/SkullEventSetup.cs>) — Исходник C#: SkullEventSetup.
- [Assets/Resources/SkullFire.shader](<Assets/Resources/SkullFire.shader>) — Шейдер.
- [Assets/Prefabs/Networking/NetworkSkullEvent.prefab](<Assets/Prefabs/Networking/NetworkSkullEvent.prefab>) — Префаб Unity.
- [Assets/Models/SeaEvents/SkullAltar/SkullAltar.fbx](<Assets/Models/SeaEvents/SkullAltar/SkullAltar.fbx>) — Модель / анимации FBX.
- [Art/Blender/SeaEvents/SkullAltar.blend](<Art/Blender/SeaEvents/SkullAltar.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Editor/LootModelReplacementSetup.cs](<Assets/Scripts/Editor/LootModelReplacementSetup.cs>) — Исходник C#: LootModelReplacementSetup.
- [Assets/Scripts/Networking/ExperimentalShipEquipment.cs](<Assets/Scripts/Networking/ExperimentalShipEquipment.cs>) — Исходник C#: ExperimentalShipEquipment.
- [Assets/Scripts/Loot/RumShelf.cs](<Assets/Scripts/Loot/RumShelf.cs>) — Исходник C#: RumShelf.
- [Art/Blender/LootReplacement/LootReplacement.blend](<Art/Blender/LootReplacement/LootReplacement.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Editor/BoardingEquipmentSetup.cs](<Assets/Scripts/Editor/BoardingEquipmentSetup.cs>) — Исходник C#: BoardingEquipmentSetup.
- [Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonPairVisual.prefab](<Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonPairVisual.prefab>) — Префаб Unity.
- [Assets/Resources/BoardingHookAmmo.prefab](<Assets/Resources/BoardingHookAmmo.prefab>) — Префаб Unity.
- [Assets/Scripts/Editor/BottleFishReplacementSetup.cs](<Assets/Scripts/Editor/BottleFishReplacementSetup.cs>) — Исходник C#: BottleFishReplacementSetup.
- [Assets/Resources/BottleVortex.shader](<Assets/Resources/BottleVortex.shader>) — Шейдер.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Scripts/Networking/NetworkFish.cs](<Assets/Scripts/Networking/NetworkFish.cs>) — Исходник C#: InventoryItem, NetworkFish.
- [Assets/Scripts/Loot/BottleBreakVfx.cs](<Assets/Scripts/Loot/BottleBreakVfx.cs>) — Исходник C#: BottleBreakVfx.
- [Assets/Resources/BottleGlassShard.shader](<Assets/Resources/BottleGlassShard.shader>) — Шейдер.
- [Assets/Resources/BottleGlassShard.mat](<Assets/Resources/BottleGlassShard.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/WhiskyBottle/WhiskySwirl.mat](<Assets/Models/Loot/Replacement/WhiskyBottle/WhiskySwirl.mat>) — Материал Unity.
- [Assets/Scripts/Player/PlayerInventory.Barricade.cs](<Assets/Scripts/Player/PlayerInventory.Barricade.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Networking/NetworkWeapon.Barricade.cs](<Assets/Scripts/Networking/NetworkWeapon.Barricade.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkBarricade.cs](<Assets/Scripts/Networking/NetworkBarricade.cs>) — Исходник C#: NetworkBarricade.
- [Assets/Scripts/Editor/BarricadeSetup.cs](<Assets/Scripts/Editor/BarricadeSetup.cs>) — Исходник C#: BarricadeSetup.
- [Assets/Prefabs/Barricades/Barricade.prefab](<Assets/Prefabs/Barricades/Barricade.prefab>) — Префаб Unity.
- [Assets/Prefabs/Barricades/BarricadePickup.prefab](<Assets/Prefabs/Barricades/BarricadePickup.prefab>) — Префаб Unity.
- [Assets/Models/Barricade/BarricadeVisual.prefab](<Assets/Models/Barricade/BarricadeVisual.prefab>) — Префаб Unity.
- [Assets/Models/Barricade/BarricadeConstruction.mat](<Assets/Models/Barricade/BarricadeConstruction.mat>) — Материал Unity.
- [Assets/Shaders/BarricadeConstruction.shader](<Assets/Shaders/BarricadeConstruction.shader>) — Шейдер.
- [Assets/Scripts/Loot/BarricadeBreakAnimation.cs](<Assets/Scripts/Loot/BarricadeBreakAnimation.cs>) — Исходник C#: BarricadeBreakAnimation.
- [Assets/Scripts/World/WaterImpactPhysics.cs](<Assets/Scripts/World/WaterImpactPhysics.cs>) — Исходник C#: WaterImpactKind, WaterImpactEvent, WaterImpactPhysics.
- [Assets/Scripts/World/WaterImpactBody.cs](<Assets/Scripts/World/WaterImpactBody.cs>) — Исходник C#: WaterImpactBody.
- [Assets/Scripts/Networking/NetworkEquipment.Lantern.cs](<Assets/Scripts/Networking/NetworkEquipment.Lantern.cs>) — Исходник C#: NetworkEquipment.
- [Assets/Scripts/Networking/NetworkLantern.cs](<Assets/Scripts/Networking/NetworkLantern.cs>) — Исходник C#: NetworkLantern.
- [Assets/Scripts/Player/HandLanternVisual.cs](<Assets/Scripts/Player/HandLanternVisual.cs>) — Исходник C#: HandLanternVisual.
- [Assets/Scripts/Editor/HandLanternSetup.cs](<Assets/Scripts/Editor/HandLanternSetup.cs>) — Исходник C#: HandLanternSetup.
- [Assets/Prefabs/Networking/LanternPickup.prefab](<Assets/Prefabs/Networking/LanternPickup.prefab>) — Префаб Unity.
- [Assets/Models/Loot/HandLantern/HandLanternVisual.prefab](<Assets/Models/Loot/HandLantern/HandLanternVisual.prefab>) — Префаб Unity.
- [Assets/Models/Loot/HandLantern/HandLanternBody.asset](<Assets/Models/Loot/HandLantern/HandLanternBody.asset>) — Настройки или данные Unity.
- [Assets/UI/Inventory/Lantern.png](<Assets/UI/Inventory/Lantern.png>) — Изображение / текстура.
- [Assets/Shaders/BarricadeWood.shader](<Assets/Shaders/BarricadeWood.shader>) — Шейдер.
- [Assets/Shaders/BarricadeWood.hlsl](<Assets/Shaders/BarricadeWood.hlsl>) — Код шейдера.
- [Assets/Scripts/Editor/ChestModelSetup.cs](<Assets/Scripts/Editor/ChestModelSetup.cs>) — Исходник C#: ChestModelSetup.
- [Assets/Models/Loot/Chest/ChestBase/tripo_convert_f24c7824-0801-40ca-a4a6-5177deaa3702.fbx](<Assets/Models/Loot/Chest/ChestBase/tripo_convert_f24c7824-0801-40ca-a4a6-5177deaa3702.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Chest/ChestLid/tripo_convert_fc547d5f-a403-4e58-9507-d859a80e5520.fbx](<Assets/Models/Loot/Chest/ChestLid/tripo_convert_fc547d5f-a403-4e58-9507-d859a80e5520.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Chest/ChestBase/ChestBase.mat](<Assets/Models/Loot/Chest/ChestBase/ChestBase.mat>) — Материал Unity.
- [Assets/Models/Loot/Chest/ChestLid/ChestLid.mat](<Assets/Models/Loot/Chest/ChestLid/ChestLid.mat>) — Материал Unity.
- [Assets/Scripts/Editor/BarricadeAtlasSetup.cs](<Assets/Scripts/Editor/BarricadeAtlasSetup.cs>) — Исходник C#: BarricadeAtlasSetup.
- [Assets/Models/Barricade/BarricadeAtlas.asset](<Assets/Models/Barricade/BarricadeAtlas.asset>) — Настройки или данные Unity.
- [Assets/Models/Barricade/Barricade.mat](<Assets/Models/Barricade/Barricade.mat>) — Материал Unity.
- [Assets/Scripts/Networking/NetworkEquipment.BottleAim.cs](<Assets/Scripts/Networking/NetworkEquipment.BottleAim.cs>) — Исходник C#: NetworkEquipment.
- [Assets/Scripts/Networking/NetworkFish.Kick.cs](<Assets/Scripts/Networking/NetworkFish.Kick.cs>) — Исходник C#: NetworkFish.
- [rum-loot.md](<rum-loot.md>) — Документация.
- [combat-balance.md](<combat-balance.md>) — Документация.
- [Art/Blender/LootReplacement/README.md](<Art/Blender/LootReplacement/README.md>) — Документация.

### Повреждения корпуса, ремонт и затопление (`repair`)

Ключевые слова: ремонт, repairing, разрушение, затопление, damage.

Текущая точка входа ремонта — NetworkHullRepair, а не старый путь Scripts/Repair/ShipRepair.cs.
Урон, ремонт фрагментов и затопление связаны с секциями корабля. Старое описание накладных досок не считать актуальной архитектурой.
ShipV3Destruction использует подготовленный граф опор V18 и собственный профиль OrdinaryCannonballsOnly. Разрушение игрового Ship V3 работает от обычных ядер, ремонт использует существующую систему. Профиль архивного старого корабля не менялся.
LazyFragmentColliders включён только для секций Ship V3: из префаба исключены 6275 MeshCollider скрытых фрагментов. При разрушении точный MeshCollider создаётся из исходного MeshFilter.sharedMesh до включения только оставшихся видимых фрагментов; ссылка добавляется в DamageColliders. После ремонта созданные коллайдеры сохраняются на неактивных фрагментах. В сохранённом префабе 2383 ленивые секции, MeshCollider стало 2386 вместо 8661, первоначальных коллайдеров фрагментов нет. В edit-mode preview повреждение одной секции из трёх фрагментов с маской 1 создало только два коллайдера; ремонт выключил их, другие секции остались ленивыми. Флаг основного корабля false; его прежняя ветка сохранена.
DamageAdjacentFragments включён только в ShipV3Destruction. Обычный прямой удар дополнительно снимает один ближайший сохранившийся фрагмент каждой соседней секции по графу Structure.Neighbours. Маски передаются существующими событиями и снимками разрушения; ремонт сохраняет прежний путь. Профиль архивного старого корабля не изменён.
ShipV3CollisionBatch объединяет только неподвижные intact-коллайдеры. CannonShotDamage, ShipDestruction.Resolve и RewardDeckPriority учитывают такие группы; исходные секции, маски соседних фрагментов и механизм ремонта сохранены. ShipDamageSection.VisualChanged уведомляет обе объединённые системы после применения состояния коллайдеров.
Затопление сохраняет серверную механику 60/40/20/10 с по числу незаделанных попаданий и осушение за 30 с. Новые разрушенные фрагменты корпуса проверяются у воды по собственным центрам, включая потерю опоры; исходная высота попадания больше не исключает нижние пробоины. Ремонт очищает маски прежним способом.
Обезьянка использует существующие серверные RepairNearby/RepairMast и общие константы ударов NetworkHullRepair. Темп 15% от игрока; ShipMonkey.Repair.cs выбирает доступные точки своего корабля.
Пожар планируется без изменения геометрии через PlanFire, затем BurnFragment удаляет выбранные реальные фрагменты. Огненные разрушения используют отдельный идентификатор попадания для группировки пробоин и существующий ремонт. OrdinaryCannonballsOnly сохранён: новая ветка допускает только явно спланированное огненное повреждение, остальные особые ядра сохраняют прежние ограничения. Двухударная логика мачт исправлена отдельной общей сборкой (см. актуальную заметку ниже).
Повторное попадание во время ремонта 2026-10-06: native Unity чтение ShipV3Test подтвердило, что все 2346 Sources физических групп находятся вне родителей ShipDamageSection, но имеют однозначные ссылки через DamageColliders. ShipDestruction хранит явные связи Collider/Renderer→секция, обе группы и RepairReveal используют эти связи вместо одного parent lookup. Это обновляет повреждённую физику и убирает невидимые целые поверхности; новые ленивые коллайдеры осколков сразу регистрируются в Resolve для следующего удара. ShipDamageSection.Apply пропускает неизменившиеся state/mask после первого применения, чтобы сетевые снимки не перестраивали весь корабль при каждом ремонте. На ленивых MeshCollider также выключен UseFastMidphase, как на объединённых коллайдерах. Компиляция без ошибок; пользовательский сценарий с двумя игроками не воспроизводился.
Фиксы 2026-10-06 по повторной жалобе: ShipDestruction.Masts объединяет основания, стволы и верхние детали Fore/Main/Mizzen, привязывает реи к ближайшей мачте. Первый прямой удар снимает один фрагмент; соседний урон исключает Mast, Unsupported защищает мачту и её реи до второго попадания. Второй удар разрушает всю сборку. RepairMast восстанавливает её целиком, зелёная точка определяется по реальным bounds у палубы и доступна также после первого удара. Первичное повреждение чинится обычными 3 ударами, полное восстановление — 10; игрок и обезьянка используют общие ключи сборки. RepairNearby выбирает до 3 связанных повреждённых фрагментов одного типа в радиусе 1.5 м через ShipStructuralGraph.RepairCluster. Компиляция без ошибок; игровой и сетевой сценарии не проверены.
PlanFire: минимум 16, максимум 64 очага на выстрел, радиус² 72 сохраняет ранее удвоенную площадь; значения MinimumFireFragments/MaximumFireFragments/FireSurfaceGap задаются профилем. Начало с прямой секции, BFS по живым структурным и ближайшим поверхностям с зазором до 1.5 м; не более 6 пространственных соседей. Для перил приоритет палубе. Пространственный переход запрещён, если его открытый отрезок пересекает bounds удалённого фрагмента; ближайшие точки перехода также ограничены радиусом. Нормаль палубы ship.up, остальных очагов — ближайшая грань bounds; первый очаг сразу, следующие через 0.45–2 с. Общий лимит 128 и правила воды/льда сохранены. Fire mastHits также использует общий ключ сборки, первое горение снимает один фрагмент, повторное обрушает сборку.
Коррекция первого разрушения мачты 2026-10-06: прежние 3 полных поперечных фрагмента оставляли верхнюю часть без древесной опоры. MastFragmentSetup через native Unity подготовил короткие участки высотой до 1.1 м, каждый разделён на 2 половины сечения с закрытыми срезами и SplinterMaterial. Fore30/Main42/Mizzen18 fragments; первый удар снимает одну короткую половину, вторая продолжает держать верх. Intact/геометрия целой мачты сохранены; новый Structure перенумерован вместе с масками, опорными связями и prefab, невалидных ссылок графа нет. Остаются 2 попадания до полного разрушения и ремонт 3/10 ударов. SessionConfig.ProtocolVersion127 из-за новой схемы фрагментов; обоим игрокам нужна актуальная версия. Компиляция без ошибок; игровой сценарий не запускался.

- [Assets/Scripts/Networking/NetworkHullRepair.cs](<Assets/Scripts/Networking/NetworkHullRepair.cs>) — Исходник C#: NetworkHullRepair.
- [Assets/Scripts/ShipDestruction/ShipDestruction.cs](<Assets/Scripts/ShipDestruction/ShipDestruction.cs>) — Исходник C#: ShipSectionSnapshot, ShipDestructionEvent, ShipDestruction.
- [Assets/Scripts/ShipDestruction/ShipDamageSection.cs](<Assets/Scripts/ShipDestruction/ShipDamageSection.cs>) — Исходник C#: ShipDamageSection.
- [Assets/Scripts/ShipDestruction/ShipFlooding.cs](<Assets/Scripts/ShipDestruction/ShipFlooding.cs>) — Исходник C#: ShipBreach, ShipFlooding.
- [Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs](<Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs>) — Исходник C#: ShipSectionState, ShipSectionType, ShipDamageReason, ShipAmmoMultiplier, ShipSectionDefinition, ShipDestructionProfile, ShipFragmentConnection.
- [Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs](<Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs>) — Исходник C#: ShipStructuralGraph.
- [Assets/Scripts/Editor/ShipDestructionSetup.cs](<Assets/Scripts/Editor/ShipDestructionSetup.cs>) — Исходник C#: ShipDestructionSetup, Manifest, Record.
- [Assets/Settings/ShipDestruction/ShipV3Destruction.asset](<Assets/Settings/ShipDestruction/ShipV3Destruction.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Ships/ShipV3Features.cs](<Assets/Scripts/Ships/ShipV3Features.cs>) — Исходник C#: ShipV3TargetKind, ShipV3Lantern, ShipV3DiceSlot, ShipV3PhysicsPose, ShipV3Support, ShipV3Attachment, ShipV3Features.
- [Assets/Scripts/Ships/ShipMonkey.Repair.cs](<Assets/Scripts/Ships/ShipMonkey.Repair.cs>) — Исходник C#: ShipMonkey.
- [Assets/Scripts/ShipDestruction/ShipDestruction.Fire.cs](<Assets/Scripts/ShipDestruction/ShipDestruction.Fire.cs>) — Исходник C#: ShipFireTarget, ShipDestruction, FireSurface.
- [Assets/Scripts/Networking/NetworkShipAmmo.cs](<Assets/Scripts/Networking/NetworkShipAmmo.cs>) — Исходник C#: ShipFirePatch, ShipFreezeState, NetworkShip, FireExposure.
- [Assets/Scripts/Ships/ShipV3CollisionBatch.cs](<Assets/Scripts/Ships/ShipV3CollisionBatch.cs>) — Исходник C#: ShipV3CollisionBatch.
- [Assets/Scripts/Ships/ShipV3RenderBatch.cs](<Assets/Scripts/Ships/ShipV3RenderBatch.cs>) — Исходник C#: ShipV3RenderBatch.
- [Assets/Scripts/ShipDestruction/RepairReveal.cs](<Assets/Scripts/ShipDestruction/RepairReveal.cs>) — Исходник C#: RepairReveal.
- [Assets/Scripts/ShipDestruction/ShipDestruction.Masts.cs](<Assets/Scripts/ShipDestruction/ShipDestruction.Masts.cs>) — Исходник C#: ShipDestruction, MastAssembly.
- [Assets/Scripts/Editor/MastFragmentSetup.cs](<Assets/Scripts/Editor/MastFragmentSetup.cs>) — Исходник C#: MastFragmentSetup, Vertex, Polygon, Replacement.
- [Assets/Models/ShipV3/MastChips/V3_Mast_Fore.asset](<Assets/Models/ShipV3/MastChips/V3_Mast_Fore.asset>) — Настройки или данные Unity.
- [Assets/Models/ShipV3/MastChips/V3_Mast_Main.asset](<Assets/Models/ShipV3/MastChips/V3_Mast_Main.asset>) — Настройки или данные Unity.
- [Assets/Models/ShipV3/MastChips/V3_Mast_Mizzen.asset](<Assets/Models/ShipV3/MastChips/V3_Mast_Mizzen.asset>) — Настройки или данные Unity.
- [ship-destruction.md](<ship-destruction.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Мир, острова и океан (`world`)

Ключевые слова: мир, острова, карта, океан, water.

Референсы универсального Tripo-набора готовы: ../Blender/Скалы содержит 15 папок и 60 PNG спереди.png/сзаде.png/справа.png/слева.png, TRIPO.txt и README.md с бюджетами. Пользователь предоставил все GLB и затем одобрил фактическую арку, разрешив заменить остальные игровые формы в том же стиле.
Пользователь предоставил 15 Tripo GLB, 44860 исходных треугольников, UV и встроенные BaseColor/Normal/MetallicRoughness2K. Одобренная SeaArch_Huge_A_Tripo.blend сохранена без перестройки:32камня76786tris+3Palm/11Fern11444=88230. CoastalTripoCollection.blend содержит все17групп:12скал+5растений. assemble_tripo_collection→refine_collection→refine_cliffwall_b_base→export_tripo_collection. Lagoon shoulders связаны whole-модулями, CliffB опирается на целую Terrace07. Protected water passages проверены worldAABB. Старые NativeModules/CReview не приняты. Blender-кадры и actualUnityматериал арки просмотрены; Play/FPS не измерены.
Согласовывать генерацию карты и её состояние между участниками; seed и профиль брать из используемых ассетов.
CPU-поверхность воды используется игровой логикой: визуальные волны нельзя менять независимо от OceanSurface без проверки связи. OceanSurface добавляет длинные волны 180/120 м; по StormProgress плавно подключаются 90/64 м. Амплитуды всех четырёх длинных волн удвоены (1.1/0.56/0.36/0.24 м);, высота растёт до FinalSwellMultiplier=2.5 и усиливается мелкое волнение. SwellStrength задаёт базовую высоту. Одинаковые параметры _SwellWaves и время используются CPU и активным SimpleWaterURP; ShipController использует27 площадных проб ShipBuoyancy. SessionStorm больше не меняет неиспользуемый активным SimpleWater параметр WaveScale. Старый неиспользуемый Assets/Shaders/Ocean.shader удалён при очистке 2026-10-05.
WorldStructureCollision ревизии 2 учитывает соседний authored _COL, не добавляет дублирующие MeshCollider к визуальным LOD. Для нечитаемого меша без коллайдера используется BoxCollider по bounds. Семь FBX в Assets/Game/Environment импортированы с Read/Write; EnvironmentTestSetup сохраняет этот режим. Ошибки CollisionMeshData у Reef_Moai_A/Reef_Spires_B/C/SeaArch_Huge_A/Reef_ShallowField_A относятся к окружению, не к старому кораблю.
Boat Attack Water: SystemInitializer повторно включает существующие скрытые компоненты после очистки реестра, с Cleanup перед Init; runtime-сброс работает также в Editor. WaterManager восстанавливает уникальную регистрацию океанов и единственную подписку камеры. Исправляет полностью отсутствовавшую воду при загрузке тестового океана после запуска из меню.
WaterPhysics: NativeArrayDispose освобождает четыре поля по ref с default после Dispose; Cleanup сбрасывает JobHandle и managed-кеши. Устраняет чтение уже освобождённого _waterBodyData на первом кадре после повторной инициализации Boat Attack.
Boat Attack: screen UV напрямую World→Clip; глубина поверхности вычисляется из позиции пикселя, без обратной LOD-матрицы. Сине-зелёные absorption(.10,.46,.42)/scattering(.008,.12,.105), шесть волн amplitude2.2/wavelength32м; пена от.60 до.90 с максимумом.70, foamIntensity.40, рябь.32. R500/D120/центр в550м перед кораблём сохранены.
Boat Attack передаёт LOD отдельным NativeArray<float4>/instanced MaterialPropertyBlock; матрицы обычные, батчи по256 с фактическим count. A/B в Play выявил остаточные разрывы от lighting probe/lightmap instancing и cullDistance1000 у камеры: Water.shader использует nolightprobe nolightmap assumeuniformscaling/target4.5; корень воды на слое4, DrawMeshSurface поднимает только его layerCullDistances до farClipPlane. Новый запуск из меню: вода5000м, остальные слои1000м; палуба, море и чаша без дыр на проверенных кадрах.
Дополнительная коррекция Boat Attack после проверки exe: Gerstner peak=2 создавал отрицательный горизонтальный якобиан (на реальных шести волнах det min−.455, 2.20% перевёрнутых точек). В GerstnerWaves.hlsl, GerstnerWaves.cs и BoatAttackOcean.CacheWaves согласованно установлен peak=.85; вертикальная амплитуда сохранена. J=I−(peak/N)Σsin(phase)d dᵀ, поэтому λmin≥.15 для любых направлений/фаз, для текущего спектра≥.28376. Повторная сборка и приёмка завершены, результат ниже.
Основная Тестовая карта запускается через EnvironmentTestGallery внутри NetworkOcean. Resources/EnvironmentTest/Ocean.prefab сохраняет настройки Boat Attack; TestOceanController подключает адаптер к единственному игровому OceanSurface, скрывает прежний MeshRenderer и восстанавливает его в OnDisable до следующей генерации. Ресурс содержит instanced BoatAttackWater.mat для Player.
Стартовая ориентация yaw180 (от галереи в открытое море); центр воронки=Spawn+(0,0,−550), R500/D120/Twist2, ближний край50м. Другие корабли разнесены поперёк по−X65м. Layout.Radius≥2000м обеспечивает доступ ко всей чаше. Глубина дна200м, SeabedTerrain углубляет чашу под тем же центром.
F8: сила волн0..200%, крутизна0..100%, скорость.25..2x и сброс. CPU/GPU вертикаль×strength, горизонталь peak.85×min(strength,1)×steepness; speed меняет непрерывные часы, высота0 не делит на ноль. Волны меняет хост; SessionOcean передаёт параметры и общие clockanchor/value клиентам, включая подключившихся позднее.
TestSky: HDR grading/LUT32, Neutral, Contrast6/Saturation−4, Bloomthreshold1.2/intensity.15/scatter.55/clamp8/Half, SSAOdirectStrength.15. Boat Water near/far используют динамический skyCube; ночью пена×.45/scatter×.65. Прозрачная вода применяет тот же PBSky atmospheric fog в fragment через Lighting.w, без второго fullscreen pass.
TestSkyPipeline зарегистрирован для сбора shader features через IncludeAdditionalRPAssets/includeAssetsByLabel и метку PirateSlopRuntimePipeline. Без этого URP удаляет HDR_GRADING для динамического переключения pipeline: в Player белое небо и резкий контраст, хотя Editor работает. TestSkySetup.ConfigureLook поддерживает регистрацию.
WaterTestCapture: -environmenttest -watercapture включает12GPUкадров в Player, включая нулевые/сильные волны, закат и ночь. StandardRequest использует полный RenderCameraStack с автоматическим Volume update после создания актуального pipeline. Диагностические profile/stack EV сравниваются после рендера; SingleCameraRequest пропускал штатный update.
Динамическая пена Boat Attack: 38 проб измеренной ватерлинии, мировая история контактов2.8с и следа12с; 2048² ARGBHalf/512м хранит гладкую плотность, hull mask и высоту V-гребня≤.22м; CPU и atlas shader используют общую форму, ShipController исключает собственный гребень из качки, фактура берётся из общего FoamMap. Носовые плечи по сечениям14–16 и масса перед форштевнем растут с реальной скоростью относительно воды, пакеты живут1.4с и расходятся1м/с. WaterBowSpray запускается от wet×положительного подъёма воды >1.25м/с; cooldown.6с, gravity1, мировые капли с округлым AA-шейдером возвращаются в воду. F8 содержит локальные косметические ползунки носовой пены и брызг0..200%. Подводный medium определяется по фактической Height с волнами/чашей; UnderwaterRendererFeature копирует актуальную глубину после воды и перед postprocessing применяет один Beer pass, затем редкую взвесь. Воздушные PBSky fog/SeaMist под водой отключены. -waterdetailcapture проверяет близкий борт/след, скорости/удары/ночь, переход среды, чашу и POI5/15/30/60м.
Качка ShipController использует ShipBuoyancy:9 продольных сечений×3 поперечных точки, weighted plane-fit по площади измеренного корпуса. Высота берётся из intercept плоскости в центре корабля с учётом смещённого weighted meanZ; pitch/roll из её уклонов. Четыре края±18/±5.5 раньше давали spatial alias на волне32м и могли менять знак наклона. Сохранены сглаживаниеexp, clamps12/15deg, Flooding/Cannon и сетевой ShipState. Проверены16 аналитических плоскостей при yaw0/90/180/270: height error<7.2e−7, normal dot≥.99999994. Дляsin(2πz/32) oldPitch+1.218deg, new−3.528deg.
WaterImpactPhysics/WaterImpactBody: общие локальные всплески входа в actual OceanSurface для физических тел, игроков без Rigidbody и скриптовых предметов. Swept crossing промежуточных точек/уточнение; ядро передаёт incoming velocity при входе в воду и продолжает подводный полёт. Нормальная скорость, масса и площадь задают капли/пену; человек вверх, ядро по касательной. Dynamic Rigidbody.GetPointVelocity, кинематика по времени фактического перемещения. Rearm после .25м/.25с над поверхностью, спавн под водой и прыжок позиции игрока не создают всплеск. 64 истории splash-ring в общем foam atlas; единые 600капель/с,1200живых,2burst/.1с. Fish/bottle/grenade RPC передают вход до despawn; сетевой ProtocolVersion124. Носовой spray на текущей высоте воды переносится raycast на наружную обшивку с запасом10см. Обычный Build Windows06.10.2026:0 ошибок; Player32кадра,6событий без повторов/исключений; итоговая VFX-приёмка [APPROVED]. Свободный подводный верх без белого потолка, POI5–30м читается;60м частично закрыт настоящим дном. Два сетевых клиента не проверялись.
Брызги 2026-10-06: Drops ×4, 2400 капель/с, 4800 живых, запас720, до4 impact bursts/.1с. Подъём ядер5–18 м/с учитывает нормальную и касательную энергию; радиальный разлёт до7 м/с, жизньдо3.4с, линейное сопротивление. WaterSplashContacts проверяет реальные позиции частиц по seed:256 проб,128 сегментных+128 поверхностных запросов/.033с; первое препятствие блокирует каплю.192 проецируемых мокрых следа на корабле, жизнь3.5с с плавным затуханием; локальные капли на экране только по подтверждённому контакту. Компиляция проверена, внешний вид/игра/онлайн не проверены.
Облака 2026-10-07, фикс после повторной игровой жалобы: удалены volumeShape, smooth union и узнаваемая тройка эллипсоидов. Непрерывный warped weather только размещает редкие группы; сама плотность — remap двух red-выборок линейной Texture3D Worley128 с разными осями/offset и octave2.07, мягкий переменный height-gradient, настоящая erosion и micro erosion. R8 шумы проверены GPU-readback всех слоёв: Worley128³ mean.753/std.118, Perlin32³ mean.500/std.123, оба не-константны/sRGBfalse; G/B/A не используются. samplingNormalization100000, shapeScale6 даёт период16.67км вместо37км. Material coverageStart.54/end.76, cell7000/seed19/farFade12000–28000; profile density.34, shape6/.75, erosion90/.5, microtrue/.28/200, primary64/light6. ShapeLOD<=2/detailLOD<=1 только CLEAR, вертикальные края строго нулевые с защищённым remap denominator. Authoring CPU расчёт43 296 точек:19.29% world columns живые,6.88% точек с промежуточной density, насыщенных0, liveMean.05259/max.16781; это не доля пикселей неба и не игровой рендер. Общие worldOffset/wind, Main/Shadow/Cubemap, fog/солнце/grading сохранены; legacy CandidateA не менялся. Native профиль/материал/keywords сохранены после импорта; shader и console без ошибок, сцена не сохранялась; игровая визуальная приёмка не проводилась.
Подводный след 2026-10-06: UnderwaterProjectileTrail использует Underwater/Bubbles (PirateSlop/UnderwaterParticles, Ring1), мировые пузырьки с жизнью2.4с, всплытием.12–.28м/с, fade и размером по снаряду. Эмиссия по расстоянию, max40/segment и256/768 частиц; выше actual Height частицы удаляются. VFX-specialist одобрил код и данные native Unity; визуальная игровая приёмка не проводилась. Исправлен NullReferenceException теней VolumetricCloudsURP на Light без UniversalAdditionalLightData: компонент создаётся перед lightCookieSize в обоих путях рендера.
Брызги ядер 2026-10-06, повторный фикс после жалобы: быстрый вход с tangential energy>=100 допускает splash при closing=0. CannonShotDamage сохраняет launchedAboveWater и сообщает пропущенный вход, когда волна накрыла ядро между FixedUpdate; incomingVelocity берётся до подводного damping, оба пути защищены waterEntered от повторных burst при колебании волны; старт под водой не создаёт новый всплеск. Projectile Lift14–20м/с, Life2.8–4с. WaterBowSpray выделяет треть крупных капель .09–.16м с alpha1 и узким вертикальным plume, остальные .035–.075м; y-скорость минимум .85*Lift, нормаль смешана сup. Shader усиливает центральную видимость только alpha>.7–.9; обычный носовой spray сохранён. Native renderer включает Water layer4, shader существует/support=true. Wet contacts/палубные следы сохранены; игровой вид не проверялся.
Окружение C/Tripo:51визуальныйFBX подключён к7Environment+10StarterIsland префабам по прежним GUID. CoastalTripoRock сохраняет nativeUV0/2Kкарты, Groughness/Bmetallic,3textureforward, мягкую мокрую полосу у actualseaLevel; простые shadow/depth и vertexweathering. Растения CC0 используют CoastalFoliage. ОбщийLOD0/1/2:417365/147289/35098tris; переходы .45/.16/.015, LOD2безтеней, GPUmesh безCPUreadableкопии. КонсольUnity0ошибок. CoastalRock остаётся прежним материалом terrain; новая каменная модель не перекрашивается общим RockWall02.
Тестовая галерея:Gallery.prefab сохранён с17обновлёнными моделями в одном ряду,30м по visibleLOD0bounds, maxfloatпогрешность.00013м. ArrangeTestGallery меняет только галерею/подписи/старттестовойкарты. Nativeaudit17prefabs:3LODкаждый,0visualcolliders. Before/after оригинальныхCOL иGUID полностьюравны;15COLFBX SHA256 unchanged; DefaultWorld+19LocationDefinition механики diff0 кромеassetверсий. Seed17421,ShippingGap90,радиусы/количество/проходы сохранены. Пользователь проверяет gameplay,волну/пену,LOD иFPS.
История окружения:slab-shell/voxel из build_coastal_c.py и SeaArch_Huge_A_CReview.blend отвергнуты как пластилин; прежнийAPPROVED отозван. Сборка изCC0сканов тоже отвергнута. Актуальный согласованный подход — целые пользовательскиеTripoGLB с их nativeUV/PBR, настоящиеCC0растения. Арка одобрена человеком; остальные17записей галереи заменены по его разрешению. Новое согласование арки не требуется.
Исправление нагрузки окружения после пользовательского FPS100→25: CoastalShoreFoamVfx использует spatial grid24м, область72м, 2048contacts/64probes, максимум4HeightQueries и256stamps/frame, 24queries/.2с. Атлас256²/192м snap12м, profiler markers и counters. ExposedContours строит winding-union сечения каменных submeshes, исключает внутренние пересечения и сохраняет проходы. CoastalRock считает hash weathering в vertex вместо48sin/pixel, собственные depth/shadow passes совместимы с SRP Batcher; LOD thresholds .45/.16/.015. NativeCPU: old13695HeightOffset38.07мс; новая пена64forcedupdates mean.573/maxwarm1.856мс, budgetviolations0. Это не замер FPS; Play/build не запускались.

- [Assets/Scripts/World/ProceduralWorld.cs](<Assets/Scripts/World/ProceduralWorld.cs>) — Исходник C#: ProceduralWorld.
- [Assets/Scripts/World/WorldProfile.cs](<Assets/Scripts/World/WorldProfile.cs>) — Исходник C#: WorldDecoration, WorldProfile.
- [Assets/Scripts/World/WorldGenerator.cs](<Assets/Scripts/World/WorldGenerator.cs>) — Исходник C#: WorldGenerator.
- [Assets/Scripts/World/BalancedWorldGenerator.cs](<Assets/Scripts/World/BalancedWorldGenerator.cs>) — Исходник C#: BalancedWorldGenerator.
- [Assets/Scripts/World/WorldDecorationPlacer.cs](<Assets/Scripts/World/WorldDecorationPlacer.cs>) — Исходник C#: WorldDecorationPlacer.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
- [Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader](<Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader>) — Шейдер.
- [Assets/Scripts/Networking/SessionStorm.cs](<Assets/Scripts/Networking/SessionStorm.cs>) — Исходник C#: StormMessage, SessionController.
- [Assets/Scripts/Editor/OceanSetup.cs](<Assets/Scripts/Editor/OceanSetup.cs>) — Исходник C#: OceanSetup.
- [Assets/Scripts/Editor/WorldGenerationSetup.cs](<Assets/Scripts/Editor/WorldGenerationSetup.cs>) — Исходник C#: WorldGenerationSetup.
- [Assets/Scripts/World/EnvironmentTestGallery.cs](<Assets/Scripts/World/EnvironmentTestGallery.cs>) — Исходник C#: EnvironmentTestGallery.
- [Assets/Scripts/Editor/EnvironmentTestSetup.cs](<Assets/Scripts/Editor/EnvironmentTestSetup.cs>) — Исходник C#: EnvironmentTestSetup.
- [Assets/Scenes/NetworkOcean.unity](<Assets/Scenes/NetworkOcean.unity>) — Сцена Unity.
- [Assets/Scripts/World/WorldStructureCollision.cs](<Assets/Scripts/World/WorldStructureCollision.cs>) — Исходник C#: WorldStructureCollision.
- [Assets/Scripts/World/OceanHeightSource.cs](<Assets/Scripts/World/OceanHeightSource.cs>) — Исходник C#: OceanHeightSource.
- [Assets/Scripts/World/BoatAttackOcean.cs](<Assets/Scripts/World/BoatAttackOcean.cs>) — Исходник C#: BoatAttackOcean, SpectralWave.
- [Assets/Resources/WaterSystemSettings.asset](<Assets/Resources/WaterSystemSettings.asset>) — Настройки или данные Unity.
- [Assets/Settings/WaterTests/BoatAttackWaterTile.asset](<Assets/Settings/WaterTests/BoatAttackWaterTile.asset>) — Настройки или данные Unity.
- [Packages/com.unity.urp-water-system/Runtime/Bodies/Water.cs](<Packages/com.unity.urp-water-system/Runtime/Bodies/Water.cs>) — Исходник C#: Water, Settings, TempData.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/WaterCommon.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/WaterCommon.hlsl>) — Код шейдера.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/Water.shader](<Packages/com.unity.urp-water-system/Runtime/Shaders/Water.shader>) — Шейдер.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/InfiniteWater.shader](<Packages/com.unity.urp-water-system/Runtime/Shaders/InfiniteWater.shader>) — Шейдер.
- [Packages/com.unity.urp-water-system/Runtime/System/SystemInitializer.cs](<Packages/com.unity.urp-water-system/Runtime/System/SystemInitializer.cs>) — Исходник C#: SystemInitializer.
- [Packages/com.unity.urp-water-system/Runtime/System/WaterManager.cs](<Packages/com.unity.urp-water-system/Runtime/System/WaterManager.cs>) — Исходник C#: WaterManager.
- [Packages/com.unity.urp-water-system/Runtime/Physics/WaterPhysics.cs](<Packages/com.unity.urp-water-system/Runtime/Physics/WaterPhysics.cs>) — Исходник C#: WaterPhysics, BoundingBoxCheck, WaterSurfacePrep, WaterBodyLookup, public, WaterBodyData.
- [Packages/com.unity.urp-water-system/Runtime/Rendering/MeshSurface.cs](<Packages/com.unity.urp-water-system/Runtime/Rendering/MeshSurface.cs>) — Исходник C#: MeshSurface, BaseLayout, SubdivideTiles, MatrixJob, WaterTile, WaterMeshSettings.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/WaterInput.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/WaterInput.hlsl>) — Код шейдера.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/CommonUtilities.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/CommonUtilities.hlsl>) — Код шейдера.
- [Assets/Settings/PC_Renderer.asset](<Assets/Settings/PC_Renderer.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/SteamTestBuild.cs](<Assets/Scripts/Editor/SteamTestBuild.cs>) — Исходник C#: SteamTestBuild.
- [Assets/Scripts/World/WaterShipFoam.cs](<Assets/Scripts/World/WaterShipFoam.cs>) — Исходник C#: WaterShipFoam, HullState, Packet, Hit, SurfaceImpact.
- [Assets/Shaders/WaterShipFoam.hlsl](<Assets/Shaders/WaterShipFoam.hlsl>) — Код шейдера.
- [Assets/Scripts/World/WaterTestCapture.cs](<Assets/Scripts/World/WaterTestCapture.cs>) — Исходник C#: WaterTestCapture.
- [Assets/Settings/WaterTests/BoatAttackWater.mat](<Assets/Settings/WaterTests/BoatAttackWater.mat>) — Материал Unity.
- [Assets/Scripts/Editor/MultiplayerSceneSetup.cs](<Assets/Scripts/Editor/MultiplayerSceneSetup.cs>) — Исходник C#: MultiplayerSceneSetup.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/GerstnerWaves.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/GerstnerWaves.hlsl>) — Код шейдера.
- [Packages/com.unity.urp-water-system/Runtime/Modifiers/GerstnerWaves.cs](<Packages/com.unity.urp-water-system/Runtime/Modifiers/GerstnerWaves.cs>) — Исходник C#: GerstnerWaves, HeightJob, Data, WaveType, JobData, BasicWaves, Wave, WaveDescriptor.
- [Assets/Resources/EnvironmentTest/Ocean.prefab](<Assets/Resources/EnvironmentTest/Ocean.prefab>) — Префаб Unity.
- [Assets/Scripts/World/TestOceanController.cs](<Assets/Scripts/World/TestOceanController.cs>) — Исходник C#: TestOceanController.
- [Assets/Scripts/Player/DeveloperMenu.cs](<Assets/Scripts/Player/DeveloperMenu.cs>) — Исходник C#: DeveloperMenu.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/WaterLighting.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/WaterLighting.hlsl>) — Код шейдера.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/AtmosphericScattering.hlsl](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/AtmosphericScattering.hlsl>) — Код шейдера.
- [Assets/Scripts/Networking/SessionOcean.cs](<Assets/Scripts/Networking/SessionOcean.cs>) — Исходник C#: TestOceanMessage, SessionController.
- [Assets/Settings/UniversalRenderPipelineGlobalSettings.asset](<Assets/Settings/UniversalRenderPipelineGlobalSettings.asset>) — Настройки или данные Unity.
- [Assets/Resources/EnvironmentTest/ShipFoamAtlas.shader](<Assets/Resources/EnvironmentTest/ShipFoamAtlas.shader>) — Шейдер.
- [Assets/Scripts/ShipBuoyancy.cs](<Assets/Scripts/ShipBuoyancy.cs>) — Исходник C#: ShipBuoyancy.
- [Assets/Scripts/World/UnderwaterRendererFeature.cs](<Assets/Scripts/World/UnderwaterRendererFeature.cs>) — Исходник C#: UnderwaterRendererFeature, ImmersionPass, FogData, SuspensionData.
- [Assets/Scripts/World/UnderwaterEnvironment.cs](<Assets/Scripts/World/UnderwaterEnvironment.cs>) — Исходник C#: UnderwaterEnvironment.
- [Assets/Scripts/World/WaterBowSpray.cs](<Assets/Scripts/World/WaterBowSpray.cs>) — Исходник C#: WaterBowSpray.
- [Assets/Resources/EnvironmentTest/UnderwaterImmersion.shader](<Assets/Resources/EnvironmentTest/UnderwaterImmersion.shader>) — Шейдер.
- [Assets/Resources/EnvironmentTest/UnderwaterSuspension.shader](<Assets/Resources/EnvironmentTest/UnderwaterSuspension.shader>) — Шейдер.
- [Assets/Resources/EnvironmentTest/WaterBowSpray.shader](<Assets/Resources/EnvironmentTest/WaterBowSpray.shader>) — Шейдер.
- [Assets/Settings/TestSky/Underwater.mat](<Assets/Settings/TestSky/Underwater.mat>) — Материал Unity.
- [Assets/Scripts/World/WaterImpactPhysics.cs](<Assets/Scripts/World/WaterImpactPhysics.cs>) — Исходник C#: WaterImpactKind, WaterImpactEvent, WaterImpactPhysics.
- [Assets/Scripts/World/WaterImpactBody.cs](<Assets/Scripts/World/WaterImpactBody.cs>) — Исходник C#: WaterImpactBody.
- [Assets/Scripts/World/WaterSplashContacts.cs](<Assets/Scripts/World/WaterSplashContacts.cs>) — Исходник C#: WaterSplashContacts.
- [Assets/Resources/EnvironmentTest/WaterWetMark.shader](<Assets/Resources/EnvironmentTest/WaterWetMark.shader>) — Шейдер.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.shader](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.shader>) — Шейдер.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsDefs.hlsl](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsDefs.hlsl>) — Код шейдера.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl>) — Код шейдера.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsURP.cs](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsURP.cs>) — Исходник C#: VolumetricCloudsURP, CloudsRenderMode, CloudsAmbientMode, CloudsUpscaleMode, VolumetricCloudsPass, stores, PassData, RasterPassData, VolumetricCloudsAmbientPass, VolumetricCloudsShadowsPass, LightCookieShaderFormat.
- [Assets/Scripts/World/ProjectileWaterFlight.cs](<Assets/Scripts/World/ProjectileWaterFlight.cs>) — Исходник C#: ProjectileWaterFlight.
- [Assets/Scripts/World/UnderwaterProjectileTrail.cs](<Assets/Scripts/World/UnderwaterProjectileTrail.cs>) — Исходник C#: UnderwaterProjectileTrail.
- [Assets/Resources/Underwater/Bubbles.mat](<Assets/Resources/Underwater/Bubbles.mat>) — Материал Unity.
- [Assets/Shaders/UnderwaterParticles.shader](<Assets/Shaders/UnderwaterParticles.shader>) — Шейдер.
- [Assets/Scripts/World/CoastalRockVisual.cs](<Assets/Scripts/World/CoastalRockVisual.cs>) — Исходник C#: CoastalShoreContour, CoastalRockVisual.
- [Assets/Scripts/World/CoastalShoreFoamVfx.cs](<Assets/Scripts/World/CoastalShoreFoamVfx.cs>) — Исходник C#: CoastalShoreFoamVfx, Source, Probe, Contact.
- [Assets/Scripts/Editor/CoastalEnvironmentSetup.cs](<Assets/Scripts/Editor/CoastalEnvironmentSetup.cs>) — Исходник C#: CoastalEnvironmentSetup.
- [Assets/Shaders/CoastalRock.shader](<Assets/Shaders/CoastalRock.shader>) — Шейдер.
- [Assets/Shaders/CoastalFoliage.shader](<Assets/Shaders/CoastalFoliage.shader>) — Шейдер.
- [Assets/Shaders/CoastalShoreFoam.hlsl](<Assets/Shaders/CoastalShoreFoam.hlsl>) — Код шейдера.
- [Art/Blender/World/CoastalEnvironment/CoastalEnvironment.blend](<Art/Blender/World/CoastalEnvironment/CoastalEnvironment.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/generate_coastal.py](<Art/Blender/World/CoastalEnvironment/generate_coastal.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/build_coastal_c.py](<Art/Blender/World/CoastalEnvironment/build_coastal_c.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_CReview.blend](<Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_CReview.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/CoastalSourceModuleQC.blend](<Art/Blender/World/CoastalEnvironment/CoastalSourceModuleQC.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/assemble_cc0_arch.py](<Art/Blender/World/CoastalEnvironment/assemble_cc0_arch.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_NativeModules.blend](<Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_NativeModules.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_Tripo.blend](<Art/Blender/World/CoastalEnvironment/SeaArch_Huge_A_Tripo.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/import_tripo_arch_sources.py](<Art/Blender/World/CoastalEnvironment/import_tripo_arch_sources.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/render_tripo_source_contact.py](<Art/Blender/World/CoastalEnvironment/render_tripo_source_contact.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/assemble_tripo_arch.py](<Art/Blender/World/CoastalEnvironment/assemble_tripo_arch.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/refine_tripo_arch_vault.py](<Art/Blender/World/CoastalEnvironment/refine_tripo_arch_vault.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/add_tripo_arch_footstones.py](<Art/Blender/World/CoastalEnvironment/add_tripo_arch_footstones.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/add_tripo_arch_plants.py](<Art/Blender/World/CoastalEnvironment/add_tripo_arch_plants.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/render_tripo_arch.py](<Art/Blender/World/CoastalEnvironment/render_tripo_arch.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/physical-baseline.json](<Art/Blender/World/CoastalEnvironment/physical-baseline.json>) — Конфигурация / данные JSON.
- [Art/Blender/World/CoastalEnvironment/CoastalTripoCollection.blend](<Art/Blender/World/CoastalEnvironment/CoastalTripoCollection.blend>) — Редактируемая сцена Blender.
- [Art/Blender/World/CoastalEnvironment/assemble_tripo_collection.py](<Art/Blender/World/CoastalEnvironment/assemble_tripo_collection.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/refine_collection.py](<Art/Blender/World/CoastalEnvironment/refine_collection.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/refine_cliffwall_b_base.py](<Art/Blender/World/CoastalEnvironment/refine_cliffwall_b_base.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/render_tripo_collection.py](<Art/Blender/World/CoastalEnvironment/render_tripo_collection.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/render_cliffwall_b_base.py](<Art/Blender/World/CoastalEnvironment/render_cliffwall_b_base.py>) — Инструмент Python.
- [Art/Blender/World/CoastalEnvironment/export_tripo_collection.py](<Art/Blender/World/CoastalEnvironment/export_tripo_collection.py>) — Инструмент Python.
- [Assets/Shaders/CoastalTripoRock.shader](<Assets/Shaders/CoastalTripoRock.shader>) — Шейдер.
- [procedural-world.md](<procedural-world.md>) — Документация.
- [unity.md](<unity.md>) — Документация.
- [Docs/TestOcean.md](<Docs/TestOcean.md>) — Документация.
- [Art/Sources/CoastalEnvironment/SOURCES.md](<Art/Sources/CoastalEnvironment/SOURCES.md>) — Документация.
- [Art/Sources/CoastalEnvironment/TRIPO_MODULAR_ROCK_BRIEF.md](<Art/Sources/CoastalEnvironment/TRIPO_MODULAR_ROCK_BRIEF.md>) — Документация.
- [Art/Blender/World/CoastalEnvironment/README_TRIPO_ARCH.md](<Art/Blender/World/CoastalEnvironment/README_TRIPO_ARCH.md>) — Документация.
- `../Blender/Скалы/README.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- [Art/Blender/World/CoastalEnvironment/README_TRIPO_COLLECTION.md](<Art/Blender/World/CoastalEnvironment/README_TRIPO_COLLECTION.md>) — Документация.

### Сеть, сессия и Steam (`networking`)

Ключевые слова: сеть, мультиплеер, steam, network.

FishNet: серверный авторитет. Изменения формата сообщений согласовывать с ProtocolVersion; не повышать его автоматически без изменения протокола.
IP/Tugboat и Steam — отдельные способы подключения. localhost не подтверждает работу через интернет; позднее подключение и отключение требуют отдельной приёмки.
MultiplayerSceneSetup.Configure и PromoteShipV3 привязывают ShipV3Test к общему SessionController.ShipPrefab. Отдельный вызов ShipV3TestSpawner из ServerState удалён. ShipComparisonEnabled выключен, ComparisonShips очищен; старый корабль сохранён в проекте и регистрации FishNet без изменения существующих индексов.
Кастомизация Ship V3 и сетевое горение: ProtocolVersion=123; игровая проверка и проверка двумя клиентами не запускались.
Меню оставляет основную Тестовую карту и Тест нагрузки. Begin(environmentTest=true) всегда загружает NetworkOcean через SessionSceneLoading; командный -environmenttest запускает тот же режим. Build Windows/Configure Scenes используют NetworkMenu, NetworkOcean, NetworkLoadTest и дополнительные включённые сцены. SteamTestBuild только записывает steam_appid.txt после Windows-сборки.

- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionConfig.cs](<Assets/Scripts/Networking/SessionConfig.cs>) — Исходник C#: SessionConfig.
- [Assets/Scripts/Networking/SessionAuthenticator.cs](<Assets/Scripts/Networking/SessionAuthenticator.cs>) — Исходник C#: HelloMessage, AdmissionMessage, PopulationMessage, SessionAuthenticator.
- [Assets/Scripts/Networking/SteamParty.cs](<Assets/Scripts/Networking/SteamParty.cs>) — Исходник C#: SteamParty.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Networking/SimulationState.cs](<Assets/Scripts/Networking/SimulationState.cs>) — Исходник C#: PlayerCommand, PlayerState, ShipState.
- [Assets/Settings/Networking/SessionConfig.asset](<Assets/Settings/Networking/SessionConfig.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/MultiplayerSceneSetup.cs](<Assets/Scripts/Editor/MultiplayerSceneSetup.cs>) — Исходник C#: MultiplayerSceneSetup.
- [Assets/Scripts/Networking/SessionSceneLoading.cs](<Assets/Scripts/Networking/SessionSceneLoading.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionMenu.cs](<Assets/Scripts/Networking/SessionMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Editor/SteamTestBuild.cs](<Assets/Scripts/Editor/SteamTestBuild.cs>) — Исходник C#: SteamTestBuild.
- [Assets/Scripts/Networking/SessionOcean.cs](<Assets/Scripts/Networking/SessionOcean.cs>) — Исходник C#: TestOceanMessage, SessionController.
- [multiplayer-plan.md](<multiplayer-plan.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Новая система ботов (`bots`)

Ключевые слова: боты, бот, ai.

Спецификация: BOT_SPEC.md; текущий этап и проверки: BOT_PROGRESS.md. Выполнять один этап за запуск.
Старое поведение удалено. Начальная политика создаёт ботов только при запуске с включённой настройкой; подключение человека заменяет бота, смерть/выход не дозаполняют матч.
BotNumber использует стабильный ParticipantId; номера могут иметь пропуски. Наличие серверного персонажа не подтверждает навигацию и действия.

- [BOT_SPEC.md](<BOT_SPEC.md>) — Документация.
- [BOT_PROGRESS.md](<BOT_PROGRESS.md>) — Документация.
- [BOT_ITEMS.md](<BOT_ITEMS.md>) — Документация.
- [Assets/Scripts/Networking/BotConsumables.cs](<Assets/Scripts/Networking/BotConsumables.cs>) — Исходник C#: BotEatFishAction, BotRumStation, SessionController.
- [Assets/Scripts/Networking/BotUtilityAction.cs](<Assets/Scripts/Networking/BotUtilityAction.cs>) — Исходник C#: BotUtilityAction.
- [Assets/Scripts/Networking/BotBoardingStation.cs](<Assets/Scripts/Networking/BotBoardingStation.cs>) — Исходник C#: BotBoardingStation.
- [Assets/Scripts/Networking/BotFishingStation.cs](<Assets/Scripts/Networking/BotFishingStation.cs>) — Исходник C#: BotFishingStation.
- [Assets/Scripts/Networking/BotBellStation.cs](<Assets/Scripts/Networking/BotBellStation.cs>) — Исходник C#: BotBellStation.
- [Assets/Scripts/Networking/NetworkCrewBell.cs](<Assets/Scripts/Networking/NetworkCrewBell.cs>) — Исходник C#: NetworkCrewBell.
- [Assets/Scripts/Networking/BotActionExecutor.cs](<Assets/Scripts/Networking/BotActionExecutor.cs>) — Исходник C#: BotActionState, IBotAction, IBotOutsideWork, BotActionExecutor, BotStuckEvidence, BotStationAction.
- [Assets/Scripts/Networking/BotYieldAction.cs](<Assets/Scripts/Networking/BotYieldAction.cs>) — Исходник C#: BotYieldAction.
- [Assets/Scripts/Networking/BotCombat.cs](<Assets/Scripts/Networking/BotCombat.cs>) — Исходник C#: BotCombatMemory, BotPersonalCombatAction.
- [Assets/Scripts/Networking/BotPersonalWeapons.cs](<Assets/Scripts/Networking/BotPersonalWeapons.cs>) — Исходник C#: BotPersonalWeapons.
- [Assets/Scripts/Networking/BotSwordfishAction.cs](<Assets/Scripts/Networking/BotSwordfishAction.cs>) — Исходник C#: BotSwordfishAction.
- [Assets/Scripts/Networking/BotAreaThrowAction.cs](<Assets/Scripts/Networking/BotAreaThrowAction.cs>) — Исходник C#: BotAreaThrowAction.
- [Assets/Scripts/Networking/BotCombatPosition.cs](<Assets/Scripts/Networking/BotCombatPosition.cs>) — Исходник C#: BotCombatPosition.
- [Assets/Scripts/Networking/BotCannonCombat.cs](<Assets/Scripts/Networking/BotCannonCombat.cs>) — Исходник C#: BotCannonStation.
- [Assets/Scripts/Networking/BotCannonAmmoPolicy.cs](<Assets/Scripts/Networking/BotCannonAmmoPolicy.cs>) — Исходник C#: SessionController, BotCannonAmmoPolicy.
- [Assets/Scripts/Networking/BotBoomerangShot.cs](<Assets/Scripts/Networking/BotBoomerangShot.cs>) — Исходник C#: BotBoomerangShot.
- [Assets/Scripts/Networking/SessionBotCombat.cs](<Assets/Scripts/Networking/SessionBotCombat.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/BotShipStations.cs](<Assets/Scripts/Networking/BotShipStations.cs>) — Исходник C#: IBotApproachConstraint, IBotApproachRange, IBotShipStation, BotHelmStation, BotSailStation, BotCapstanDropStation, BotCapstanRaiseStation.
- [Assets/Scripts/Networking/BotCannonPlacement.cs](<Assets/Scripts/Networking/BotCannonPlacement.cs>) — Исходник C#: BotCannonSite, BotCannonPlacement, BotInstallCannonAction, WorkStation.
- [Assets/Scripts/Networking/BotMaintenanceAction.cs](<Assets/Scripts/Networking/BotMaintenanceAction.cs>) — Исходник C#: BotMaintenanceAction, WorkStation.
- [Assets/Scripts/Networking/BotHullWaterRepairAction.cs](<Assets/Scripts/Networking/BotHullWaterRepairAction.cs>) — Исходник C#: BotHullWaterRepairAction, Phase, ExitStation.
- [Assets/Scripts/Networking/BotIslandLootAction.cs](<Assets/Scripts/Networking/BotIslandLootAction.cs>) — Исходник C#: BotIslandLootAction, Phase.
- [Assets/Scripts/Networking/NetworkLootChest.Ocean.cs](<Assets/Scripts/Networking/NetworkLootChest.Ocean.cs>) — Исходник C#: SeaLootKind, SeaLootState, NetworkLootChest.
- [Assets/Scripts/Networking/BotShoreRoute.cs](<Assets/Scripts/Networking/BotShoreRoute.cs>) — Исходник C#: BotShoreRoute, Node.
- [Assets/Scripts/Interaction/ShipLadder.cs](<Assets/Scripts/Interaction/ShipLadder.cs>) — Исходник C#: ShipLadder.
- [Assets/Scripts/Networking/BotSeaPilot.cs](<Assets/Scripts/Networking/BotSeaPilot.cs>) — Исходник C#: BotSeaPilot.
- [Assets/Scripts/Networking/BotSeaCombatCourse.cs](<Assets/Scripts/Networking/BotSeaCombatCourse.cs>) — Исходник C#: BotSeaCombatCourse.
- [Assets/Scripts/Networking/SessionBotTasks.cs](<Assets/Scripts/Networking/SessionBotTasks.cs>) — Исходник C#: SessionController, BotCrew.
- [Assets/Scripts/Networking/NetworkPlayer.BotTasks.cs](<Assets/Scripts/Networking/NetworkPlayer.BotTasks.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/Networking/DeckRoute.cs](<Assets/Scripts/Networking/DeckRoute.cs>) — Исходник C#: IBotPathSearch, BotPathScheduler, DeckRoute, Node.
- [Assets/Scripts/Networking/BotMotionSettings.cs](<Assets/Scripts/Networking/BotMotionSettings.cs>) — Исходник C#: BotMotionSettings.
- [Assets/Scripts/Networking/BotDiagnostics.cs](<Assets/Scripts/Networking/BotDiagnostics.cs>) — Исходник C#: BotDebugRow, BotDebugSnapshot, BotDebugJournal.
- [Assets/Scripts/Networking/BotValidationProbe.cs](<Assets/Scripts/Networking/BotValidationProbe.cs>) — Исходник C#: BotValidationProbe, Sample.
- [Assets/Scripts/Networking/SessionBotDiagnostics.cs](<Assets/Scripts/Networking/SessionBotDiagnostics.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/NetworkPlayer.BotDiagnostics.cs](<Assets/Scripts/Networking/NetworkPlayer.BotDiagnostics.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/UI/BotDebugPanel.cs](<Assets/Scripts/UI/BotDebugPanel.cs>) — Исходник C#: BotDebugPanel.
- [Assets/Scripts/UI/CrewPresentation.cs](<Assets/Scripts/UI/CrewPresentation.cs>) — Исходник C#: CrewPresentation.
- [Assets/Scripts/Networking/BotRosterPolicy.cs](<Assets/Scripts/Networking/BotRosterPolicy.cs>) — Исходник C#: IBotRosterPolicy, InitialFillBotRosterPolicy.
- [Assets/Scripts/Networking/SessionBotRoster.cs](<Assets/Scripts/Networking/SessionBotRoster.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionCrew.cs](<Assets/Scripts/Networking/SessionCrew.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Networking/NetworkWeapon.Roster.cs](<Assets/Scripts/Networking/NetworkWeapon.Roster.cs>) — Исходник C#: NetworkWeapon.

### Модели и Blender (`art`)

Ключевые слова: модели, blender, арт, модель.

Перед моделированием прочитать blender.md и подтвердить реальный Blender MCP; при импорте нужен также Unity MCP.
Редактируемые модели хранить в Art/Blender, игровые модели в Assets/Models. Сохранять GUID и несохранённую работу; свойства конкретной модели сверять с её импортёром.
В ../NewShip/Ship_V3_Fitted.blend подготовлены труба и кабестан: V9_Telescope_Control и V9_AnchorSystem_Control. Цепь выходит из палубной горловины и клюза прямо к якорю; верхний подвес и шарнир ушка раздельны для будущего качания. Рычаги размахом 2,6 м используют отделку исходной выдвижной детали. Управление описано в TelescopeAndAnchorControls.md; игровая физика волн не подключена.
В ../NewShip/Ship_V3_Fitted.blend добавлены два гарпуна V10_Bow_Harpoons на носовых перилах. Yaw вращает подставку, Pitch — орудие, Reel — катушку, Projectile отделён; свободная верёвка имеет Hook-контроли и три витые пряди. Анимаций нет. Узлы описаны в HarpoonControls.md.
Нижние основания гарпунов подогнаны к ширине перил около 19 см и стоят непосредственно на дереве без серых подвесных пластин. Курки направлены концом к дулу; цапфы согласованы с вилкой, передняя направляющая касается ствола. Катушки используют исходные UV и PBR-текстуры. Дерево кабестана согласовано с V4_Frame_Ship_Art_Frame_Warm_Timber через отдельные albedo-карты V11_Capstan_Matched_Ship_Wood; металл и остальные PBR-карты сохранены.
Настил трюма V3_Deck_SternRoomFloor_* расширен до фактической внутренней обшивки бортов и транца при прежней высоте 8,71 м. Сохранены имена существующих досок, материалы и GameUV; добавлены 27 краевых досок. Актуальные доски включены в подготовку разрушения V18; Unity в этой правке не изменялся. Подробности и превью — HarpoonControls.md.
Исходные папки фонаря, носового ворона и изогнутых деталей перенесены в ../NewShip/Детали корабля. В Ship_V3_Fitted.blend восстановлены ссылки на 20 PBR-карт и выполнена их упаковка; розовые фонари и носовая фигура исправлены без изменений геометрии и UV. Пути записаны в BowTextureRestoreReport.json; пример вида — BowTexturesRestored.png.
В Ship_V3_Fitted.blend закрыта щель над рамой прохода в трюм; раме возвращены исходные PBR-карты из Детали корабля/проём. Внутри трюма рядом с проходом установлена V15_Hold_Cannonball_Dispenser высотой 1,2 м с исходной текстурой и отдельным Mount. Точка V15_Cannonball_Spawn находится за краем лотка примерно на 0,966 м над полом; спавн и анимации пока не реализованы. Узлы описаны в HoldDispenserControls.md.
В проход установлена дверь из ../NewShip/дверь/дверь.zip с исходными упакованными PBR-картами. Проход сужен до 1,10 м для одного игрока, рама согласована с полотном и толщиной стены. V16_Hold_Door_Hinge вращает отдельное полотно вокруг локальной Z от 0 до -100 градусов внутрь трюма, в сторону от маски выдачи ядер; анимаций и игровой логики нет. Исходные меши сохранены; изменённые детали и новая дверь включены в подготовку разрушения V18. Управление описано в HoldDispenserControls.md.
В Ship_V3_Fitted.blend дверь заменена обновлённой моделью из Extracted_V2 и подогнана по деревянному полотну; рама видна с обеих сторон. Добавлена кормовая панель с перенесёнными на крайние стойки фонарями, старые столбы удалены. На основной и передней мачтах два пиратских флага с циклом ветра 120 кадров. В трюме бочка с тремя группами по пять отдельных кубиков и тремя деревянными стаканами, а также четыре разные настенные полки для рома между окнами левого борта, отдельно от выдачи ядер. Управление описано в ShipDetailsControls.md, источники и лицензии в FreeAssets/CREDITS.md. Разрушение подготовлено в V18; Unity не изменялся.
В Ship_V3_Fitted.blend быстрый слой Ship_View отображает 2900 статичных объектов через 29 объединений, всего 296 объектов с геометрией вместо 3167. Ship_Edit сохраняет исходные отдельные меши, PBR и управление. Старые сцены вынесены в резервную копию; скрытые обломки, старые детали и исходники исключены из рабочих слоёв. 34 побайтово одинаковые текстуры объединены без уменьшения размеров. Для 2383 деревянных деталей подготовлено 6275 замкнутых фрагментов, фрагментные опоры 784 недеревянных объектов и граф контактов/креплений. Подробности — OptimizationAndDestructionControls.md; проверка сохранности — V18Validation.json. Unity и прежние FBX не изменялись.
Финальная подгонка корабля V19: изогнутые кронштейны кормовых фонарей входят в квадратные верхушки стоек панели, торцы боковых поручней обрезаны внутри согласованных стоек без дублирующих столбов. На трёх мачтах восстановлена исходная цветовая карта 4K с сохранением металлических обручей. Якорь оставлен на существующей цепи и отдельном шарнире для будущей физики. Пересобраны 15 фрагментов семи изменённых деревянных деталей и локальные зависимости опор; быстрый вид обновлён. Итог — V19Validation.json; Unity и FBX не менялись.
Ship V3 импортирован из открытого Ship_V3_Fitted.blend через Tools/ShipV3/ExportFromOpenBlender.py: 2383 деревянные детали, 6275 фрагментов, исходные материалы, морфы и локальные позы механизмов. Редактируемая копия — Art/Blender/Ships/ShipV3. ShipV3ImportSetup собирает отдельный префаб в preview-сцене, не заменяя NetworkShip и не сохраняя открытое меню.
Игровой импорт текстур Ship V3: Base Color/Emission не более 2048, Normal/данные не более 1024, сжатие Standalone, mipmaps, Read/Write отключён. ShipV3ImportSetup.OptimizeRuntimeTextures и меню PirateSlop/Optimize Ship V3 Runtime Textures применяют эти ограничения без изменения исходных PNG, материалов, модели и основного корабля; повторный импорт не должен возвращать принудительный минимум 4K.
ShipV3ImportSetup.RegisterNetworkPrefab добавляет ShipV3Test в фактический SinglePrefabObjects Assets/Settings/Networking/NetworkPrefabs.asset, сохраняя существующие индексы. PrefabId назначается FishNet при Awake NetworkManager; регистрация только в Assets/DefaultPrefabObjects.asset для этого проекта недостаточна.
ShipV3RenderBatch сокращает число объектов отрисовки без сокращения полигонов, изменения UV или текущих текстур 2K/1K: неподвижные MeshRenderer группируются по материалу и участкам 8 м вдоль Z, максимум 96 источников на группу. Исключены цели ShipV3VisualRig.Motions, руль, дверь, якорь, штурвал, отдельные Rigidbody, HarpoonGun, ShipV3ClothMotion, падающие Attachments, DependentRenderers и звенья цепи. Отслеживаются active и forceRenderingOff источников; перестраиваются только затронутые группы, полностью целые группы кешируются. CPU-данные сгенерированных мешей освобождаются только в Play Mode. Сохранены 110 групп вместо 2758 неподвижных рендереров; 205 неиспользуемых сгенерированных мешей удалены. Edit-mode preview подтвердил точное совпадение 1217522 вершин и 1439357 треугольников источников и групп; проверка начальных/конечных выборок UV, позиций и нормалей дала 0 ошибок. Повреждение одной секции убрало её целую геометрию из группы с ожидаемым числом треугольников, ремонт восстановил кешированную целую группу.
ShipV3ChainInstances рисует 145 звеньев цепи одним instanced-вызовом с резервной отрисовкой исходными MeshRenderer; существующее движение звеньев сохранено. ShipV3VisualRig повторно использует список точек цепи. У шести фонарей сохранены интенсивность и мягкие тени; локальный бюджет теней Low/256. ShipV3ImportSetup.OptimizeRuntimeGeometry повторно применяет оптимизацию геометрии и коллайдеров к новому тестовому префабу. Native inspection сохранённого префаба показывает 678 первоначально активных рендереров вместо 3326; при instancing цепи эффективное число равно 534. Это показатели структуры ассета, без замера кадра в игре.
ShipV3ImportSetup.RepairMechanismBindings обновляет материалы и механизмы сохранённого ShipV3Test без пересоздания основного корабля. ExportFromOpenBlender вызывает update_tag, считывает evaluated-позы и parent_basis, экспортирует 17 положений парусного механизма и 25 положений якорного. Материалы реализованной Geometry Nodes геометрии собираются дополнительно; Base Color выбирается из Albedo/BaseColor, Metallic и Roughness упаковываются в Metal/Smoothness для URP.
Итоговый Base Color мачт V5/V6 переносится через EMIT-запекание графа V19_Preserve_Original_Metal на временной UV-плоскости: сохраняются исходный цвет обручей и тон дерева, вместо выбора неиспользуемого Game_Albedo. Запечённые FinalBaseColor PNG импортируются как sRGB 2048, CompressedHQ, streaming mipmaps, Read/Write false; рабочая сцена Blender восстанавливается.
По запросу пользователя дверь Ship V3 исключена из игрового корабля: V16_Hold_Door_Mount неактивен вместе с полотном и коллайдерами, все Door interaction targets удалены, DoorHinge/DoorGrip очищены, DoorAssembly пуст. Рама проёма сохранена. RemoveDoor применяется к сохранённому префабу, полному импорту и ремонту привязок. Исходник Blender не изменён. Игровой запуск и проверка двумя игроками не выполнялись.
Три дополнительные модели из New импортированы через открытый Blender 5.2.2 LTS. Отдельный исходник BoardingEquipment.blend с упакованными материалами; исходный файл корабля и LootReplacement.blend не перезаписаны. Детализация 1890/1996/1924 треугольника сохранена, игровые размеры и направления нормализованы в Unity по настоящим вершинам. Подготовка и повторный импорт описаны в BoardingEquipment/README.md.
Whisky Bottle с бесплатной BlenderKit Royalty Free загружена в Art/Blender/Loot/WhiskyBottle; жидкость удалена в открытом Blender, стекло и пробка разделены, экспорт WhiskyBottleEmpty.fbx высотой 0.56 м. Источник и лицензия в Source.md. BottleFishReplacementSetup меняет только визуальные префабы и материалы, сохраняя игровые GUID и сетевые компоненты.
Выбранная пользователем кормовая доска №3: тёмное дерево, крупная латунная рамка и объёмные завитки. Создана в открытом Blender через MCP-аддон; 21 меш, 10716 треугольников, 5.6×0.897×0.18 м. Unity ConfigureNameplate размещает её (0,6.44,-20.2) над кормовыми окнами, без коллайдеров. Дерево использует atlas StylShip_Masts; Надпись обращена назад (-Z), теперь формируется геометрией букв. Внешний вид по превью утверждён visual_reviewer.
Наборные буквы для кормовой доски: реальные контуры Georgia Bold со светлой золотистой сердцевиной и широким латунным фасочным кантом. После замечания пользователя штрихи расширены на 0.004 cap, кант 0.032 cap ограничен 45% расстояния противоположного контура; отверстия и исправленные оси сохранены. Набор экспортируется ShipNameGlyphs.fbx/JSON, baseline и advance общие. Импортированная доска имеет лицо +Z и устанавливается с поворотом Y180 к корме; NameAnchor берётся по maxZ NameField с зазором 0.6 мм и локальным поворотом Y180. Размеры имени 4.2×0.48 м и ширина глифов 0.88 сохранены в ShipV3Test/ShipV3Menu. Visual_reviewer утвердил Unity preview двух длинных кириллических имён с 5 м с копиями сценового света; Play, полный Trilight и постобработка игровой камеры не проверялись.

- [Assets/Scripts/Editor/GltfPropImporter.cs](<Assets/Scripts/Editor/GltfPropImporter.cs>) — Исходник C#: GltfPropImporter.
- [Assets/Scripts/Editor/PirateCharacterImport.cs](<Assets/Scripts/Editor/PirateCharacterImport.cs>) — Исходник C#: PirateCharacterImport.
- [Assets/Scripts/Editor/SailRiggingArtSetup.cs](<Assets/Scripts/Editor/SailRiggingArtSetup.cs>) — Исходник C#: SailRiggingArtSetup.
- [Assets/Scripts/Editor/MainShipSetup.cs](<Assets/Scripts/Editor/MainShipSetup.cs>) — Исходник C#: MainShipSetup.
- `../NewShip/Ship_V3_Fitted.blend` — отсутствует в текущем снимке; не использовать как готовый путь.
- [Assets/Scripts/Editor/ShipV3ImportSetup.cs](<Assets/Scripts/Editor/ShipV3ImportSetup.cs>) — Исходник C#: ShipV3ImportSetup.
- [Tools/ShipV3/ExportFromOpenBlender.py](<Tools/ShipV3/ExportFromOpenBlender.py>) — Инструмент Python.
- [Assets/Models/Ships/ShipV3/ShipV3.fbx](<Assets/Models/Ships/ShipV3/ShipV3.fbx>) — Модель / анимации FBX.
- [Assets/Models/Ships/ShipV3/ShipV3.json](<Assets/Models/Ships/ShipV3/ShipV3.json>) — Конфигурация / данные JSON.
- [Art/Blender/Ships/ShipV3/ShipV3_Fitted.blend](<Art/Blender/Ships/ShipV3/ShipV3_Fitted.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Editor/ShipV3BindingRepair.cs](<Assets/Scripts/Editor/ShipV3BindingRepair.cs>) — Исходник C#: ShipV3BindingRepair.
- [Art/Blender/LootReplacement/BoardingEquipment/BoardingEquipment.blend](<Art/Blender/LootReplacement/BoardingEquipment/BoardingEquipment.blend>) — Редактируемая сцена Blender.
- [Art/Blender/LootReplacement/BoardingEquipment/README.md](<Art/Blender/LootReplacement/BoardingEquipment/README.md>) — Документация.
- [Tools/Art/prepare_boarding_equipment.py](<Tools/Art/prepare_boarding_equipment.py>) — Инструмент Python.
- [Tools/Art/import_boarding_equipment.py](<Tools/Art/import_boarding_equipment.py>) — Инструмент Python.
- [Art/Blender/Loot/WhiskyBottle/Source.md](<Art/Blender/Loot/WhiskyBottle/Source.md>) — Документация.
- [Art/Blender/Loot/WhiskyBottle/WhiskyBottle_Empty.blend](<Art/Blender/Loot/WhiskyBottle/WhiskyBottle_Empty.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Loot/WhiskyBottle/WhiskyBottle_Source.blend](<Art/Blender/Loot/WhiskyBottle/WhiskyBottle_Source.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbx](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbx>) — Модель / анимации FBX.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbx](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbx>) — Модель / анимации FBX.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbx](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbx>) — Модель / анимации FBX.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_basecolor.JPEG](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_basecolor.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_metallic.JPEG](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_metallic.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_normal.PNG](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_normal.PNG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_rm.JPEG](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_rm.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_roughness.JPEG](<Art/Blender/Loot/FishReplacement/Fish/tripo_convert_ef7518a4-3de5-4390-add5-a892bae7777b.fbm/Обычная_рыба_roughness.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_basecolor.JPEG](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_basecolor.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_metallic.JPEG](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_metallic.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_normal.PNG](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_normal.PNG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_rm.JPEG](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_rm.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_roughness.JPEG](<Art/Blender/Loot/FishReplacement/Pufferfish/tripo_convert_c2eec7cc-e554-47b0-b8f9-b6cc0ed1121a.fbm/рыба-фугу_roughness.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_basecolor.JPEG](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_basecolor.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_metallic.JPEG](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_metallic.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_normal.PNG](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_normal.PNG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_rm.JPEG](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_rm.JPEG>) — Изображение / текстура.
- [Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_roughness.JPEG](<Art/Blender/Loot/FishReplacement/Swordfish/tripo_convert_e200e96c-6c0f-4ed8-af3c-3f667a1f367e.fbm/Рыба-меч_roughness.JPEG>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/WhiskyBottle/CorkBaseColor.png](<Assets/Models/Loot/Replacement/WhiskyBottle/CorkBaseColor.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/WhiskyBottle/CorkNormal.png](<Assets/Models/Loot/Replacement/WhiskyBottle/CorkNormal.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyBottleEmpty.fbx](<Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyBottleEmpty.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyCork.mat](<Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyCork.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyGlass.mat](<Assets/Models/Loot/Replacement/WhiskyBottle/WhiskyGlass.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/WhiskyBottle/WhiskySwirl.mat](<Assets/Models/Loot/Replacement/WhiskyBottle/WhiskySwirl.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/Fish/Fish.fbx](<Assets/Models/Loot/Replacement/Fish/Fish.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Replacement/Fish/Fish.mat](<Assets/Models/Loot/Replacement/Fish/Fish.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/Fish/FishMetalSmooth.png](<Assets/Models/Loot/Replacement/Fish/FishMetalSmooth.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Fish/Fish_basecolor.jpeg](<Assets/Models/Loot/Replacement/Fish/Fish_basecolor.jpeg>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Fish/Fish_normal.png](<Assets/Models/Loot/Replacement/Fish/Fish_normal.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Pufferfish/Pufferfish.fbx](<Assets/Models/Loot/Replacement/Pufferfish/Pufferfish.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Replacement/Pufferfish/Pufferfish.mat](<Assets/Models/Loot/Replacement/Pufferfish/Pufferfish.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/Pufferfish/PufferfishMetalSmooth.png](<Assets/Models/Loot/Replacement/Pufferfish/PufferfishMetalSmooth.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Pufferfish/Pufferfish_basecolor.jpeg](<Assets/Models/Loot/Replacement/Pufferfish/Pufferfish_basecolor.jpeg>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Pufferfish/Pufferfish_normal.png](<Assets/Models/Loot/Replacement/Pufferfish/Pufferfish_normal.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Swordfish/Swordfish.fbx](<Assets/Models/Loot/Replacement/Swordfish/Swordfish.fbx>) — Модель / анимации FBX.
- [Assets/Models/Loot/Replacement/Swordfish/Swordfish.mat](<Assets/Models/Loot/Replacement/Swordfish/Swordfish.mat>) — Материал Unity.
- [Assets/Models/Loot/Replacement/Swordfish/SwordfishMetalSmooth.png](<Assets/Models/Loot/Replacement/Swordfish/SwordfishMetalSmooth.png>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Swordfish/Swordfish_basecolor.jpeg](<Assets/Models/Loot/Replacement/Swordfish/Swordfish_basecolor.jpeg>) — Изображение / текстура.
- [Assets/Models/Loot/Replacement/Swordfish/Swordfish_normal.png](<Assets/Models/Loot/Replacement/Swordfish/Swordfish_normal.png>) — Изображение / текстура.
- [Assets/Scripts/Editor/BottleFishReplacementSetup.cs](<Assets/Scripts/Editor/BottleFishReplacementSetup.cs>) — Исходник C#: BottleFishReplacementSetup.
- [Assets/Resources/BottleVortex.shader](<Assets/Resources/BottleVortex.shader>) — Шейдер.
- [Art/Blender/Barricade/Barricade.blend](<Art/Blender/Barricade/Barricade.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Barricade/PrepareBarricade.py](<Art/Blender/Barricade/PrepareBarricade.py>) — Инструмент Python.
- [Assets/Models/Barricade/Barricade.fbx](<Assets/Models/Barricade/Barricade.fbx>) — Модель / анимации FBX.
- [Assets/Models/Barricade/BarricadeFragments.fbx](<Assets/Models/Barricade/BarricadeFragments.fbx>) — Модель / анимации FBX.
- [Art/Blender/Ships/ShipNameplate/ShipNameplate.blend](<Art/Blender/Ships/ShipNameplate/ShipNameplate.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Ships/ShipNameplate/create_ship_nameplate.py](<Art/Blender/Ships/ShipNameplate/create_ship_nameplate.py>) — Инструмент Python.
- [Art/Blender/Ships/ShipNameplate/ShipNameplatePreview.png](<Art/Blender/Ships/ShipNameplate/ShipNameplatePreview.png>) — Изображение / текстура.
- [Assets/Models/Ships/ShipNameplate/ShipNameplate.fbx](<Assets/Models/Ships/ShipNameplate/ShipNameplate.fbx>) — Модель / анимации FBX.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateWood.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateWood.mat>) — Материал Unity.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateWoodEdge.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateWoodEdge.mat>) — Материал Unity.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateBrass.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateBrass.mat>) — Материал Unity.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateBrassHighlight.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameplateBrassHighlight.mat>) — Материал Unity.
- [Art/Blender/Ships/ShipNameplate/ShipNameGlyphs.blend](<Art/Blender/Ships/ShipNameplate/ShipNameGlyphs.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Ships/ShipNameplate/create_ship_name_glyphs.py](<Art/Blender/Ships/ShipNameplate/create_ship_name_glyphs.py>) — Инструмент Python.
- [Art/Blender/Ships/ShipNameplate/ShipNameGlyphsPreview.png](<Art/Blender/Ships/ShipNameplate/ShipNameGlyphsPreview.png>) — Изображение / текстура.
- [Assets/Models/Ships/ShipNameplate/ShipNameGlyphs.fbx](<Assets/Models/Ships/ShipNameplate/ShipNameGlyphs.fbx>) — Модель / анимации FBX.
- [Assets/Models/Ships/ShipNameplate/ShipNameGlyphs.json](<Assets/Models/Ships/ShipNameplate/ShipNameGlyphs.json>) — Конфигурация / данные JSON.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameGlyphFace.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameGlyphFace.mat>) — Материал Unity.
- [Assets/Models/Ships/ShipNameplate/Materials/ShipNameGlyphRim.mat](<Assets/Models/Ships/ShipNameplate/Materials/ShipNameGlyphRim.mat>) — Материал Unity.
- [Art/Blender/Ships/ShipNameplate/ShipNameGlyphsDetail.png](<Art/Blender/Ships/ShipNameplate/ShipNameGlyphsDetail.png>) — Изображение / текстура.
- [blender.md](<blender.md>) — Документация.
- [unity.md](<unity.md>) — Документация.
- [frigate.md](<frigate.md>) — Документация.
- `../NewShip/V3Preparation/TelescopeAndAnchorControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/External/AnchorNikdane12/SOURCES.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/HarpoonControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/BowTextureRestoreReport.json` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/HoldDispenserControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/ShipDetailsControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/FreeAssets/CREDITS.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/OptimizationAndDestructionControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/V18Validation.json` — отсутствует в текущем снимке; не использовать как готовый путь.
- `../NewShip/V3Preparation/V19Validation.json` — отсутствует в текущем снимке; не использовать как готовый путь.

### Звуки и голос (`audio`)

Ключевые слова: звук, звуки, голос, voice.

Назначения звуков хранить в существующем GameAudioBank. Источники и лицензии проверять в CREDITS.
Голосовой чат — отдельная система от игровых звуков. SessionVoiceMenu переключает сохранённый режим VOIP: удержание B (по умолчанию; V отведена под улучшения) или активация микрофона по RMS-порогу -60..-20 дБ (по умолчанию -40), с хвостом 0.3 с. PirateVoiceInputFilter фильтрует исходящие кадры до кодирования; запреты в меню, без фокуса и при смерти сохраняются.
Взлом плота: шесть Lockpick cues в GameAudioBank. Движение отмычки, вращение и заедание звучат локально с ограничением частоты; начало, поломка и успех подтверждаются сервером и слышны рядом. Короткие CC0-записи и обработка перечислены в Assets/Audio/Lockpick/SOURCE.md; варианты движения/заедания не повторяются подряд.
ShipV3InteractionAudioSetup импортирует короткие CC0-фрагменты костей на столе, контактов костей, тряски в кружке, зажигания и тушения огня и три новых удара рынды. Новые SoundCue добавлены в конец enum; GameAudio исключает немедленные повторы этих вариантов. Источники и лицензии: Assets/Audio/ShipInteractions/SOURCES.md. Свеча и фонари используют одинаковые FlameLight/FlameExtinguish через серверные события.
SabreWood добавлен в конец SoundCue без изменения прежних числовых значений. SabreModelReplacementSetup назначает существующий Foley/chop.ogg (громкость 0.6, дистанция 22 м); звук контакта с деревом передаётся вместе с серверным эффектом. Knife остаётся звуком взмаха.
BottleBreak добавлен последним в SoundCue и назначен через BottleFishReplacementSetup.ConfigureBottleBreakAudio. Три оригинальных процедурных mono PCM16 WAV, 44.1 кГц, 0.725–0.775 с, громкость 0.75, дистанция 24 м; источники описаны в Assets/Audio/BottleBreak/SOURCES.md, генератор — Art/Audio/BottleBreak/GenerateBottleBreak.py. Это отдельный звук разбитого стекла; BottleClose сохраняет звук пробки. Звук и осколки приходят из одного серверного события разбития; общий эффект пули для этих бутылок подавлен. Ссылки и импорт проверены, звучание на слух и Play Mode не проверялись.
BarricadeBreak добавлен последним без изменения прежних значений SoundCue. Использует существующий Naval/cannon_hit_ship_short.ogg с громкостью 0.7 и дистанцией 55 м; вызывается вместе с серверным разрушением баррикады.
Старый неподключённый StormWeather.cs удалён по Git-аудиту 2026-10-05; действующие системы шторма и все звуковые клипы сохранены.
SelectedReview: выбор звуков revision 132 (80 файлов, 55 событий) импортирован в GameAudioBank; 173 WAV и 9 прежних ссылок после нарезки. GameAudio.Selected управляет LowHealth/Whirlpool/Vortex; DistanceShotAudio разделяет ближние и дальние выстрелы, для DoubleBarrel дальний слой фильтруется в игре. Редкости Rare/Epic/Legendary и BulletNearbyImpact разделены. Источники, интервалы и комментарии сохранены в SelectionManifest.json/SOURCES.md. Новые cues добавлены в конец enum; сетевой протокол 128 из-за новых аудио RPC и причины drowning в DamageFeedback. Play Mode и мультиплеер проверяет пользователь.
Настройка после ручной проверки 2026-10-06: Pistol усилен на 50% в пределах 5 м с плавным возвратом до 15 м. Редкость проигрывается при открытии V по максимуму текущих offers (с ожиданием первой сетевой руки), подтверждение выбора использует Select. KnockdownBody вызывается при ApplyKnockdown независимо от ragdoll, для локального игрока 2D Effects. WaterRunSteps учитывает вино и улучшение, реальную водную опору и имеет приоритет перед палубой. У водоворота Ocean/Wind плавно снижаются до 30%, оба слоя WhirlpoolNear усилены вдвое. Настройки требуют проверки звучания в игре.
Пинок: KickSwing — взмах одежды из локального SoundBits/Sonniss2016; KickBody — Deep Punch02 из The Chris Alan/Sonniss2018; KickObject и KickCannon используют существующие деревянные/металлические звуки банка. События пространственные, исходники записаны в Assets/Audio/Combat/Kick/Sources.md. Прослушивание в игре оставлено пользователю.

- [Assets/Scripts/Audio/GameAudio.cs](<Assets/Scripts/Audio/GameAudio.cs>) — Исходник C#: GameAudio.
- [Assets/Scripts/Audio/GameAudioBank.cs](<Assets/Scripts/Audio/GameAudioBank.cs>) — Исходник C#: SoundCue, GameAudioBank, Entry.
- [Assets/Scripts/Audio/GameplayAudio.cs](<Assets/Scripts/Audio/GameplayAudio.cs>) — Исходник C#: GameplayAudio.
- [Assets/Scripts/Audio/PirateVoiceChat.cs](<Assets/Scripts/Audio/PirateVoiceChat.cs>) — Исходник C#: PirateVoiceChat.
- [Assets/Scripts/Audio/PirateVoiceInputFilter.cs](<Assets/Scripts/Audio/PirateVoiceInputFilter.cs>) — Исходник C#: PirateVoiceInputFilter.
- [Assets/Scripts/Networking/SessionVoiceMenu.cs](<Assets/Scripts/Networking/SessionVoiceMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/NetworkPlayer.Voice.cs](<Assets/Scripts/Networking/NetworkPlayer.Voice.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/Editor/AudioBankWindow.cs](<Assets/Scripts/Editor/AudioBankWindow.cs>) — Исходник C#: AudioBankWindow, Page.
- [Assets/Resources/GameAudioBank.asset](<Assets/Resources/GameAudioBank.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Player/PlayerInventory.RaftLockpick.cs](<Assets/Scripts/Player/PlayerInventory.RaftLockpick.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Networking/NetworkLootChest.Raft.cs](<Assets/Scripts/Networking/NetworkLootChest.Raft.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Audio/Lockpick/SOURCE.md](<Assets/Audio/Lockpick/SOURCE.md>) — Документация.
- [Assets/Audio/Lockpick/PickMove01.wav](<Assets/Audio/Lockpick/PickMove01.wav>) — Аудио.
- [Assets/Audio/Lockpick/PickMove02.wav](<Assets/Audio/Lockpick/PickMove02.wav>) — Аудио.
- [Assets/Audio/Lockpick/PickMove03.wav](<Assets/Audio/Lockpick/PickMove03.wav>) — Аудио.
- [Assets/Audio/Lockpick/LockTurn.wav](<Assets/Audio/Lockpick/LockTurn.wav>) — Аудио.
- [Assets/Audio/Lockpick/LockJam01.wav](<Assets/Audio/Lockpick/LockJam01.wav>) — Аудио.
- [Assets/Audio/Lockpick/LockJam02.wav](<Assets/Audio/Lockpick/LockJam02.wav>) — Аудио.
- [Assets/Audio/Lockpick/PickBreak.wav](<Assets/Audio/Lockpick/PickBreak.wav>) — Аудио.
- [Assets/Audio/Lockpick/LockSuccess.wav](<Assets/Audio/Lockpick/LockSuccess.wav>) — Аудио.
- [Assets/Scripts/Ships/ShipV3DiceContact.cs](<Assets/Scripts/Ships/ShipV3DiceContact.cs>) — Исходник C#: ShipV3DiceContact.
- [Assets/Scripts/Editor/ShipV3InteractionAudioSetup.cs](<Assets/Scripts/Editor/ShipV3InteractionAudioSetup.cs>) — Исходник C#: ShipV3InteractionAudioSetup.
- [Assets/Scripts/Loot/BottleBreakVfx.cs](<Assets/Scripts/Loot/BottleBreakVfx.cs>) — Исходник C#: BottleBreakVfx.
- [Assets/Scripts/Editor/BottleFishReplacementSetup.cs](<Assets/Scripts/Editor/BottleFishReplacementSetup.cs>) — Исходник C#: BottleFishReplacementSetup.
- [Art/Audio/BottleBreak/GenerateBottleBreak.py](<Art/Audio/BottleBreak/GenerateBottleBreak.py>) — Инструмент Python.
- [Assets/Audio/BottleBreak/BottleBreak01.wav](<Assets/Audio/BottleBreak/BottleBreak01.wav>) — Аудио.
- [Assets/Audio/BottleBreak/BottleBreak02.wav](<Assets/Audio/BottleBreak/BottleBreak02.wav>) — Аудио.
- [Assets/Audio/BottleBreak/BottleBreak03.wav](<Assets/Audio/BottleBreak/BottleBreak03.wav>) — Аудио.
- [Assets/Scripts/Loot/BarricadeBreakAnimation.cs](<Assets/Scripts/Loot/BarricadeBreakAnimation.cs>) — Исходник C#: BarricadeBreakAnimation.
- [Assets/Scripts/Audio/GameAudio.Selected.cs](<Assets/Scripts/Audio/GameAudio.Selected.cs>) — Исходник C#: GameAudio.
- [Assets/Scripts/Audio/DistanceShotAudio.cs](<Assets/Scripts/Audio/DistanceShotAudio.cs>) — Исходник C#: DistanceShotAudio.
- [Assets/Audio/SelectedReview/SelectionManifest.json](<Assets/Audio/SelectedReview/SelectionManifest.json>) — Конфигурация / данные JSON.
- [Assets/Audio/Combat/Kick/KickSwing.wav](<Assets/Audio/Combat/Kick/KickSwing.wav>) — Аудио.
- [Assets/Audio/Combat/Kick/KickBody.wav](<Assets/Audio/Combat/Kick/KickBody.wav>) — Аудио.
- [Assets/Audio/Combat/Kick/Sources.md](<Assets/Audio/Combat/Kick/Sources.md>) — Документация.
- [AudioIntegration.md](<AudioIntegration.md>) — Документация.
- [Assets/Audio/CREDITS.md](<Assets/Audio/CREDITS.md>) — Документация.
- [Assets/Audio/ShipInteractions/SOURCES.md](<Assets/Audio/ShipInteractions/SOURCES.md>) — Документация.
- [Assets/Audio/BottleBreak/SOURCES.md](<Assets/Audio/BottleBreak/SOURCES.md>) — Документация.
- [Assets/Audio/SelectedReview/SOURCES.md](<Assets/Audio/SelectedReview/SOURCES.md>) — Документация.

### Меню и HUD (`ui`)

Ключевые слова: интерфейс, меню, hud.

SessionController разделён на partial-файлы меню. Различать меню сессии и игровой HUD.
Сохранённое оформление меню и его runtime-поведение имеют разные точки входа.
GameTelemetry подключается в SessionController.Awake. Настройки → Игра и интерфейс → Полная телеметрия сохраняют ShowTelemetry (по умолчанию выключено). Локальная панель справа под HUD шторма обновляется раз в 0.5 с: FPS/кадр, доступные CPU/Render/GPU и render counters, Unity/GC память, RTT/тики/роль, загруженные игроки/корабли/сундуки, XYZ и скорость корабля. Недоступные счётчики показаны прочерком; сбор отключён вместе с панелью.
MenuPresentationSetup.ReplaceShip создаёт фон меню из геометрии Ship V3 без сетевых и физических компонентов. Копируются только активные ветки и включённые рендереры: сохранённые объединённые меши учитываются, их выключенные исходники не дублируются. Старый MenuShip удалён из NetworkMenu; исходный старый префаб сохранён.
MenuBackdrop заменяет фон корабля при Awake на Resources/Ships/ShipV3Menu: текущие паруса и кормовая доска без сетевых/физических компонентов. Меню кастомизации использует этот корабль и добавляет поле названия, сохранённого с парусами. Сцену NetworkMenu с несохранёнными изменениями при настройке не сохраняли.
В кастомизации кнопка «Название» открывает отдельный ввод имени и плавно переводит камеру к кормовой доске. Буквы появляются и удаляются сразу; доска размещается в свободной части экрана слева от панели. Возврат через «Паруса и флаги»/«Вид на корабль». Runtime-буквы объединены в меш и не используют экранный TextMesh.
F8 в основной тестовой карте содержит ползунки силы, крутизны и скорости Boat Attack волн, сброс, существующие день/ночь и морской туман. Отдельных пунктов водных сцен в меню нет.
Меню 2026-10-06: MenuBackdrop при Awake заменяет старую MenuSea на Water из EnvironmentTest/Ocean, включает SkyDayNight и TestSkyPipeline, HDR/postprocessing и Volume layer30. Вода спокойнее (.55 strength/.75 steepness), небо TargetBlend .25. Меню включает/выключает окружение вместе с Content и восстанавливает прежний pipeline через TestSkyDayNight. Сцена редактора не перезаписывалась; переходы в игре не проверены.
Чёрный экран меню после сборки 2026-10-06: Player.log указал NullReferenceException VolumetricCloudsShadowsPass.RecordRenderGraph на lightCookieSize. Светила EnvironmentTest/SkyDayNight не имеют UniversalAdditionalLightData; VolumetricCloudsURP создаёт его перед записью свойств cookie в RenderGraph и legacy pass. Компиляция проверена, новая сборка и запуск Player не выполнялись.

- [Assets/Scripts/Networking/SessionMenu.cs](<Assets/Scripts/Networking/SessionMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionPartyMenu.cs](<Assets/Scripts/Networking/SessionPartyMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/UI/PlayerHud.cs](<Assets/Scripts/UI/PlayerHud.cs>) — Исходник C#: PlayerHud.
- [Assets/Scripts/UI/GameTelemetry.cs](<Assets/Scripts/UI/GameTelemetry.cs>) — Исходник C#: GameTelemetry.
- [Assets/Scripts/UI/PirateHudStyle.cs](<Assets/Scripts/UI/PirateHudStyle.cs>) — Исходник C#: PirateHudStyle.
- [Assets/Scripts/UI/HudLayout.cs](<Assets/Scripts/UI/HudLayout.cs>) — Исходник C#: HudLayout, Scope.
- [Assets/Scripts/Player/MenuBackdrop.cs](<Assets/Scripts/Player/MenuBackdrop.cs>) — Исходник C#: MenuBackdrop.
- [Assets/Scripts/Editor/MenuPresentationSetup.cs](<Assets/Scripts/Editor/MenuPresentationSetup.cs>) — Исходник C#: MenuPresentationSetup.
- [Assets/Scripts/Customization/SailCustomizationUI.cs](<Assets/Scripts/Customization/SailCustomizationUI.cs>) — Исходник C#: SailCustomizationUI, GizmoMode.
- [Assets/Scripts/Customization/ShipNameplate.cs](<Assets/Scripts/Customization/ShipNameplate.cs>) — Исходник C#: ShipNameplate.
- [Assets/Scripts/Editor/ShipCustomizationSetup.cs](<Assets/Scripts/Editor/ShipCustomizationSetup.cs>) — Исходник C#: ShipCustomizationSetup, GlyphMetadata, KerningMetadata, GlyphMetadataSet.
- [Assets/Resources/Ships/ShipV3Menu.prefab](<Assets/Resources/Ships/ShipV3Menu.prefab>) — Префаб Unity.
- [Assets/Scripts/Customization/ShipNameGlyphLibrary.cs](<Assets/Scripts/Customization/ShipNameGlyphLibrary.cs>) — Исходник C#: ShipNameGlyphLibrary, Glyph, Kerning.
- [Assets/Scripts/Player/DeveloperMenu.cs](<Assets/Scripts/Player/DeveloperMenu.cs>) — Исходник C#: DeveloperMenu.
- [Assets/UI/Inventory/Lantern.png](<Assets/UI/Inventory/Lantern.png>) — Изображение / текстура.
- [unity.md](<unity.md>) — Документация.
- [qol-roadmap.md](<qol-roadmap.md>) — Документация.

### Шторм, зона и объёмный туман (`storm`)

Ключевые слова: шторм, зона, туман, fog, brzone.

Логика зоны и её сетевое состояние находятся в StormZone и SessionStorm; визуал объёмного шторма — в BRZoneVolumetric. SeaMistRendererFeature задаёт туман 15% по умолчанию; OceanSurface при запуске NetworkOcean применяет тот же множитель к обычному туману. DeveloperMenu показывает процент и меняет оба вида тумана от общей базовой плотности.
В каталоге Assets/Game также есть BRZoneV2, BRZoneV3 и BRZoneFinal. Наличие нескольких вариантов не означает, что все подключены: проверять ссылки только нужной сцены/префаба.
Старый неподключённый StormWeather.cs удалён по Git-аудиту 2026-10-05; действующие системы шторма и все звуковые клипы сохранены.

- [Assets/Scripts/World/StormZone.cs](<Assets/Scripts/World/StormZone.cs>) — Исходник C#: StormZone.
- [Assets/Scripts/Networking/SessionStorm.cs](<Assets/Scripts/Networking/SessionStorm.cs>) — Исходник C#: StormMessage, SessionController.
- [Assets/Game/BRZoneVolumetric/StormVolumeController.cs](<Assets/Game/BRZoneVolumetric/StormVolumeController.cs>) — Исходник C#: StormVolumeController.
- [Assets/Game/BRZoneVolumetric/SeaMistRendererFeature.cs](<Assets/Game/BRZoneVolumetric/SeaMistRendererFeature.cs>) — Исходник C#: SeaMistRendererFeature.
- [Assets/Scripts/Player/DeveloperMenu.cs](<Assets/Scripts/Player/DeveloperMenu.cs>) — Исходник C#: DeveloperMenu.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
- [Assets/Game/BRZoneVolumetric/SeaMist.mat](<Assets/Game/BRZoneVolumetric/SeaMist.mat>) — Материал Unity.

### Гарпун и корабельное крепление (`harpoon`)

Ключевые слова: гарпун.

Наведение, выстрел, трос и прочность установки — HarpoonGun; снаряд и цель зацепления вынесены отдельно. Сетевая часть корабля — NetworkShip.Harpoon.
Для будущей замены модели в ../NewShip/Ship_V3_Fitted.blend подготовлены два гарпуна с раздельными Yaw/Pitch/Reel/Projectile и редактируемой верёвкой. Только Blender-модель; игровые скрипты и Unity-префабы не изменены. Основания стоят на перилах без подвесных пластин; уменьшенные курки направлены концом к дулу. Исправлены центр оси цапф, разворот вилки и посадка направляющей; катушки сохраняют исходную PBR-отделку.
Древко обоих заряженных гарпунов укорочено примерно на 28,4 см, оставлено около 2 см выноса перед узлом ушка. Острие, шейка и ушко перенесены ближе к дулу без изменения формы и масштаба; конец верёвки следует за ушком. Исходные меши сохранены резервными данными в BLEND. Размеры и превью указаны в HarpoonControls.md.
Для ExternalVisualRig HarpoonGun сохраняет авторскую исходную ориентацию и вращает YawAxis/PitchAxis в локальных координатах. ShipV3BindingRepair располагает камеру на 1.45 м за дулом и на 0.48 м выше, направляя вдоль ствола. У основного корабля остаётся прежняя ветка вращения.
Подводный гарпун 2026-10-06: отменён возврат по фиксированной высоте 0.05; поверхность определяется OceanSurface, коллайдер исключает слой Water. В воде сопротивление, пониженная гравитация, пузырьковый след и возврат через 4.5 с; тросовое ограничение дальности и попадания сохранены. Клиентский след строится по серверной позе. Компиляция проверена; игровой/сетевой тест не запускался.

- [Assets/Scripts/Harpoon/HarpoonGun.cs](<Assets/Scripts/Harpoon/HarpoonGun.cs>) — Исходник C#: HarpoonGun.
- [Assets/Scripts/Harpoon/HarpoonProjectile.cs](<Assets/Scripts/Harpoon/HarpoonProjectile.cs>) — Исходник C#: HarpoonProjectile, HarpoonState.
- [Assets/Scripts/Harpoon/HarpoonHookTarget.cs](<Assets/Scripts/Harpoon/HarpoonHookTarget.cs>) — Исходник C#: HarpoonHookTarget.
- [Assets/Scripts/Harpoon/HarpoonShipMount.cs](<Assets/Scripts/Harpoon/HarpoonShipMount.cs>) — Исходник C#: HarpoonShipMount.
- [Assets/Scripts/Networking/NetworkShip.Harpoon.cs](<Assets/Scripts/Networking/NetworkShip.Harpoon.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Ships/ShipV3HarpoonVisual.cs](<Assets/Scripts/Ships/ShipV3HarpoonVisual.cs>) — Исходник C#: ShipV3HarpoonVisual.
- [Assets/Resources/Ships/ShipV3HarpoonPort.prefab](<Assets/Resources/Ships/ShipV3HarpoonPort.prefab>) — Префаб Unity.
- [Assets/Resources/Ships/ShipV3HarpoonStarboard.prefab](<Assets/Resources/Ships/ShipV3HarpoonStarboard.prefab>) — Префаб Unity.
- `../NewShip/V3Preparation/HarpoonControls.md` — отсутствует в текущем снимке; не использовать как готовый путь.

### Кракен и щупальца (`kraken`)

Ключевые слова: кракен, щупальца.

KrakenEncounterManager отслеживает стоянку корабля и управляет встречей; для сетевого корабля обновление ограничено сервером. Атаки, щупальца и их анимация разделены по файлам.

- [Assets/Scripts/Kraken/KrakenEncounterManager.cs](<Assets/Scripts/Kraken/KrakenEncounterManager.cs>) — Исходник C#: KrakenEncounterManager.
- [Assets/Scripts/Kraken/KrakenEncounter.cs](<Assets/Scripts/Kraken/KrakenEncounter.cs>) — Исходник C#: KrakenEncounter.
- [Assets/Scripts/Kraken/KrakenAttackSystem.cs](<Assets/Scripts/Kraken/KrakenAttackSystem.cs>) — Исходник C#: KrakenAttackSystem.
- [Assets/Scripts/Kraken/KrakenTentacle.cs](<Assets/Scripts/Kraken/KrakenTentacle.cs>) — Исходник C#: KrakenTentacle.
- [Assets/Scripts/Kraken/TentacleAnimator.cs](<Assets/Scripts/Kraken/TentacleAnimator.cs>) — Исходник C#: TentacleAnimator.
- [Assets/Scripts/Networking/NetworkShip.Kraken.cs](<Assets/Scripts/Networking/NetworkShip.Kraken.cs>) — Исходник C#: NetworkShip.

### Кит — точка интереса и сундук (`whale`)

Ключевые слова: кит, poi.

WhaleLootPoint хранит состояния Idle, Agitated, Diving, Cleared и таймер испытания. Гарпуны, сундук и знак вынесены в отдельные компоненты. Наличие кода не подтверждает игровую или сетевую приёмку.

- [Assets/Scripts/WhalePOI/WhaleLootPoint.cs](<Assets/Scripts/WhalePOI/WhaleLootPoint.cs>) — Исходник C#: WhalePOIState, WhaleLootPoint.
- [Assets/Scripts/WhalePOI/WhaleChest.cs](<Assets/Scripts/WhalePOI/WhaleChest.cs>) — Исходник C#: WhaleChest.
- [Assets/Scripts/WhalePOI/WhaleHarpoonPin.cs](<Assets/Scripts/WhalePOI/WhaleHarpoonPin.cs>) — Исходник C#: WhaleHarpoonPin, HarpoonWaterImpact.
- [Assets/Scripts/WhalePOI/WhaleYellowSign.cs](<Assets/Scripts/WhalePOI/WhaleYellowSign.cs>) — Исходник C#: WhaleYellowSign.

### Рыбалка и рыба (`fishing`)

Ключевые слова: рыбалка, рыба.

Катушка собрана из предоставленных ReelMount/ReelMechanism через Blender MCP. Неподвижное крепление, отдельный ReelSpoolPivot со шпулей, намоткой и рукояткой; леска проходит через шесть существующих колец модели, TipLocalPoint(0,.133,1.69). FishingRodReel вращает шпулю обратно при забросе, вперёд при фактическом вываживании, останавливает при отпускании ЛКМ/ожидании. NetworkFishing.reelActive синхронизирует серверное состояние намотки; обезьянка использует существующий FishingPhase. BendPoint совмещает леску с деформацией модели. Общий цвет лески и тонкие LineRenderer, pickup получает статичную сборку. Исходники/упакованные карты в FishingRodAssembly.blend и FishingReelSources.blend; повторная сборка AssembleFishingReel.py. ProtocolVersion136; PlayMode и два клиента не запускались.
Удочка заменена моделью из Desktop/3d/удочка через Blender MCP: нормализованный хват, направление +Z, кончик в(0,.15,1.7) для штатной лески и FishingRodBend. FishingRodReplacementSetup меняет геометрию FishingRod и DroppedRod с сохранением GUID, назначает URP/PBR и обновляет коллайдер по модели. Player NetworkFishing и ShipMonkey используют общий FishingRod; импорт FBX Read/Write сохраняет runtime-изгиб. FishingSetup повторно применяет замену после настройки рыбалки. Нормализованный BLEND и исходные FBX/карты сохранены в Art/Blender/FishingRod. Сетевая логика/протокол не менялись; игровой и онлайн-тест выполняет пользователь.
Рыбалка — NetworkFishing; предмет рыбы и его полёт — NetworkFish и NetworkFishProjectile. Использование рыбы как метательного предмета находится в NetworkWeapon.FishThrows.
Модели обычной рыбы, фугу и рыбы-меча заменены файлами из ../Blender/Лутабельные/Рыба. Исходники в Art/Blender/Loot/FishReplacement, игровые FBX и материалы URP в Assets/Models/Loot/Replacement/Fish, Pufferfish, Swordfish. BottleFishReplacementSetup сохраняет существующие FishVisual, PufferfishVisual и SwordfishVisual GUID и подменяет дочернюю геометрию двух специальных pickup. Коллайдеры, NetworkFish, NetworkFishProjectile, направление головы +Z, подбор, рыбалка, броски, раздувание фугу и втыкание рыбы-меча сохранены. Игровая проверка выполняется пользователем.
Уточнение заменённых моделей: Swordfish и Pufferfish Geometry rotation Y=-90° с сохранением FBX-преобразования Z-up в Y-up; после смены базиса визуал повторно центрируется. Переносимый визуал без дополнительного Y=90°, смещение и хват соответствуют телу. NetworkFishProjectile раздувает поперечные оси фугу до 2.1x и учитывает любой Renderer.
NetworkFishMotion: обычная рыба, фугу и рыба-меч на палубе извиваются и небольшими серверными прыжками направляются к ближайшей посадочной сетке ShipLadder.BoardingAccess. Прыжки считаются относительно корабля, при потере опоры и выходе за борт наследуется скорость корабля. Проверка движения использует габариты визуала, BoxCast и дополнительные лучи опоры, а посадка учитывает центр и нижнюю точку модели. При касании воды слышен всплеск, рыба уплывает вниз с вилянием хвоста и удаляется через 3 секунды; состояние и таймер задаёт сервер. Старое удаление обычной рыбы через 600 секунд на палубе отключено. Воткнутая рыба-меч имеет NetworkFishProjectile.Stuck и периодически виляет только хвостом. Все три рыбы при сбросе сразу укладываются на бок через LootPlacement; коллайдеры подогнаны под реальный визуал. У фугу исправлено направление головы по +Z в общем визуале, включая инвентарь и переносимый улов. Модели рыб импортируются с Read/Write для деформации отдельных runtime-копий мешей. Игровая и онлайн-проверка выполняется пользователем.
Отскок иглобрюха рассчитывает центр по расстоянию SphereCast, а не hit.point: при начальном пересечении нулевая точка контакта не переносит рыбу в центр мира. Фитиль2.5с и штатный взрыв сохранены.

- [Assets/Scripts/Player/FishingRodReel.cs](<Assets/Scripts/Player/FishingRodReel.cs>) — Исходник C#: FishingRodReel.
- [Art/Blender/FishingRod/AssembleFishingReel.py](<Art/Blender/FishingRod/AssembleFishingReel.py>) — Инструмент Python.
- [Art/Blender/FishingRod/FishingRodAssembly.blend](<Art/Blender/FishingRod/FishingRodAssembly.blend>) — Редактируемая сцена Blender.
- [Art/Blender/FishingRod/FishingReelSources.blend](<Art/Blender/FishingRod/FishingReelSources.blend>) — Редактируемая сцена Blender.
- [Art/Blender/FishingRod/README.md](<Art/Blender/FishingRod/README.md>) — Документация.
- [Art/Blender/FishingRod/Sources/ReelMount/ReelMountSource.fbx](<Art/Blender/FishingRod/Sources/ReelMount/ReelMountSource.fbx>) — Модель / анимации FBX.
- [Art/Blender/FishingRod/Sources/ReelMechanism/ReelMechanismSource.fbx](<Art/Blender/FishingRod/Sources/ReelMechanism/ReelMechanismSource.fbx>) — Модель / анимации FBX.
- [Assets/Models/Fishing/Replacement/Textures/ReelMountBaseColor.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMountBaseColor.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMountNormal.png](<Assets/Models/Fishing/Replacement/Textures/ReelMountNormal.png>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMountMetallic.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMountMetallic.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMountRoughness.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMountRoughness.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMountMetalSmooth.png](<Assets/Models/Fishing/Replacement/Textures/ReelMountMetalSmooth.png>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMechanismBaseColor.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMechanismBaseColor.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMechanismNormal.png](<Assets/Models/Fishing/Replacement/Textures/ReelMechanismNormal.png>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMechanismMetallic.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMechanismMetallic.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMechanismRoughness.jpg](<Assets/Models/Fishing/Replacement/Textures/ReelMechanismRoughness.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/ReelMechanismMetalSmooth.png](<Assets/Models/Fishing/Replacement/Textures/ReelMechanismMetalSmooth.png>) — Изображение / текстура.
- [Assets/Materials/Fishing/ReelMount.mat](<Assets/Materials/Fishing/ReelMount.mat>) — Материал Unity.
- [Assets/Materials/Fishing/ReelMechanism.mat](<Assets/Materials/Fishing/ReelMechanism.mat>) — Материал Unity.
- [Assets/Materials/Fishing/ReelBrass.mat](<Assets/Materials/Fishing/ReelBrass.mat>) — Материал Unity.
- [Assets/Materials/Fishing/ReelThread.mat](<Assets/Materials/Fishing/ReelThread.mat>) — Материал Unity.
- [Assets/Materials/Fishing/ReelGrip.mat](<Assets/Materials/Fishing/ReelGrip.mat>) — Материал Unity.
- [Assets/Scripts/Editor/FishingRodReplacementSetup.cs](<Assets/Scripts/Editor/FishingRodReplacementSetup.cs>) — Исходник C#: FishingRodReplacementSetup.
- [Assets/Models/Fishing/FishingRod.prefab](<Assets/Models/Fishing/FishingRod.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/DroppedRod.prefab](<Assets/Prefabs/Networking/DroppedRod.prefab>) — Префаб Unity.
- [Assets/Models/Fishing/Replacement/FishingRodReplacement.fbx](<Assets/Models/Fishing/Replacement/FishingRodReplacement.fbx>) — Модель / анимации FBX.
- [Assets/Models/Fishing/Replacement/Textures/FishingRodBaseColor.jpg](<Assets/Models/Fishing/Replacement/Textures/FishingRodBaseColor.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/FishingRodNormal.png](<Assets/Models/Fishing/Replacement/Textures/FishingRodNormal.png>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/FishingRodMetallic.jpg](<Assets/Models/Fishing/Replacement/Textures/FishingRodMetallic.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/FishingRodRoughness.jpg](<Assets/Models/Fishing/Replacement/Textures/FishingRodRoughness.jpg>) — Изображение / текстура.
- [Assets/Models/Fishing/Replacement/Textures/FishingRodMetalSmooth.png](<Assets/Models/Fishing/Replacement/Textures/FishingRodMetalSmooth.png>) — Изображение / текстура.
- [Assets/Materials/Fishing/FishingRodReplacement.mat](<Assets/Materials/Fishing/FishingRodReplacement.mat>) — Материал Unity.
- [Art/Blender/FishingRod/FishingRodReplacement.blend](<Art/Blender/FishingRod/FishingRodReplacement.blend>) — Редактируемая сцена Blender.
- [Art/Blender/FishingRod/Sources/FishingRodSource.fbx](<Art/Blender/FishingRod/Sources/FishingRodSource.fbx>) — Модель / анимации FBX.
- [Assets/Scripts/Networking/NetworkFishing.cs](<Assets/Scripts/Networking/NetworkFishing.cs>) — Исходник C#: NetworkFishing.
- [Assets/Scripts/Networking/NetworkFish.cs](<Assets/Scripts/Networking/NetworkFish.cs>) — Исходник C#: InventoryItem, NetworkFish.
- [Assets/Scripts/Networking/NetworkFishMotion.cs](<Assets/Scripts/Networking/NetworkFishMotion.cs>) — Исходник C#: NetworkFish, BodyMesh.
- [Assets/Scripts/Networking/LootPlacement.cs](<Assets/Scripts/Networking/LootPlacement.cs>) — Исходник C#: LootPlacement.
- [Assets/Scripts/Networking/NetworkFishProjectile.cs](<Assets/Scripts/Networking/NetworkFishProjectile.cs>) — Исходник C#: NetworkFishProjectile.
- [Assets/Scripts/Networking/NetworkWeapon.FishThrows.cs](<Assets/Scripts/Networking/NetworkWeapon.FishThrows.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Player/FishingRodBend.cs](<Assets/Scripts/Player/FishingRodBend.cs>) — Исходник C#: FishingRodBend, Part.
- [Assets/Scripts/Editor/FishingSetup.cs](<Assets/Scripts/Editor/FishingSetup.cs>) — Исходник C#: FishingSetup.
- [Assets/Scripts/Editor/BottleFishReplacementSetup.cs](<Assets/Scripts/Editor/BottleFishReplacementSetup.cs>) — Исходник C#: BottleFishReplacementSetup.
- [Assets/Models/Fishing/FishVisual.prefab](<Assets/Models/Fishing/FishVisual.prefab>) — Префаб Unity.
- [Assets/Models/FishingWeapons/PufferfishVisual.prefab](<Assets/Models/FishingWeapons/PufferfishVisual.prefab>) — Префаб Unity.
- [Assets/Models/FishingWeapons/SwordfishVisual.prefab](<Assets/Models/FishingWeapons/SwordfishVisual.prefab>) — Префаб Unity.
- [Assets/Models/FishingWeapons/PufferfishPickup.prefab](<Assets/Models/FishingWeapons/PufferfishPickup.prefab>) — Префаб Unity.
- [Assets/Models/FishingWeapons/SwordfishPickup.prefab](<Assets/Models/FishingWeapons/SwordfishPickup.prefab>) — Префаб Unity.

### Тестовая карта и водоворот (`test-gallery`)

Ключевые слова: тестовая карта, галерея, водоворот, whirlpool.

В NetworkOcean водоворот всегда активен в центре: OceanSurface.CentralWhirlpoolRadius=500 м (вдвое прежнего), CentralWhirlpoolDepth=120 м. StormZone больше не запускает его в финале и не создаёт WhirlpoolVFX. SeabedTerrain формирует локальное углубление радиусом 625 м с запасом под воронкой, CPU и mesh используют одну функцию. WaterGridGenerator больше не создаёт 81 сферу WaterBuoy в меню и удаляет старые объекты по точным именам. SimpleWater исключает небо из depth-пены, проверяет расстояние до контакта геометрии и подавляет береговую пену внутри и у края воронки. В SimpleWater вращающаяся рябь и три разорванные спиральные полосы пены показывают течение внутри воронки по синхронизированному времени; эффект плавно исчезает до края. Нормали учитывают наклон воронки. Старые эффекты каймы и затемнение убраны; bounds воды учитывают глубокую воронку. WhirlpoolTest использует радиус 500 м без частиц, глубину 45 м для отдельной тестовой площадки. Точка входа тестовой карты — EnvironmentTestGallery, подготовка — EnvironmentTestSetup. Водоворот имеет отдельные файлы поведения и визуала; не путать этот режим со старой SampleScene.
Ship V3 используется как основной корабль и в тестовой карте, и в обычной игре, через общий спавн SessionController. ShipV3TestSpawner оставлен как исторический исходник без вызова из сессии; отдельный лишний корабль больше не создаётся. Старый ShipSkeletonTest ранее удалён. ShipV3Features синхронизирует фонари, рынду, принадлежность кубиков, физические подвесы и гарпуны; игровой онлайн-прогон не выполнялся.
Первое падение пользовательской сборки произошло при Resources.Load ShipV3Test до создания корабля: D3D11 8007000e при выделении текстуры 4096×4096. До ограничения импорта 91 зависимая текстура Ship V3 занимала около 2,20 ГБ по измерению редактора. После переимпорта с пределами 2048 для цвета/эмиссии и 1024 для нормалей/данных, сжатием Standalone, mipmaps и отключённым Read/Write размер по тому же измерению снизился с 2198252896 до 659993344 байт (примерно на 70%); нет текстур выше 2048 и нормалей выше 1024.
Последующий пользовательский запуск дошёл до отображения нового корабля и завершился native-падением из-за нехватки памяти: Mimalloc fatal error (12), ENOMEM, BeginRenderQueueExtraction → PrepareDrawShadowsCommandStep1 → Submit_Internal. Новая локальная оптимизация Ship V3 уменьшает объекты отрисовки и первоначальную нагрузку коллайдеров, сохраняя модель, UV, текущие текстуры и игровые механизмы. Native inspection сохранённых ассетов подтвердил одну запись Ship V3 в сетевом реестре, отсутствие групп ShipV3RenderBatch и ленивых секций на основном корабле; NetworkMenu на момент проверки не имел несохранённых изменений. Выполнена только edit-mode preview проверка геометрии и одной секции разрушения/ремонта. Сборка, Play Mode и онлайн после оптимизации не запускались; устранение native-падения пока не подтверждено.
ShipV3GameplayRepair добавляет точку игры в кости непосредственно бочке, поднимает стаканы и кубики на её поверхность и создаёт 18 физических ограничителей по краю. Сервер ограничивает броски радиусом бочки, закрепляет результат относительно корабля и проверяет владельца места. Рында: connectedAnchor язычка совмещён с исходным подвесом, enableCollision включён, ударник соответствует нижней части модели, углы ограничены по расстоянию до внутренних стенок. Звон остаётся от контакта и рассылается существующим BellSound; правило трёх ударов для воскресения сохранено. Сетевой ProtocolVersion повышен до 110; для совместной игры нужны одинаковые новые сборки. Изменения сохранены через Unity MCP, компиляция без ошибок; игровой онлайн-прогон не выполнялся.
Вход в кости по F доступен в пределах 3.2 м от бочки при прямой видимости; точное наведение на кубик или верхнюю крышку не требуется. ShipV3Features.Active используется для выбора ближайшего стола. Target Dice добавлен всей V17_Dice_Game_Station; CanReachDice одинаково проверяет клиента, JoinDice, ввод и удержание серверного места, игнорируя собственную мебель станции. Локальное движение блокируется сразу при запросе входа, до синхронизации владельца, чтобы F не запускала одновременно другую механику.
Неподключённые WhirlpoolVFX.cs и ShipV3TestSpawner.cs удалены по Git-аудиту 2026-10-05; действующие Resources/EnvironmentTest/Gallery.prefab, Resources/Ships/ShipV3Test.prefab и игровая загрузка тестовой карты сохранены.
Основная тестовая карта: TestSkyDayNight через EnvironmentTestGallery создаёт день, закат и лунную ночь; F8 управляет плавным переходом0..1. TestSkyPipeline/Renderer/Volume и отдельный материал облаков; PBSky1.0.4 MIT дляUnity6000.6, облакаCandidateA. Moon .30, видимыйазимут125°, высота24°, skyExposure−3.5EV, ambientFloor(.25,.29,.38).linear x .5. Сумеречный fill (.30,.29,.30) растёт в linear на .28–.48 до ухода солнца под горизонт, с .58 переходит в более слабый ночной свет. Boat Attack near/far отражает динамический skyCube; PBSky Fog связан с F8 морским туманом. Обычная игра сохраняет прежнийpipeline; свет/отражения/туман восстанавливаются при выходе.
Дополнительная коррекция Boat Attack после проверки exe: Gerstner peak=2 создавал отрицательный горизонтальный якобиан (на реальных шести волнах det min−.455, 2.20% перевёрнутых точек). В GerstnerWaves.hlsl, GerstnerWaves.cs и BoatAttackOcean.CacheWaves согласованно установлен peak=.85; вертикальная амплитуда сохранена. J=I−(peak/N)Σsin(phase)d dᵀ, поэтому λmin≥.15 для любых направлений/фаз, для текущего спектра≥.28376. Повторная сборка и приёмка завершены, результат ниже.
Основная Тестовая карта запускается через EnvironmentTestGallery внутри NetworkOcean. Resources/EnvironmentTest/Ocean.prefab сохраняет настройки Boat Attack; TestOceanController подключает адаптер к единственному игровому OceanSurface, скрывает прежний MeshRenderer и восстанавливает его в OnDisable до следующей генерации. Ресурс содержит instanced BoatAttackWater.mat для Player.
Стартовая ориентация yaw180 (от галереи в открытое море); центр воронки=Spawn+(0,0,−550), R500/D120/Twist2, ближний край50м. Другие корабли разнесены поперёк по−X65м. Layout.Radius≥2000м обеспечивает доступ ко всей чаше. Глубина дна200м, SeabedTerrain углубляет чашу под тем же центром.
F8: сила волн0..200%, крутизна0..100%, скорость.25..2x и сброс. CPU/GPU вертикаль×strength, горизонталь peak.85×min(strength,1)×steepness; speed меняет непрерывные часы, высота0 не делит на ноль. Волны меняет хост; SessionOcean передаёт параметры и общие clockanchor/value клиентам, включая подключившихся позднее.
TestSky: HDR grading/LUT32, Neutral, Contrast6/Saturation−4, Bloomthreshold1.2/intensity.15/scatter.55/clamp8/Half, SSAOdirectStrength.15. Boat Water near/far используют динамический skyCube; ночью пена×.45/scatter×.65. Прозрачная вода применяет тот же PBSky atmospheric fog в fragment через Lighting.w, без второго fullscreen pass.
TestSkyPipeline зарегистрирован для сбора shader features через IncludeAdditionalRPAssets/includeAssetsByLabel и метку PirateSlopRuntimePipeline. Без этого URP удаляет HDR_GRADING для динамического переключения pipeline: в Player белое небо и резкий контраст, хотя Editor работает. TestSkySetup.ConfigureLook поддерживает регистрацию.
WaterTestCapture: -environmenttest -watercapture включает12GPUкадров в Player, включая нулевые/сильные волны, закат и ночь. StandardRequest использует полный RenderCameraStack с автоматическим Volume update после создания актуального pipeline. Диагностические profile/stack EV сравниваются после рендера; SingleCameraRequest пропускал штатный update.
Динамическая пена Boat Attack: 38 проб измеренной ватерлинии, мировая история контактов2.8с и следа12с; 2048² ARGBHalf/512м хранит гладкую плотность, hull mask и высоту V-гребня≤.22м; CPU и atlas shader используют общую форму, ShipController исключает собственный гребень из качки, фактура берётся из общего FoamMap. Носовые плечи по сечениям14–16 и масса перед форштевнем растут с реальной скоростью относительно воды, пакеты живут1.4с и расходятся1м/с. WaterBowSpray запускается от wet×положительного подъёма воды >1.25м/с; cooldown.6с, gravity1, мировые капли с округлым AA-шейдером возвращаются в воду. F8 содержит локальные косметические ползунки носовой пены и брызг0..200%. Подводный medium определяется по фактической Height с волнами/чашей; UnderwaterRendererFeature копирует актуальную глубину после воды и перед postprocessing применяет один Beer pass, затем редкую взвесь. Воздушные PBSky fog/SeaMist под водой отключены. -waterdetailcapture проверяет близкий борт/след, скорости/удары/ночь, переход среды, чашу и POI5/15/30/60м.
Качка ShipController использует ShipBuoyancy:9 продольных сечений×3 поперечных точки, weighted plane-fit по площади измеренного корпуса. Высота берётся из intercept плоскости в центре корабля с учётом смещённого weighted meanZ; pitch/roll из её уклонов. Четыре края±18/±5.5 раньше давали spatial alias на волне32м и могли менять знак наклона. Сохранены сглаживаниеexp, clamps12/15deg, Flooding/Cannon и сетевой ShipState. Проверены16 аналитических плоскостей при yaw0/90/180/270: height error<7.2e−7, normal dot≥.99999994. Дляsin(2πz/32) oldPitch+1.218deg, new−3.528deg.
WaterImpactPhysics/WaterImpactBody: общие локальные всплески входа в actual OceanSurface для физических тел, игроков без Rigidbody и скриптовых предметов. Swept crossing промежуточных точек/уточнение; ядро передаёт incoming velocity при входе в воду и продолжает подводный полёт. Нормальная скорость, масса и площадь задают капли/пену; человек вверх, ядро по касательной. Dynamic Rigidbody.GetPointVelocity, кинематика по времени фактического перемещения. Rearm после .25м/.25с над поверхностью, спавн под водой и прыжок позиции игрока не создают всплеск. 64 истории splash-ring в общем foam atlas; единые 600капель/с,1200живых,2burst/.1с. Fish/bottle/grenade RPC передают вход до despawn; сетевой ProtocolVersion124. Носовой spray на текущей высоте воды переносится raycast на наружную обшивку с запасом10см. Обычный Build Windows06.10.2026:0 ошибок; Player32кадра,6событий без повторов/исключений; итоговая VFX-приёмка [APPROVED]. Свободный подводный верх без белого потолка, POI5–30м читается;60м частично закрыт настоящим дном. Два сетевых клиента не проверялись.
Облака 2026-10-07, фикс после повторной игровой жалобы: удалены volumeShape, smooth union и узнаваемая тройка эллипсоидов. Непрерывный warped weather только размещает редкие группы; сама плотность — remap двух red-выборок линейной Texture3D Worley128 с разными осями/offset и octave2.07, мягкий переменный height-gradient, настоящая erosion и micro erosion. R8 шумы проверены GPU-readback всех слоёв: Worley128³ mean.753/std.118, Perlin32³ mean.500/std.123, оба не-константны/sRGBfalse; G/B/A не используются. samplingNormalization100000, shapeScale6 даёт период16.67км вместо37км. Material coverageStart.54/end.76, cell7000/seed19/farFade12000–28000; profile density.34, shape6/.75, erosion90/.5, microtrue/.28/200, primary64/light6. ShapeLOD<=2/detailLOD<=1 только CLEAR, вертикальные края строго нулевые с защищённым remap denominator. Authoring CPU расчёт43 296 точек:19.29% world columns живые,6.88% точек с промежуточной density, насыщенных0, liveMean.05259/max.16781; это не доля пикселей неба и не игровой рендер. Общие worldOffset/wind, Main/Shadow/Cubemap, fog/солнце/grading сохранены; legacy CandidateA не менялся. Native профиль/материал/keywords сохранены после импорта; shader и console без ошибок, сцена не сохранялась; игровая визуальная приёмка не проводилась.
Брызги ядер 2026-10-06, повторный фикс после жалобы: быстрый вход с tangential energy>=100 допускает splash при closing=0. CannonShotDamage сохраняет launchedAboveWater и сообщает пропущенный вход, когда волна накрыла ядро между FixedUpdate; incomingVelocity берётся до подводного damping, оба пути защищены waterEntered от повторных burst при колебании волны; старт под водой не создаёт новый всплеск. Projectile Lift14–20м/с, Life2.8–4с. WaterBowSpray выделяет треть крупных капель .09–.16м с alpha1 и узким вертикальным plume, остальные .035–.075м; y-скорость минимум .85*Lift, нормаль смешана сup. Shader усиливает центральную видимость только alpha>.7–.9; обычный носовой spray сохранён. Native renderer включает Water layer4, shader существует/support=true. Wet contacts/палубные следы сохранены; игровой вид не проверялся.

- [Assets/Scripts/World/EnvironmentTestGallery.cs](<Assets/Scripts/World/EnvironmentTestGallery.cs>) — Исходник C#: EnvironmentTestGallery.
- [Assets/Scripts/Editor/EnvironmentTestSetup.cs](<Assets/Scripts/Editor/EnvironmentTestSetup.cs>) — Исходник C#: EnvironmentTestSetup.
- [Assets/Scripts/World/WhirlpoolTest.cs](<Assets/Scripts/World/WhirlpoolTest.cs>) — Исходник C#: WhirlpoolTest.
- [Assets/Scripts/World/SeabedTerrain.cs](<Assets/Scripts/World/SeabedTerrain.cs>) — Исходник C#: SeabedTerrain.
- [Assets/Scripts/WaterGridGenerator.cs](<Assets/Scripts/WaterGridGenerator.cs>) — Исходник C#: WaterGridGenerator.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
- [Assets/Scripts/World/StormZone.cs](<Assets/Scripts/World/StormZone.cs>) — Исходник C#: StormZone.
- [Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader](<Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader>) — Шейдер.
- [Assets/Scripts/Ships/ShipV3PlayerInteraction.cs](<Assets/Scripts/Ships/ShipV3PlayerInteraction.cs>) — Исходник C#: ShipV3PlayerInteraction.
- [Assets/Scripts/Ships/ShipV3InteractionTarget.cs](<Assets/Scripts/Ships/ShipV3InteractionTarget.cs>) — Исходник C#: ShipV3InteractionTarget.
- [Assets/Scripts/Ships/ShipV3BellContact.cs](<Assets/Scripts/Ships/ShipV3BellContact.cs>) — Исходник C#: ShipV3BellContact.
- [Assets/Scripts/Ships/ShipV3ClothMotion.cs](<Assets/Scripts/Ships/ShipV3ClothMotion.cs>) — Исходник C#: ShipV3ClothMotion.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Scripts/Editor/ShipV3GameplayRepair.cs](<Assets/Scripts/Editor/ShipV3GameplayRepair.cs>) — Исходник C#: ShipV3GameplayRepair.
- [Assets/Scripts/World/BoatAttackOcean.cs](<Assets/Scripts/World/BoatAttackOcean.cs>) — Исходник C#: BoatAttackOcean, SpectralWave.
- [Assets/Scripts/World/OceanHeightSource.cs](<Assets/Scripts/World/OceanHeightSource.cs>) — Исходник C#: OceanHeightSource.
- [Assets/Settings/PC_Renderer.asset](<Assets/Settings/PC_Renderer.asset>) — Настройки или данные Unity.
- [Assets/Scripts/World/TestSkyDayNight.cs](<Assets/Scripts/World/TestSkyDayNight.cs>) — Исходник C#: TestSkyDayNight.
- [Assets/Scripts/Editor/TestSkySetup.cs](<Assets/Scripts/Editor/TestSkySetup.cs>) — Исходник C#: TestSkySetup.
- [Assets/Scripts/Player/DeveloperMenu.cs](<Assets/Scripts/Player/DeveloperMenu.cs>) — Исходник C#: DeveloperMenu.
- [Assets/Resources/EnvironmentTest/SkyDayNight.prefab](<Assets/Resources/EnvironmentTest/SkyDayNight.prefab>) — Префаб Unity.
- [Assets/Settings/TestSky/TestSkyRenderer.asset](<Assets/Settings/TestSky/TestSkyRenderer.asset>) — Настройки или данные Unity.
- [Assets/Settings/TestSky/TestSkyPipeline.asset](<Assets/Settings/TestSky/TestSkyPipeline.asset>) — Настройки или данные Unity.
- [Assets/Settings/TestSky/TestSkyVolume.asset](<Assets/Settings/TestSky/TestSkyVolume.asset>) — Настройки или данные Unity.
- [Assets/Settings/TestSky/TestSkyClouds.mat](<Assets/Settings/TestSky/TestSkyClouds.mat>) — Материал Unity.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Runtime/PhysicallyBasedSkyURP.cs](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Runtime/PhysicallyBasedSkyURP.cs>) — Исходник C#: PhysicallyBasedSkyURP, PrecomputationQualityMode, CelestialBodyData, PBSkyPrePass, PassData, SkyViewLUTPass, AtmosphericScatteringPass, PBSkyPostPass, AmbientProbePass.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Runtime/PhysicallyBasedSkyVolume.cs](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Runtime/PhysicallyBasedSkyVolume.cs>) — Исходник C#: PhysicallyBasedSky, PhysicallyBasedSkyModel, EnvironmentUpdateMode, SkyIntensityMode, SkyIntensityParameter, EnvUpdateParameter, PhysicallyBasedSkyModelParameter.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/LICENSE.md](<Packages/com.jiaozi158.unity-physically-based-sky-urp/LICENSE.md>) — Документация.
- [Assets/Scripts/Editor/SteamTestBuild.cs](<Assets/Scripts/Editor/SteamTestBuild.cs>) — Исходник C#: SteamTestBuild.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSky.shader](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSky.shader>) — Шейдер.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSkyRendering.hlsl](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/PhysicallyBasedSkyRendering.hlsl>) — Код шейдера.
- [Assets/Scripts/World/WaterShipFoam.cs](<Assets/Scripts/World/WaterShipFoam.cs>) — Исходник C#: WaterShipFoam, HullState, Packet, Hit, SurfaceImpact.
- [Assets/Shaders/WaterShipFoam.hlsl](<Assets/Shaders/WaterShipFoam.hlsl>) — Код шейдера.
- [Assets/Scripts/World/WaterTestCapture.cs](<Assets/Scripts/World/WaterTestCapture.cs>) — Исходник C#: WaterTestCapture.
- [Assets/Settings/WaterTests/BoatAttackWater.mat](<Assets/Settings/WaterTests/BoatAttackWater.mat>) — Материал Unity.
- [Assets/Scripts/Editor/MultiplayerSceneSetup.cs](<Assets/Scripts/Editor/MultiplayerSceneSetup.cs>) — Исходник C#: MultiplayerSceneSetup.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/GerstnerWaves.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/GerstnerWaves.hlsl>) — Код шейдера.
- [Packages/com.unity.urp-water-system/Runtime/Modifiers/GerstnerWaves.cs](<Packages/com.unity.urp-water-system/Runtime/Modifiers/GerstnerWaves.cs>) — Исходник C#: GerstnerWaves, HeightJob, Data, WaveType, JobData, BasicWaves, Wave, WaveDescriptor.
- [Assets/Resources/EnvironmentTest/Ocean.prefab](<Assets/Resources/EnvironmentTest/Ocean.prefab>) — Префаб Unity.
- [Assets/Scripts/World/TestOceanController.cs](<Assets/Scripts/World/TestOceanController.cs>) — Исходник C#: TestOceanController.
- [Packages/com.unity.urp-water-system/Runtime/Shaders/WaterLighting.hlsl](<Packages/com.unity.urp-water-system/Runtime/Shaders/WaterLighting.hlsl>) — Код шейдера.
- [Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/AtmosphericScattering.hlsl](<Packages/com.jiaozi158.unity-physically-based-sky-urp/Shaders/AtmosphericScattering.hlsl>) — Код шейдера.
- [Assets/Scripts/Networking/SessionOcean.cs](<Assets/Scripts/Networking/SessionOcean.cs>) — Исходник C#: TestOceanMessage, SessionController.
- [Assets/Settings/UniversalRenderPipelineGlobalSettings.asset](<Assets/Settings/UniversalRenderPipelineGlobalSettings.asset>) — Настройки или данные Unity.
- [Assets/Resources/EnvironmentTest/ShipFoamAtlas.shader](<Assets/Resources/EnvironmentTest/ShipFoamAtlas.shader>) — Шейдер.
- [Assets/Scripts/ShipBuoyancy.cs](<Assets/Scripts/ShipBuoyancy.cs>) — Исходник C#: ShipBuoyancy.
- [Assets/Scripts/World/UnderwaterRendererFeature.cs](<Assets/Scripts/World/UnderwaterRendererFeature.cs>) — Исходник C#: UnderwaterRendererFeature, ImmersionPass, FogData, SuspensionData.
- [Assets/Scripts/World/UnderwaterEnvironment.cs](<Assets/Scripts/World/UnderwaterEnvironment.cs>) — Исходник C#: UnderwaterEnvironment.
- [Assets/Scripts/World/WaterBowSpray.cs](<Assets/Scripts/World/WaterBowSpray.cs>) — Исходник C#: WaterBowSpray.
- [Assets/Resources/EnvironmentTest/UnderwaterImmersion.shader](<Assets/Resources/EnvironmentTest/UnderwaterImmersion.shader>) — Шейдер.
- [Assets/Resources/EnvironmentTest/UnderwaterSuspension.shader](<Assets/Resources/EnvironmentTest/UnderwaterSuspension.shader>) — Шейдер.
- [Assets/Resources/EnvironmentTest/WaterBowSpray.shader](<Assets/Resources/EnvironmentTest/WaterBowSpray.shader>) — Шейдер.
- [Assets/Settings/TestSky/Underwater.mat](<Assets/Settings/TestSky/Underwater.mat>) — Материал Unity.
- [Assets/Scripts/World/WaterImpactPhysics.cs](<Assets/Scripts/World/WaterImpactPhysics.cs>) — Исходник C#: WaterImpactKind, WaterImpactEvent, WaterImpactPhysics.
- [Assets/Scripts/World/WaterImpactBody.cs](<Assets/Scripts/World/WaterImpactBody.cs>) — Исходник C#: WaterImpactBody.
- [Assets/Scripts/World/WaterSplashContacts.cs](<Assets/Scripts/World/WaterSplashContacts.cs>) — Исходник C#: WaterSplashContacts.
- [Assets/Resources/EnvironmentTest/WaterWetMark.shader](<Assets/Resources/EnvironmentTest/WaterWetMark.shader>) — Шейдер.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.shader](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricClouds.shader>) — Шейдер.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsDefs.hlsl](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsDefs.hlsl>) — Код шейдера.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsUtilities.hlsl>) — Код шейдера.
- [Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsURP.cs](<Assets/Tests/StormCloudBakeoff/CandidateA/VolumetricClouds/VolumetricCloudsURP.cs>) — Исходник C#: VolumetricCloudsURP, CloudsRenderMode, CloudsAmbientMode, CloudsUpscaleMode, VolumetricCloudsPass, stores, PassData, RasterPassData, VolumetricCloudsAmbientPass, VolumetricCloudsShadowsPass, LightCookieShaderFormat.
- [Docs/TestOcean.md](<Docs/TestOcean.md>) — Документация.
- [Docs/TestSkyDayNight.md](<Docs/TestSkyDayNight.md>) — Документация.

### Производительность и тест нагрузки без AI (`performance`)

Ключевые слова: оптимизация, FPS, профайлер, NetworkLoadTest.

NetworkLoadTest запускается из меню Тест нагрузки · без AI, отдельной сцены или аргумента -loadtest. Использует текущий MaxPlayers, реальные корабли и персонажей, seed 41719, близкие спавны, простые движения без принятия решений и поиска пути; F7 переключает движение. Локальный человек заменяет одного синтетического участника. Зона не сужается. Это нагрузка локального хоста, а не эмуляция 30 сетевых соединений.
Общие оптимизации: активный реестр кораблей вместо поиска всей сцены в снимках персонажей; кэш blend shapes и неизменных поз; центры канатов вместо BakeMesh для простых blend shapes; визуальный такт 10/4 Гц дальше 80/200 м; локальные фонари до 100 м; повторное использование сетевых массивов; проверки визуала по событиям с редким распределённым fallback. Формат RPC и ProtocolVersion сохранены. Компиляция и сохранение сцены не доказывают FPS и мультиплеер.

- [Assets/Scripts/Networking/SessionLoadTest.cs](<Assets/Scripts/Networking/SessionLoadTest.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Editor/NetworkLoadTestSetup.cs](<Assets/Scripts/Editor/NetworkLoadTestSetup.cs>) — Исходник C#: NetworkLoadTestSetup.
- [Assets/Scenes/NetworkLoadTest.unity](<Assets/Scenes/NetworkLoadTest.unity>) — Сцена Unity.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Ships/ShipV3VisualRig.cs](<Assets/Scripts/Ships/ShipV3VisualRig.cs>) — Исходник C#: ShipV3Pose, ShipV3Motion, ShipV3VisualRig.
- [Assets/Scripts/Interaction/RopeTubeVisual.cs](<Assets/Scripts/Interaction/RopeTubeVisual.cs>) — Исходник C#: RopeTubeVisual, Centerline.
- [Assets/Scripts/Ships/ShipV3RenderBudget.cs](<Assets/Scripts/Ships/ShipV3RenderBudget.cs>) — Исходник C#: ShipV3RenderBudget.
- [Assets/Scripts/Ships/ShipV3Features.cs](<Assets/Scripts/Ships/ShipV3Features.cs>) — Исходник C#: ShipV3TargetKind, ShipV3Lantern, ShipV3DiceSlot, ShipV3PhysicsPose, ShipV3Support, ShipV3Attachment, ShipV3Features.
- [Assets/Scripts/Ships/ShipV3RenderBatch.cs](<Assets/Scripts/Ships/ShipV3RenderBatch.cs>) — Исходник C#: ShipV3RenderBatch.
- [Assets/Scripts/UI/GameTelemetry.cs](<Assets/Scripts/UI/GameTelemetry.cs>) — Исходник C#: GameTelemetry.
- [Docs/Performance/NetworkLoadTest.md](<Docs/Performance/NetworkLoadTest.md>) — Документация.

### Корабельная обезьянка (`ship-monkey`)

Ключевые слова: обезьяна, обезьянка, миньон, monkey.

Текущая локомоция двуногая: Idle/Walk/RailWalk/ClimbUp/ClimbDown точно копируют первоначальные Legacy actions, Run ускоряет LegacyWalk. Балансирование на бортах восстановлено. Четвероногие клипы и прежний прыжок убраны из игрового FBX ; архив Versions/V4-QuadrupedJump-2026-10-04 удалён при согласованной очистке 2026-10-05. Меню четвероногой локомоции удалено.
Сидение, отдых у моря и редкие взгляды сохранены. Переходы посадки/подъёма и три прыжковых клипа переделаны под вертикальную стойку. Веса глаз/лица исходные, взгляд только головой/шеей с пределами 30°/15°. 29 action/состояние Animator.
Маршруты: 1310 узлов, 3550 связей, семь предметных опор (якорь, край колокола, четыре фонаря, стол для костей). Серверный ответ на союзный выстрел: преследование, прыжок и отбрасывание с временным ragdoll 2.8 с без уменьшения здоровья. Протокол 120. Импорт/компиляция и исходные кривые проверены; Play Mode и второй клиент не запускались.
Серверные занятия: реальные предметы с пола в руках, E для забора; лечебная рыба ближайшему раненому, фактически на корабле; рыбалка с удочкой игрока и рыбой на палубе; свободный парус изменяется максимум на 0.1, руль вращается 10–15 с туда-сюда на 30–50%. FishingInterval/MischiefInterval 120 с ±15%; семь новых activity-клипов; архив до изменения V5-BipedBeforeActivities-2026-10-04 удалён при согласованной очистке 2026-10-05. Play Mode и второй клиент не проверены.
ShipMonkey.Repair.cs: ремонт своего корабля по доступным палубным маршрутам; модель молотка игрока, Repair-клип, темп строго 15% от NetworkHullRepair.StrikeInterval, общие FragmentStrikes/MastStrikes. Серверный RepairNearby/RepairMast сохраняет сетевые маски, восстановление механизмов и затопление. Ремонт выше рыбалки и шалостей по приоритету. Предыдущие 28 actions сохранены, архив V6-BeforeRepair-2026-10-04 удалён при согласованной очистке 2026-10-05; Play Mode не запускался.
Доставка лечебной рыбы проверяется каждые 0.5 с и прерывает прочие занятия, включая ремонт и шалости. Подбор/доставка бегом по палубе; выбор доступных игрока/рыбы и допуск 2 с на штатный прыжок рыбы. Руль: 10–15 с с плавными поворотами ±30–50% и циклом 5 с; паруса ±10%. Повторный игровой тест оставлен пользователю.
Архивные Versions/V1–V6 удалены 2026-10-05 по просьбе пользователя; текущие Art/Blender/Creatures/ShipMonkey/ShipMonkey.blend, исходные actions и игровые Assets сохранены. Дублированные GUID архивных копий вне Assets не являются отдельными подключёнными Unity-ресурсами.
Автомат увеличен на20% (scale1.2, высота2.28м), позиция(-3.6054,4.12,-18.0709),yaw58. После своей рыбной ставки захват ЛКМ доступен рядом без попадания лучом в LeverGrip; подсказка только у приёмника. SlotMachinePlayer order-26 и consumed input исключают перехват ShipV3PlayerInteraction. Обезьянка: бесплатный ход раз180–300с, только свободный автомат, подход спереди по палубному графу с обходом шкафа, Work/рычаг0.8с, Idle/взгляд до окончания спина и выдачи. Проигрыш80%, SkillPoint не выигрывается; оставшиеся20% пропорциональны прежним пяти категориям. При отмене бесплатной ставки не появляется возврат рыбы. Начатый спин не отменяется уходом, помощь раненому может прервать только подход. Игрок: проигрыш60%. Нативный импорт/компиляция без ошибок, PlayMode/build/клиент не запускались.
Обезьянку можно пнуть на X через её trigger-hitbox головы/тела: короткая отдача до22см с возвратом за0.25с синхронизируется существующей ShipMonkeyPose. ReceiveKick запускает общую с ReceiveFirearmShot ответную атаку: прыжок на обидчика, KnockDown2.8с, прежний КД реакции6с. За пинок отвечает и союзнику, и врагу; ограничения реакции на выстрел сохранены. Проверены импорт/компиляция; PlayMode/мультиплеер проверяет пользователь.
ShipMonkey.Defense.cs: серверная защита от абордажа. Враг определяется по TeamId и фактическому нахождению на своём корабле; защита прерывает занятия, преследует по доступным палубным маршрутам и бьёт битой с проверкой дистанции/препятствий в момент контакта. Штатный KnockDown на4с, КД120с после попадания; отмена/промах не расходуют КД. Бита из Desktop/3d/бита импортирована в MonkeyBat.fbx и подключена префабом, замах использует Repair-клип и синхронную ориентацию биты к голове. HoldingBat/BatTarget передаются в ShipMonkeyPose; ProtocolVersion135. Реакции на пинки/выстрелы сохранены. PlayMode и мультиплеер проверяет пользователь.

- [Assets/Scripts/Ships/ShipMonkey.cs](<Assets/Scripts/Ships/ShipMonkey.cs>) — Исходник C#: ShipMonkeySurface, ShipMonkeyMotion, ShipMonkeyNode, ShipMonkeyLink, ShipMonkeyPose, ShipMonkey.
- [Assets/Scripts/Ships/ShipMonkey.Defense.cs](<Assets/Scripts/Ships/ShipMonkey.Defense.cs>) — Исходник C#: ShipMonkey.
- [Assets/Prefabs/Creatures/MonkeyBat.prefab](<Assets/Prefabs/Creatures/MonkeyBat.prefab>) — Префаб Unity.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbx](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbx>) — Модель / анимации FBX.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatBaseColor.jpeg](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatBaseColor.jpeg>) — Изображение / текстура.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatMetallic.jpeg](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatMetallic.jpeg>) — Изображение / текстура.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatNormal.png](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatNormal.png>) — Изображение / текстура.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatRM.jpeg](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatRM.jpeg>) — Изображение / текстура.
- [Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatRoughness.jpeg](<Assets/Models/Creatures/ShipMonkey/Bat/MonkeyBat.fbm/BatRoughness.jpeg>) — Изображение / текстура.
- [Assets/Materials/Creatures/MonkeyBat.mat](<Assets/Materials/Creatures/MonkeyBat.mat>) — Материал Unity.
- [Assets/Scripts/Networking/NetworkShip.Monkey.cs](<Assets/Scripts/Networking/NetworkShip.Monkey.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Editor/ShipMonkeySetup.cs](<Assets/Scripts/Editor/ShipMonkeySetup.cs>) — Исходник C#: ShipMonkeySetup, RouteBuilder.
- [Assets/Prefabs/Creatures/ShipMonkey.prefab](<Assets/Prefabs/Creatures/ShipMonkey.prefab>) — Префаб Unity.
- [Assets/Models/Creatures/ShipMonkey/ShipMonkeyRigged.fbx](<Assets/Models/Creatures/ShipMonkey/ShipMonkeyRigged.fbx>) — Модель / анимации FBX.
- [Assets/Animations/ShipMonkey/ShipMonkey.controller](<Assets/Animations/ShipMonkey/ShipMonkey.controller>) — Контроллер анимации.
- [Assets/Materials/Creatures/ShipMonkey.mat](<Assets/Materials/Creatures/ShipMonkey.mat>) — Материал Unity.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Art/Blender/Creatures/ShipMonkey/ShipMonkey.blend](<Art/Blender/Creatures/ShipMonkey/ShipMonkey.blend>) — Редактируемая сцена Blender.
- [Art/Blender/Creatures/ShipMonkey/monkey_rig.py](<Art/Blender/Creatures/ShipMonkey/monkey_rig.py>) — Инструмент Python.
- [Art/Blender/Creatures/ShipMonkey/monkey_weights.py](<Art/Blender/Creatures/ShipMonkey/monkey_weights.py>) — Инструмент Python.
- [Art/Blender/Creatures/ShipMonkey/monkey_animation.py](<Art/Blender/Creatures/ShipMonkey/monkey_animation.py>) — Инструмент Python.
- [Art/Blender/Creatures/ShipMonkey/monkey_export.py](<Art/Blender/Creatures/ShipMonkey/monkey_export.py>) — Инструмент Python.
- [Assets/Scripts/Ships/ShipMonkey.Look.cs](<Assets/Scripts/Ships/ShipMonkey.Look.cs>) — Исходник C#: ShipMonkey.
- [Art/Blender/Creatures/ShipMonkey/monkey_eyes.py](<Art/Blender/Creatures/ShipMonkey/monkey_eyes.py>) — Инструмент Python.
- [Art/Blender/Creatures/ShipMonkey/monkey_legacy.py](<Art/Blender/Creatures/ShipMonkey/monkey_legacy.py>) — Инструмент Python.
- [Art/Blender/Creatures/ShipMonkey/monkey_skin_v3.py](<Art/Blender/Creatures/ShipMonkey/monkey_skin_v3.py>) — Инструмент Python.
- [Assets/Scripts/Ships/ShipMonkey.Jump.cs](<Assets/Scripts/Ships/ShipMonkey.Jump.cs>) — Исходник C#: ShipMonkey.
- [Assets/Scripts/Ships/ShipMonkeyHitbox.cs](<Assets/Scripts/Ships/ShipMonkeyHitbox.cs>) — Исходник C#: ShipMonkeyHitbox.
- [Assets/Scripts/Networking/NetworkPlayer.Knockdown.cs](<Assets/Scripts/Networking/NetworkPlayer.Knockdown.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/Player/PlayerKnockdown.cs](<Assets/Scripts/Player/PlayerKnockdown.cs>) — Исходник C#: PlayerKnockdown.
- [Art/Blender/Creatures/ShipMonkey/monkey_jump.py](<Art/Blender/Creatures/ShipMonkey/monkey_jump.py>) — Инструмент Python.
- [Assets/Scripts/Ships/ShipMonkey.Activities.cs](<Assets/Scripts/Ships/ShipMonkey.Activities.cs>) — Исходник C#: ShipMonkey, TaskKind.
- [Assets/Scripts/Networking/NetworkFish.Monkey.cs](<Assets/Scripts/Networking/NetworkFish.Monkey.cs>) — Исходник C#: NetworkFish.
- [Assets/Scripts/Networking/NetworkFish.cs](<Assets/Scripts/Networking/NetworkFish.cs>) — Исходник C#: InventoryItem, NetworkFish.
- [Assets/Scripts/Networking/NetworkFishing.cs](<Assets/Scripts/Networking/NetworkFishing.cs>) — Исходник C#: NetworkFishing.
- [Assets/Scripts/Networking/NetworkLooseCannonball.cs](<Assets/Scripts/Networking/NetworkLooseCannonball.cs>) — Исходник C#: NetworkLooseCannonball.
- [Assets/Scripts/HelmInteraction.cs](<Assets/Scripts/HelmInteraction.cs>) — Исходник C#: HelmInteraction.
- [Assets/Scripts/SailSystem.cs](<Assets/Scripts/SailSystem.cs>) — Исходник C#: SailSystem.
- [Art/Blender/Creatures/ShipMonkey/monkey_activities.py](<Art/Blender/Creatures/ShipMonkey/monkey_activities.py>) — Инструмент Python.
- [Assets/Scripts/Ships/ShipMonkey.Repair.cs](<Assets/Scripts/Ships/ShipMonkey.Repair.cs>) — Исходник C#: ShipMonkey.
- [Assets/Scripts/Networking/NetworkHullRepair.cs](<Assets/Scripts/Networking/NetworkHullRepair.cs>) — Исходник C#: NetworkHullRepair.
- [Art/Blender/Creatures/ShipMonkey/monkey_repair.py](<Art/Blender/Creatures/ShipMonkey/monkey_repair.py>) — Инструмент Python.
- [Assets/Scripts/Ships/ShipMonkey.SlotMachine.cs](<Assets/Scripts/Ships/ShipMonkey.SlotMachine.cs>) — Исходник C#: ShipMonkey.
- [Assets/Scripts/Networking/NetworkShip.SlotMachine.cs](<Assets/Scripts/Networking/NetworkShip.SlotMachine.cs>) — Исходник C#: ShipSlotSnapshot, NetworkShip.
- [Assets/Scripts/Ships/ShipSlotMachinePlayer.cs](<Assets/Scripts/Ships/ShipSlotMachinePlayer.cs>) — Исходник C#: ShipSlotMachinePlayer.
- [Art/Blender/Creatures/ShipMonkey/README.md](<Art/Blender/Creatures/ShipMonkey/README.md>) — Документация.
- [Art/Blender/Creatures/ShipMonkey/Versions/README.md](<Art/Blender/Creatures/ShipMonkey/Versions/README.md>) — Документация.
- [Docs/ShipSlotMachine.md](<Docs/ShipSlotMachine.md>) — Документация.

## Источники актуальных настроек

- [ProjectSettings/ProjectVersion.txt](<ProjectSettings/ProjectVersion.txt>).
- [Packages/manifest.json](<Packages/manifest.json>).
- [Assets/Settings/Networking/SessionConfig.asset](<Assets/Settings/Networking/SessionConfig.asset>).
- [ProjectSettings/EditorBuildSettings.asset](<ProjectSettings/EditorBuildSettings.asset>).

## Поддержание карты

- При добавлении, удалении, переименовании файлов или изменении назначения системы обновляй карту в той же задаче. Темы и пояснения редактируй в `Tools/Context/topics.json`, затем пересобери каталог.
- Обновление: `python Tools/Context/update_project_map.py` из корня проекта (либо абсолютный путь к скрипту). Нужен Python 3. Скрипт перечисляет файлы и читает C#-объявления; Unity/Blender не запускает. Это обслуживание карты, а не обязательное действие каждого нового чата.
- Не редактируй автоматически созданные приложения вручную: изменения будут заменены при обновлении. Новые подробные пояснения добавляй в тематический источник.
- Если путь устарел, проверь конкретный путь и исправь запись. При отсутствии записи и невозможности восстановить путь из ссылок запроси разрешение на ограниченный поиск; не начинай с общего сканирования.
- `Tools/Context/context.ps1 <тема>` остаётся необязательным помощником для живых значений сохранённых конфигов. Он не заменяет эту карту как первый источник путей.
