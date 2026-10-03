# PirateSlop — карта проекта

Снимок файлов: 2026-10-03. Корень: `C:\Users\K\Project`.

## Как пользоваться

Сначала прочитай `AGENTS.md`, `lessons.md` и эту карту. Выбери механику ниже; если нужного файла нет среди точек входа, открой соответствующий каталог из таблицы. Ищи имя внутри этих Markdown-файлов, затем читай исходник по указанному пути. Не запускай обзор папок или поиск файлов по всему проекту при каждом новом чате.

Карта описывает сохранённые файлы, а не живую сцену и не результаты игровых проверок. Назначения механик взяты из поддерживаемого `Tools/Context/topics.json`; в полном каталоге указаны тип файла, объявленные C#-типы и известные тематические связи. Для остальных файлов семантика не угадывается по имени. Подключение компонента и актуальную реализацию проверяй только для затронутой задачи.

Все пути относительно корня проекта. Полный каталог разбит на приложения, чтобы не загружать тысячи строк в каждый чат.

## Полный каталог

Учтено 4400 файлов без `.meta`. Ещё 4655 файлов `.meta` сопровождают ассеты/папки: их путь — путь ассета или папки плюс `.meta`; сохраняй их GUID. Сама карта и её автоматически созданные приложения не входят в подсчёт.

| Раздел | Назначение | Файлов |
| --- | --- | ---: |
| [.agents](<Docs/ProjectMap/.agents.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.cursor](<Docs/ProjectMap/.cursor.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.opencode](<Docs/ProjectMap/.opencode.md>) | Ресурсы раздела; точный состав — в каталоге | 4 |
| [Art](<Docs/ProjectMap/Art.md>) | Исходники арта и Blender | 157 |
| [Assets](<Docs/ProjectMap/Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 3 |
| [Assets/Animations](<Docs/ProjectMap/Assets-Animations.md>) | Анимации | 51 |
| [Assets/Audio](<Docs/ProjectMap/Assets-Audio.md>) | Звуковые ресурсы и лицензии | 182 |
| [Assets/Branding](<Docs/ProjectMap/Assets-Branding.md>) | Оформление проекта | 1 |
| [Assets/Editor](<Docs/ProjectMap/Assets-Editor.md>) | Редакторские ресурсы | 1 |
| [Assets/Fog Particles](<Docs/ProjectMap/Assets-Fog Particles.md>) | Ресурсы раздела; точный состав — в каталоге | 17 |
| [Assets/Game](<Docs/ProjectMap/Assets-Game.md>) | Игровые подсистемы и эффекты | 79 |
| [Assets/Houidisoft technology](<Docs/ProjectMap/Assets-Houidisoft technology.md>) | Ресурсы раздела; точный состав — в каталоге | 15 |
| [Assets/JMO Assets](<Docs/ProjectMap/Assets-JMO Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 488 |
| [Assets/Materials](<Docs/ProjectMap/Assets-Materials.md>) | Материалы | 151 |
| [Assets/Mirza](<Docs/ProjectMap/Assets-Mirza.md>) | Ресурсы раздела; точный состав — в каталоге | 186 |
| [Assets/Models](<Docs/ProjectMap/Assets-Models.md>) | Модели и связанные ресурсы | 1338 |
| [Assets/Plugins](<Docs/ProjectMap/Assets-Plugins.md>) | Плагины | 5 |
| [Assets/Prefabs](<Docs/ProjectMap/Assets-Prefabs.md>) | Готовые игровые объекты | 132 |
| [Assets/Resources](<Docs/ProjectMap/Assets-Resources.md>) | Ресурсы, доступные для загрузки по имени | 43 |
| [Assets/Scenes](<Docs/ProjectMap/Assets-Scenes.md>) | Сохранённые сцены | 3 |
| [Assets/Scripts](<Docs/ProjectMap/Assets-Scripts.md>) | Игровой код и редакторские инструменты | 346 |
| [Assets/Settings](<Docs/ProjectMap/Assets-Settings.md>) | Настройки игровых систем и рендеринга | 44 |
| [Assets/Shaders](<Docs/ProjectMap/Assets-Shaders.md>) | Шейдеры | 7 |
| [Assets/StreamingAssets](<Docs/ProjectMap/Assets-StreamingAssets.md>) | Ресурсы раздела; точный состав — в каталоге | 2 |
| [Assets/Tests](<Docs/ProjectMap/Assets-Tests.md>) | Исходники проверок | 31 |
| [Assets/ThirdParty](<Docs/ProjectMap/Assets-ThirdParty.md>) | Сторонние ресурсы | 27 |
| [Assets/TutorialInfo](<Docs/ProjectMap/Assets-TutorialInfo.md>) | Ресурсы раздела; точный состав — в каталоге | 7 |
| [Assets/UI](<Docs/ProjectMap/Assets-UI.md>) | Ресурсы интерфейса | 40 |
| [Assets/_Recovery](<Docs/ProjectMap/Assets-_Recovery.md>) | Сохранённые восстановленные данные | 9 |
| [Docs](<Docs/ProjectMap/Docs.md>) | Документы и сохранённые отчёты | 36 |
| [Packages](<Docs/ProjectMap/Packages.md>) | Манифест, lock-файл и встроенные пакеты | 916 |
| [ProjectSettings](<Docs/ProjectMap/ProjectSettings.md>) | Настройки Unity | 28 |
| [Root](<Docs/ProjectMap/Root.md>) | Корневые инструкции, планы и служебные файлы | 29 |
| [ThirdParty](<Docs/ProjectMap/ThirdParty.md>) | Сторонние ресурсы | 2 |
| [Tools](<Docs/ProjectMap/Tools.md>) | Инструменты разработки и загрузчик контекста | 18 |

Не индексируются генерируемые сборки, кэши и локальное состояние: `.git/`, `.idea/`, `.vs/`, `Builds/`, `Library/`, `Logs/`, `Temp/`, `UserSettings/`, `__pycache__/`, `bin/`, `node_modules/`, `obj/`. Зависимости из Unity PackageCache представлены манифестом/lock-файлом; встроенные пакеты из `Packages/` перечислены полностью. Скрытые конфиги вне исключённых папок включены только как пути, их содержимое не копируется.

## Механики и точки входа

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
Ship V3 теперь единственный игровой корабль: SessionController.ShipPrefab в NetworkMenu указывает на Assets/Resources/Ships/ShipV3Test.prefab. Старый NetworkShip.prefab сохранён как архивный ассет и не создаётся в игре. Историческое имя ShipV3Test сохранено вместе с GUID и регистрацией FishNet.

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
Замеры на RTX 3050 8 ГБ / i5-9400F, тестовая карта, один корабль, Unity Editor 1471x714: до оптимизации около 31 FPS, CPU main 32.2 мс, Physics.SyncTransforms 14.0 мс; после объединения коллайдеров среднее 98.3 FPS за 571 кадр, медиана 119.6, P95 12.36 мс; Physics.SyncTransforms 0.48 мс и Simulate 0.22 мс. Отдельные shadow proxies и последние правки ручки/флагов добавлены после замера. Финальный игровой прогон прекращён по просьбе пользователя; стабильные 90–100 FPS, 1080p, билд и два клиента не подтверждены.
ConfigureDispenser разворачивает поперечный захват горизонтально, сохраняет направление стержня вдоль выхода ядра и удлиняет вынос стержня на 25% через DispenserLeverLength. Область взаимодействия следует за настоящим концом рукоятки. ConfigureFlags поднимает низ полотна на 2.3 м над площадкой гнезда, удлиняет неподвижный флагшток, удаляет с него ShipV3ClothMotion. Полотно имеет закреплённый край и только горизонтальное колыхание, промежуточная фаза ветра сглаживается между сетевыми тиками.

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
- [unity.md](<unity.md>) — Документация.
- [multiplayer-plan.md](<multiplayer-plan.md>) — Документация.

### Паруса и канаты (`sails`)

Ключевые слова: паруса, парус, канаты, rigging.

Разделять управление натяжением и визуальную геометрию канатов. Изменение модели не должно менять сетевую занятость.
Настройка канатов и обновление их арта — отдельные редакторские операции; перед повторным запуском изучить соответствующий setup.
У четырёх маршрутов Ship V3 к гнёздам включён ShipLadder.BothSides: AdvancedPlayerController выбирает смещение и направление взгляда по стороне захвата. У боковых сеток PinTop вершины смещаются вместе с весом по высоте, сохраняя верхнее крепление; деформация вдоль нормалей, раздувавшая сетку, убрана.
ShipGripAim расширяет наведение на парусные ручки до 0.38 м от ближайшей точки коллайдера; ShipControlHandle.Active даёт поиск без обязательного попадания лучом в мелкий меш. Видимость, InRange и серверное владение сохранены. Для рынды и маски выдачи ShipV3 применяется тот же допуск, триггеры увеличены до 0.22 м.

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
- [unity.md](<unity.md>) — Документация.
- [blender.md](<blender.md>) — Документация.

### Пушки, ядра и лафеты (`cannons`)

Ключевые слова: пушки, пушка, cannon, ядра, мортира.

Проверять серверные условия выстрела, загрузки и занятости; локальные эффекты не подтверждают сетевой выстрел.
Для движения ядра и лафета учитывать движение корабля. Баланс брать из текущих полей и ассетов, а не старых записей.
ShipSpyglassView показывает прогноз траекторий пушек своего корабля через стационарную и ручную подзорные трубы, обновляет каждые 0.2 с и скрывает линии при выходе.
CannonSmokeTrail: дым в мировых координатах по пройденным сегментам, затухает за 3.3 с и сохраняется после уничтожения ядра; общий более тёмный серый материал частиц, масштаб 0.3 для огнестрельного оружия. CannonShotDamage: обычные, ледяные и толкающие ядра рикошетят от окружения с потерей скорости; корабли и живые цели сохраняют урон, огненные и мортирные снаряды — взрыв. WorldStructureCollision добавляет недостающие MeshCollider статической геометрии при построении мира; ревизия входит в CatalogHash.
Новые лутаемые модели из Blender/Лутабельные подключены через LootModelReplacementSetup к прежним игровым префабам. Пушка собрана из CannonBase (исходная «люлька»), CannonMount («Лафет»), CannonBarrel и четырёх CannonWheel. SimpleCannon.TraversePivot поворачивает ложе отдельно от наклона BarrelPivot; при отсутствии TraversePivot сохранена прежняя схема. CannonWheelVisual вращает колёса от перемещения относительно корабля, включая сетевое движение и отдачу. Коллайдер ствола и Breech следуют новым частям. Стрельба, боеприпасы и параметры лафета сохранены.
Кинематическое качение Cannonball учитывает столкновения с другими свободными ядрами: SphereCast движения, устранение перекрытий через OverlapSphereNonAlloc, импульс с учётом массы и скорости корабля. Опорная палуба ищется без других ядер. Серверный NetworkLooseCannonball передаёт итоговые позы прежним способом; загруженные ядра, удержание и механика выстрела сохранены.
Парный BoardingHook: BoardingShotFlight плавно разводит два гарпуна, моделирует два попадания и вытягивание верёвок. Хост хранит два BoardingCable с общим Shot и отдельными Hook/Hits; каждому нужны два удара саблей. BoardingWalkSurface создаёт поперечины и поверхность для бега; при потере одного крепления остаётся один проходимый трос. Не создавать повторное крепление от устаревшего выстрела после следующего выстрела этой пушки. Протокол 111; игровая и сетевая проверка остаются пользователю.
NetworkCannon отправляет изменения placements каждые 0.05 с; удалённый клиент сглаживает положение, вращение лафета, наклон и поворот механизма каждый кадр между полученными состояниями. Собственное управление и серверная физика сохраняются. Подаваемое ядро также получает промежуточные положения; NetworkLooseCannonball SyncVars настроены на 0.05 с. Буферы очищаются при OnStopClient. Онлайн-проверка двумя клиентами не выполнялась.

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
- [combat-balance.md](<combat-balance.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Персонаж, камера и анимации (`player`)

Ключевые слова: игрок, персонаж, камера, анимации.

AdvancedPlayerController.Spectator: через 1.5 с после смерти автоматически наблюдает живого союзника, ЛКМ переключает; после Eliminated — свободный полёт WASD/Space/Ctrl, Shift ускоряет. Камера перемещается локально, тело остаётся на месте; возрождение возвращает обычное управление. ShipObserverCondition передаёт умершим/выбывшим объекты вне обычного радиуса видимости. Ввод и камера принадлежат локальному игроку; движение наблюдателей и анимации сверять с сетевым состоянием. Камера NetworkPlayer использует сферическое отсечение по слоям на 1000 м; дальняя плоскость вынесена до 5000 м, чтобы поворот камеры не менял дальность видимости. AdvancedPlayerController сохраняет эту настройку при запуске.
Проверять привязку компонентов к NetworkPlayer. Совпадение имён костей не гарантирует совместимость анимаций.
NetworkPlayer использует Tripo/Mixamo Walking.fbx из NewPirate. Лицо и борода имеют исправленные веса Head; исходник и способ сохранения FBX — Art/Blender/Characters/NewPirate/README.md.
AdvancedPlayerController: новое нажатие пробела у поверхности воды (глубина ног <= 1.4 м) запускает прыжок с высотой jumpHeight над водой; при подъёме персонаж остаётся в воздушной симуляции. В глубине удержание пробела по-прежнему поднимает пловца. Используются существующие сетевые Swimming/VerticalVelocity без новых полей состояния.
F2 при наличии сетевого ShipV3Features переносит живого локального игрока на RespawnPoint нового корабля в текущей сцене. ServerRpc на NetworkPlayer и ObserversRpc сбрасывают состояние перемещения через Teleport, освобождают механизмы и прикрепляют пассажира к новому кораблю; домашний корабль не меняется.
Новый корабль использует FollowRopePath у ShipLadder: четыре боковых маршрута к гнёздам и две посадочные сетки, от настоящих нижних вершин мешей до выхода над ограждением. TopSideOffset и TopLean учитывают наклон в двух направлениях; RopeStandOff оставляет пловца снаружи сетки. Для этих маршрутов не создаётся сплошная вертикальная стенка. AdvancedPlayerController использует те же маршруты в существующей сетевой симуляции; прежние лестницы основного корабля сохраняют настройки по умолчанию. После правки проверены только компиляция и edit-mode привязки, игровой онлайн-прогон не выполнялся.
ShipDeckPassenger учитывает BoardingWalkSurface: перенос по текущей кривой троса между двумя кораблями, обычное управление движением сохранено. При превращении лестницы в одиночную верёвку игрок сохраняет мировое положение; потерявший опору падает. Положение продолжает передаваться существующим состоянием NetworkPlayer относительно корабля пушки.

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
- [animation-review.md](<animation-review.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Личное оружие и урон (`weapons`)

Ключевые слова: оружие, пистолет, бой, combat.

Разделять локальный отклик оружия и серверное подтверждение урона/расхода боеприпасов.
Настройки оружия искать через FirearmDefinition и используемые ссылки; не копировать числовой баланс из истории.
PistolBullet создаёт мини-дым CannonSmokeTrail по фактическому движению визуальной пули для пистолета, мушкета и каждой дробины двустволки, включая предсказанные и удалённые выстрелы. Масштаб 0.3, шаг 0.12 м, затухание 3.3 с; дым сохраняется при попадании и повторном использовании пула трассеров.
SabreAnimation использует отдельный хват Mixamo и переносит движение игровой сабли в координаты камеры для первого лица. Слой SabreCombat активен при выбранной сабле; Ready использует New_SabreReady. NewPirateAnimationBatch сохраняет эту стойку при переимпорте.

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
- [firearm-foundation.md](<firearm-foundation.md>) — Документация.
- [combat-balance.md](<combat-balance.md>) — Документация.

### Предметы, лут и инвентарь (`loot`)

Ключевые слова: лут, предметы, инвентарь, inventory.

Подбор и расход предметов подтверждает сервер. Сверять идентификаторы предметов, иконки и каталог. Одинаковые предметы складываются в стак без игрового лимита количества; ядра сохраняют отдельный слот на 2 ядра одного типа. NetworkWeapon.stackCounts хранит количество оружия/снаряжения; рыба и ром используют прежние счётчики без лимитов 20/6. Расход, сброс, установка пушек и TransferInventoryTo сохраняют остаток стака. DeveloperMenu содержит ползунок скорости корабля 100–500%: команда 20 применяет к текущему кораблю под игроком либо его собственному. Сервер меняет NetworkShip.DeveloperSpeedMultiplier (SyncVar); ShipController масштабирует максимальную скорость и разгон/торможение, сохраняя ограничения якоря, повреждений, затопления и буксировки. DeveloperMenu содержит выпадающий список пяти морских ивентов и спаун перед текущим/собственным кораблём через команду 19 NetworkDeveloperTools. Сервер проверяет свободную воду, наполняет сундук из ChestLoot.json и учитывает его при удалении тестовых объектов. Надписи морских лутовых ивентов скрыты по умолчанию; локальный переключатель DeveloperMenu показывает все доступные клиенту ивенты без ограничения расстояния.
NetworkWeapon имеет отдельные partial-файлы; для морского лута начать с NetworkWeapon.SeaLoot.cs.
Состав сундуков и число разных типов (до 10) задаёт Assets/StreamingAssets/Loot/ChestLoot.json; любой новый лут сундуков обязательно подключать туда. Сервер перечитывает таблицу при наполнении; инструкция рядом в ChestLoot.README.md.
Плот движется на сервере со скоростью 20% MaxSpeed корабля, поворачивает внутрь за 150 м от границы зоны; ограничение радиуса оставляет 108 м от центра плота до границы при сужении. RaftPlatform и ShipDeckPassenger переносят игрока, сетевой Platform ссылается на сундук. ShipSpyglassView.LootHint создаёт один полупрозрачный золотистый столб света с сечением 1×1 м и высотой 300 м над ближайшим активным морским ивентом из ClientChests. Выбор по расстоянию от игрока не зависит от направления взгляда; переносимые, пустые и закреплённые на корабле сундуки исключаются. LootEventBeam.shader использует мягкие края, затухание к вершине и depth test. Render callbacks показывают столб только камере подзорной трубы, на выходе скрывают. Экранное пятно удалено. В обеих подзорных трубах туман сохраняется первую секунду, затем за 3 с SmoothStep ослабляет его плотность до 1% исходной. ShipSpyglassView.FogMultiplier применяется к обычному и SeaMist туману только камеры трубы; выход восстанавливает значения. Экранный взлом: мышь/A/D задают угол, ЛКМ/пробел вращают замок, три отмычки на попытку; секретный угол и успех проверяет сервер.
Подводный сундук: только сундук и верёвка от дна с витками вокруг корпуса. E схватывает верёвку; сервер считает реальное плавание вокруг сундука, по умолчанию два круга (LootCatalog.SunkenUnwrapTurns). Обратное движение наматывает обратно; E/Q/Esc отпускают с сохранением прогресса. После полного разматывания сундук всплывает. Боты плывут по орбите, прежние буй и три крепления удалены. Две процедурные чайки с взмахами крыльев кружат на высоте 10–12 м над водой по радиусам 6–8 м до завершения всплытия, обозначая место сундука.
VortexBottle=23 — бутылка с вращающимся вихрем. ЛКМ бросает через NetworkEquipment и NetworkWeapon.VortexBottle. NetworkVortexBottle считает полёт и столкновения на сервере; удар по NetworkShip даёт 500% обычной скорости на 10 с, повторный удар обновляет таймер без сложения. NetworkShip.VortexBoost хранит SyncVar, ShipController за 0.35 с разгоняет до 500% базовой максимальной скорости самостоятельной тягой, включая старт с нуля, закрытые паруса и опущенный якорь. На время эффекта обычные ограничения тяги и буксировки не задают скорость, якорь перемещается вместе с кораблём через anchorSeabedPoint; после 10 с возвращаются обычные правила, опущенный якорь останавливает корабль в новой точке. DeveloperSpeedMultiplier сохраняется. VortexBottleSetup регистрирует pickup/model/icon/DefaultLoot и тестовую палубу. ChestLoot.json содержит отдельный настраиваемый entry. VortexBottleVisual создаёт три вращающиеся спиральные ленты внутри прозрачной бутылки при Awake.
FogBottle=24 — бутылка тумана, бросок по ЛКМ через NetworkWeapon.FogBottle и NetworkEquipment. NetworkFogBottle считает попадание на сервере, включая геометрию и первую точку пересечения CPU-поверхности воды. При разбитии создаёт отдельный сетевой FogCloud в мировой точке удара. NetworkFogCloud живёт 15 с, синхронизирует возраст каждые 0.2 с и даёт клиентам плавное время, включая позднее появление. FogCloudVisual/BottleFog.shader рисуют объём эллипсоида радиусом 50 м и высотой 44 м, цвет .23/.28/.29, плотность .18; raymarch с depth clipping работает снаружи и изнутри, независимо от обычного тумана, читов и подзорной трубы. Облако разворачивается за .55 с и исчезает в последние 1.1 с. FogBottleVisual использует тот же эффект внутри стеклянной бутылки с отдельными локальными осями, плотностью 45 и очередью 3000 перед стеклом. FogBottleSetup сохраняет модель/pickup/cloud, иконку, каталог, сетевой реестр, тестовую палубу; ChestLoot.json имеет отдельную настраиваемую строку без изменения прежних настроек. ProtocolVersion=108.
Огненный череп — пятый морской ивент. NetworkSkullEvent вращает платформу и череп 8°/с; сервер принимает первое фронтальное попадание ядром в увеличенный объём рта 25.2×13.68×20 м, включая зубы и края, через SkullMouthTarget или коллайдер черепа, гасит огонь и фиксирует угол. Через 3 с создаёт наш сундук из ChestLoot.json и запускает дугой 2.5 с на свободную поверхность палубы корабля стрелявшего. TryFindRewardDeckPoint использует коллайдеры палубы, включая секции разрушения; траектория следует за локальной точкой корабля через существующие support/anchor, после посадки сундук остаётся закреплён на корабле. При отсутствии пригодной палубы или потере корабля награда падает в воду. Платформа остаётся потушенной. SkullFireVfx: девять ParticleSystem, три источника света, URP SkullFire; затухание .55 с, late join восстанавливает состояние. SeaLootSpawner, F8 и подсказка подзорной трубы поддерживают ивент. SkullEventSetup сохраняет префаб, каталог и FishNet registry. ProtocolVersion=109; игровая и сетевая приёмка выполняется пользователем. После правки размеры: череп высотой 54 м, платформа шириной 96 м; текущий масштаб ивента ×2 (последний размер уменьшен в 1.5 раза) через RotatingRoot префаба, исходная модель FBX/BLEND сохраняет авторский масштаб; посадка вычислена по контакту нижней поверхности черепа с местной поверхностью платформы. Позиция ивента фиксирована как у острова, Rigidbody FreezeAll. Сокеты, область рта и пространственные параметры огня увеличены ×3; радиус платформы 50 м учитывается при спауне и выбросе сундука. ParticleSystemScalingMode.Hierarchy применяет масштаб иерархии один раз; дальность света и SoftDistance увеличены отдельно. Сундук остаётся обычного размера.
Заменены модели разобранной пушки, обычного/огненного/ледяного/толкающего ядра, рома, крюка-кошки и святой гранаты: Assets/Models/Loot/Replacement. Обновлены лут, оборудование в руках, снаряды, запас основного корабля и восемь иконок; прежние GUID, сетевые предметы и механики сохранены. ShipV3Test имеет RumShelf на четырёх авторских полках: 12 бутылок общего NetworkShip.RumCount. ExperimentalShipEquipment.SpawnPoints размещает восемь типов на палубе нового корабля; Items задаёт сетевой тип особых ядер до Spawn. Повторный полный импорт ShipV3 сохраняет это размещение через ConfigureImportedShip. Игровая и сетевая приёмка остаются пользователю.
На новом ShipV3Test обычное ядро исключено из ExperimentalShipEquipment: осталось семь палубных предметов. Обычные ядра выдаёт ShipV3Features.Dispense из маски в трюме, до шести свободных экземпляров с прежним интервалом 4 с. ConfigureDispenserBindings привязывает рот к V15_Cannonball_Spawn со смещением внутрь и зазором по радиусу ядра; MeshCollider модели сохраняет физическую поверхность языка. Основной корабль и его источник ядер не изменены.
BoardingEquipmentSetup заменяет три модели из Blender/Лутабельные/New: абордажный снаряд, Wine и Spyglass. Прежние игровые префабы, GUID, механики и ссылки инвентаря сохранены. BoardingHookVisual содержит одиночный гарпун; BoardingHarpoonPairVisual — две копии с верёвкой, индекс 5 в Cannonball.AmmoModels. Обновлены три предметные иконки и запасы обоих кораблей. Модели и PBR-материалы находятся в Assets/Models/Loot/Replacement.
На ShipV3Test к семи прежним предметам добавлены BoardingHook, Wine и Spyglass: всего десять маркеров на настоящей палубе. ExperimentalShipEquipment сохраняет авторитетный серверный спавн и цикл повторного появления; обычные ядра по-прежнему выдаёт маска. BoardingHookAmmo и CannonHands показывают парный снаряд ещё до загрузки в пушку.
FogBottle и VortexBottle используют пустую Whisky Bottle: дым ограничен внутренним объёмом, вихрь состоит из мягких вращающихся лент. BottleFog сохраняет прежний большой игровой туман отдельной веткой InsideBottle; BottleVortex — шейдер внутреннего вихря. ExperimentalShipEquipment содержит две дополнительные точки на центральной палубе ShipV3Test: (-1.1,4.11,0.35) и (1.1,4.11,0.35). После подбора бутылка восстанавливается на следующем серверном кадре в своей точке; остальные предметы сохраняют интервал 20 секунд.

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
- [Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonVisual.prefab](<Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonVisual.prefab>) — Префаб Unity.
- [Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonPairVisual.prefab](<Assets/Models/Loot/Replacement/BoardingHarpoon/BoardingHarpoonPairVisual.prefab>) — Префаб Unity.
- [Assets/Models/Loot/Replacement/WineBottle/WineBottleVisual.prefab](<Assets/Models/Loot/Replacement/WineBottle/WineBottleVisual.prefab>) — Префаб Unity.
- [Assets/Models/Loot/Replacement/SpyglassTube/SpyglassTubeVisual.prefab](<Assets/Models/Loot/Replacement/SpyglassTube/SpyglassTubeVisual.prefab>) — Префаб Unity.
- [Assets/Resources/BoardingHookAmmo.prefab](<Assets/Resources/BoardingHookAmmo.prefab>) — Префаб Unity.
- [Assets/Scripts/Editor/BottleFishReplacementSetup.cs](<Assets/Scripts/Editor/BottleFishReplacementSetup.cs>) — Исходник C#: BottleFishReplacementSetup.
- [Assets/Resources/BottleVortex.shader](<Assets/Resources/BottleVortex.shader>) — Шейдер.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
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

- [Assets/Scripts/Networking/NetworkHullRepair.cs](<Assets/Scripts/Networking/NetworkHullRepair.cs>) — Исходник C#: NetworkHullRepair.
- [Assets/Scripts/ShipDestruction/ShipDestruction.cs](<Assets/Scripts/ShipDestruction/ShipDestruction.cs>) — Исходник C#: ShipSectionSnapshot, ShipDestructionEvent, ShipDestruction.
- [Assets/Scripts/ShipDestruction/ShipDamageSection.cs](<Assets/Scripts/ShipDestruction/ShipDamageSection.cs>) — Исходник C#: ShipDamageSection.
- [Assets/Scripts/ShipDestruction/ShipFlooding.cs](<Assets/Scripts/ShipDestruction/ShipFlooding.cs>) — Исходник C#: ShipBreach, ShipFlooding.
- [Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs](<Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs>) — Исходник C#: ShipSectionState, ShipSectionType, ShipDamageReason, ShipAmmoMultiplier, ShipSectionDefinition, ShipDestructionProfile, ShipFragmentConnection.
- [Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs](<Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs>) — Исходник C#: ShipStructuralGraph.
- [Assets/Scripts/Editor/ShipDestructionSetup.cs](<Assets/Scripts/Editor/ShipDestructionSetup.cs>) — Исходник C#: ShipDestructionSetup, Manifest, Record.
- [Assets/Settings/ShipDestruction/ShipV3Destruction.asset](<Assets/Settings/ShipDestruction/ShipV3Destruction.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Ships/ShipV3Features.cs](<Assets/Scripts/Ships/ShipV3Features.cs>) — Исходник C#: ShipV3TargetKind, ShipV3Lantern, ShipV3DiceSlot, ShipV3PhysicsPose, ShipV3Support, ShipV3Attachment, ShipV3Features.
- [ship-destruction.md](<ship-destruction.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Мир, острова и океан (`world`)

Ключевые слова: мир, острова, карта, океан, water.

Согласовывать генерацию карты и её состояние между участниками; seed и профиль брать из используемых ассетов.
CPU-поверхность воды используется игровой логикой: визуальные волны нельзя менять независимо от OceanSurface без проверки связи. OceanSurface добавляет длинные волны 180/120 м; по StormProgress плавно подключаются 90/64 м. Амплитуды всех четырёх длинных волн удвоены (1.1/0.56/0.36/0.24 м);, высота растёт до FinalSwellMultiplier=2.5 и усиливается мелкое волнение. SwellStrength задаёт базовую высоту. Одинаковые параметры _SwellWaves и время используются CPU и обоими шейдерами воды; ShipController продолжает брать высоту в четырёх точках корпуса. SessionStorm больше не меняет неиспользуемый активным SimpleWater параметр WaveScale.

- [Assets/Scripts/World/ProceduralWorld.cs](<Assets/Scripts/World/ProceduralWorld.cs>) — Исходник C#: ProceduralWorld.
- [Assets/Scripts/World/WorldProfile.cs](<Assets/Scripts/World/WorldProfile.cs>) — Исходник C#: WorldDecoration, WorldProfile.
- [Assets/Scripts/World/WorldGenerator.cs](<Assets/Scripts/World/WorldGenerator.cs>) — Исходник C#: WorldGenerator.
- [Assets/Scripts/World/BalancedWorldGenerator.cs](<Assets/Scripts/World/BalancedWorldGenerator.cs>) — Исходник C#: BalancedWorldGenerator.
- [Assets/Scripts/World/WorldDecorationPlacer.cs](<Assets/Scripts/World/WorldDecorationPlacer.cs>) — Исходник C#: WorldDecorationPlacer.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
- [Assets/Shaders/Ocean.shader](<Assets/Shaders/Ocean.shader>) — Шейдер.
- [Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader](<Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader>) — Шейдер.
- [Assets/Scripts/Networking/SessionStorm.cs](<Assets/Scripts/Networking/SessionStorm.cs>) — Исходник C#: StormMessage, SessionController.
- [Assets/Scripts/Editor/OceanSetup.cs](<Assets/Scripts/Editor/OceanSetup.cs>) — Исходник C#: OceanSetup.
- [Assets/Scripts/Editor/WorldGenerationSetup.cs](<Assets/Scripts/Editor/WorldGenerationSetup.cs>) — Исходник C#: WorldGenerationSetup.
- [Assets/Scripts/World/EnvironmentTestGallery.cs](<Assets/Scripts/World/EnvironmentTestGallery.cs>) — Исходник C#: EnvironmentTestGallery.
- [Assets/Scripts/Editor/EnvironmentTestSetup.cs](<Assets/Scripts/Editor/EnvironmentTestSetup.cs>) — Исходник C#: EnvironmentTestSetup.
- [Assets/Scenes/NetworkOcean.unity](<Assets/Scenes/NetworkOcean.unity>) — Сцена Unity.
- [procedural-world.md](<procedural-world.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Сеть, сессия и Steam (`networking`)

Ключевые слова: сеть, мультиплеер, steam, network.

FishNet: серверный авторитет. Изменения формата сообщений согласовывать с ProtocolVersion; не повышать его автоматически без изменения протокола.
IP/Tugboat и Steam — отдельные способы подключения. localhost не подтверждает работу через интернет; позднее подключение и отключение требуют отдельной приёмки.
MultiplayerSceneSetup.Configure и PromoteShipV3 привязывают ShipV3Test к общему SessionController.ShipPrefab. Отдельный вызов ShipV3TestSpawner из ServerState удалён. ShipComparisonEnabled выключен, ComparisonShips очищен; старый корабль сохранён в проекте и регистрации FishNet без изменения существующих индексов.

- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionConfig.cs](<Assets/Scripts/Networking/SessionConfig.cs>) — Исходник C#: SessionConfig.
- [Assets/Scripts/Networking/SessionAuthenticator.cs](<Assets/Scripts/Networking/SessionAuthenticator.cs>) — Исходник C#: HelloMessage, AdmissionMessage, PopulationMessage, SessionAuthenticator.
- [Assets/Scripts/Networking/SteamParty.cs](<Assets/Scripts/Networking/SteamParty.cs>) — Исходник C#: SteamParty.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Networking/SimulationState.cs](<Assets/Scripts/Networking/SimulationState.cs>) — Исходник C#: PlayerCommand, PlayerState, ShipState.
- [Assets/Settings/Networking/SessionConfig.asset](<Assets/Settings/Networking/SessionConfig.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/MultiplayerSceneSetup.cs](<Assets/Scripts/Editor/MultiplayerSceneSetup.cs>) — Исходник C#: MultiplayerSceneSetup.
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

- [Assets/Scripts/Editor/GltfPropImporter.cs](<Assets/Scripts/Editor/GltfPropImporter.cs>) — Исходник C#: GltfPropImporter.
- [Assets/Scripts/Editor/PirateCharacterImport.cs](<Assets/Scripts/Editor/PirateCharacterImport.cs>) — Исходник C#: PirateCharacterImport.
- [Assets/Scripts/Editor/SailRiggingArtSetup.cs](<Assets/Scripts/Editor/SailRiggingArtSetup.cs>) — Исходник C#: SailRiggingArtSetup.
- [Assets/Scripts/Editor/MainShipSetup.cs](<Assets/Scripts/Editor/MainShipSetup.cs>) — Исходник C#: MainShipSetup.
- [../NewShip/Ship_V3_Fitted.blend](<../NewShip/Ship_V3_Fitted.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Editor/ShipV3ImportSetup.cs](<Assets/Scripts/Editor/ShipV3ImportSetup.cs>) — Исходник C#: ShipV3ImportSetup.
- [Tools/ShipV3/ExportFromOpenBlender.py](<Tools/ShipV3/ExportFromOpenBlender.py>) — Инструмент Python.
- [Assets/Models/Ships/ShipV3/ShipV3.fbx](<Assets/Models/Ships/ShipV3/ShipV3.fbx>) — Модель / анимации FBX.
- [Assets/Models/Ships/ShipV3/ShipV3.json](<Assets/Models/Ships/ShipV3/ShipV3.json>) — Конфигурация / данные JSON.
- [Art/Blender/Ships/ShipV3/ShipV3_Fitted.blend](<Art/Blender/Ships/ShipV3/ShipV3_Fitted.blend>) — Редактируемая сцена Blender.
- [Assets/Scripts/Editor/ShipV3BindingRepair.cs](<Assets/Scripts/Editor/ShipV3BindingRepair.cs>) — Исходник C#: ShipV3BindingRepair.
- [Tools/ShipV3/BindingRepairStatus.txt](<Tools/ShipV3/BindingRepairStatus.txt>) — Текстовые данные.
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
- [blender.md](<blender.md>) — Документация.
- [unity.md](<unity.md>) — Документация.
- [frigate.md](<frigate.md>) — Документация.
- [../NewShip/V3Preparation/TelescopeAndAnchorControls.md](<../NewShip/V3Preparation/TelescopeAndAnchorControls.md>) — Документация.
- [../NewShip/External/AnchorNikdane12/SOURCES.md](<../NewShip/External/AnchorNikdane12/SOURCES.md>) — Документация.
- [../NewShip/V3Preparation/HarpoonControls.md](<../NewShip/V3Preparation/HarpoonControls.md>) — Документация.
- [../NewShip/V3Preparation/BowTextureRestoreReport.json](<../NewShip/V3Preparation/BowTextureRestoreReport.json>) — Конфигурация / данные JSON.
- [../NewShip/V3Preparation/HoldDispenserControls.md](<../NewShip/V3Preparation/HoldDispenserControls.md>) — Документация.
- [../NewShip/V3Preparation/ShipDetailsControls.md](<../NewShip/V3Preparation/ShipDetailsControls.md>) — Документация.
- [../NewShip/FreeAssets/CREDITS.md](<../NewShip/FreeAssets/CREDITS.md>) — Документация.
- [../NewShip/V3Preparation/OptimizationAndDestructionControls.md](<../NewShip/V3Preparation/OptimizationAndDestructionControls.md>) — Документация.
- [../NewShip/V3Preparation/V18Validation.json](<../NewShip/V3Preparation/V18Validation.json>) — Конфигурация / данные JSON.
- [../NewShip/V3Preparation/V19Validation.json](<../NewShip/V3Preparation/V19Validation.json>) — Конфигурация / данные JSON.

### Звуки и голос (`audio`)

Ключевые слова: звук, звуки, голос, voice.

Назначения звуков хранить в существующем GameAudioBank. Источники и лицензии проверять в CREDITS.
Голосовой чат — отдельная система от игровых звуков. SessionVoiceMenu переключает сохранённый режим VOIP: удержание V (по умолчанию) или активация микрофона по RMS-порогу -60..-20 дБ (по умолчанию -40), с хвостом 0.3 с. PirateVoiceInputFilter фильтрует исходящие кадры до кодирования; запреты в меню, без фокуса и при смерти сохраняются.
Взлом плота: шесть Lockpick cues в GameAudioBank. Движение отмычки, вращение и заедание звучат локально с ограничением частоты; начало, поломка и успех подтверждаются сервером и слышны рядом. Короткие CC0-записи и обработка перечислены в Assets/Audio/Lockpick/SOURCE.md; варианты движения/заедания не повторяются подряд.

- [Assets/Scripts/Audio/GameAudio.cs](<Assets/Scripts/Audio/GameAudio.cs>) — Исходник C#: GameAudio.
- [Assets/Scripts/Audio/GameAudioBank.cs](<Assets/Scripts/Audio/GameAudioBank.cs>) — Исходник C#: SoundCue, GameAudioBank, Entry.
- [Assets/Scripts/Audio/GameplayAudio.cs](<Assets/Scripts/Audio/GameplayAudio.cs>) — Исходник C#: GameplayAudio.
- [Assets/Scripts/Audio/PirateVoiceChat.cs](<Assets/Scripts/Audio/PirateVoiceChat.cs>) — Исходник C#: PirateVoiceChat.
- [Assets/Scripts/Audio/PirateVoiceInputFilter.cs](<Assets/Scripts/Audio/PirateVoiceInputFilter.cs>) — Исходник C#: PirateVoiceInputFilter.
- [Assets/Scripts/Networking/SessionVoiceMenu.cs](<Assets/Scripts/Networking/SessionVoiceMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/NetworkPlayer.Voice.cs](<Assets/Scripts/Networking/NetworkPlayer.Voice.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/World/StormWeather.cs](<Assets/Scripts/World/StormWeather.cs>) — Исходник C#: StormWeather.
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
- [AudioIntegration.md](<AudioIntegration.md>) — Документация.
- [Assets/Audio/CREDITS.md](<Assets/Audio/CREDITS.md>) — Документация.

### Меню и HUD (`ui`)

Ключевые слова: интерфейс, меню, hud.

SessionController разделён на partial-файлы меню. Различать меню сессии и игровой HUD.
Сохранённое оформление меню и его runtime-поведение имеют разные точки входа.
GameTelemetry подключается в SessionController.Awake. Настройки → Игра и интерфейс → Полная телеметрия сохраняют ShowTelemetry (по умолчанию выключено). Локальная панель справа под HUD шторма обновляется раз в 0.5 с: FPS/кадр, доступные CPU/Render/GPU и render counters, Unity/GC память, RTT/тики/роль, загруженные игроки/корабли/сундуки, XYZ и скорость корабля. Недоступные счётчики показаны прочерком; сбор отключён вместе с панелью.
MenuPresentationSetup.ReplaceShip создаёт фон меню из геометрии Ship V3 без сетевых и физических компонентов. Копируются только активные ветки и включённые рендереры: сохранённые объединённые меши учитываются, их выключенные исходники не дублируются. Старый MenuShip удалён из NetworkMenu; исходный старый префаб сохранён.

- [Assets/Scripts/Networking/SessionMenu.cs](<Assets/Scripts/Networking/SessionMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionPartyMenu.cs](<Assets/Scripts/Networking/SessionPartyMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/UI/PlayerHud.cs](<Assets/Scripts/UI/PlayerHud.cs>) — Исходник C#: PlayerHud.
- [Assets/Scripts/UI/GameTelemetry.cs](<Assets/Scripts/UI/GameTelemetry.cs>) — Исходник C#: GameTelemetry.
- [Assets/Scripts/UI/PirateHudStyle.cs](<Assets/Scripts/UI/PirateHudStyle.cs>) — Исходник C#: PirateHudStyle.
- [Assets/Scripts/UI/HudLayout.cs](<Assets/Scripts/UI/HudLayout.cs>) — Исходник C#: HudLayout, Scope.
- [Assets/Scripts/Player/MenuBackdrop.cs](<Assets/Scripts/Player/MenuBackdrop.cs>) — Исходник C#: MenuBackdrop.
- [Assets/Scripts/Editor/MenuPresentationSetup.cs](<Assets/Scripts/Editor/MenuPresentationSetup.cs>) — Исходник C#: MenuPresentationSetup.
- [unity.md](<unity.md>) — Документация.
- [qol-roadmap.md](<qol-roadmap.md>) — Документация.

### Шторм, зона и объёмный туман (`storm`)

Ключевые слова: шторм, зона, туман, fog, brzone.

Логика зоны и её сетевое состояние находятся в StormZone и SessionStorm; визуал объёмного шторма — в BRZoneVolumetric. SeaMistRendererFeature задаёт туман 15% по умолчанию; OceanSurface при запуске NetworkOcean применяет тот же множитель к обычному туману. DeveloperMenu показывает процент и меняет оба вида тумана от общей базовой плотности.
В каталоге Assets/Game также есть BRZoneV2, BRZoneV3 и BRZoneFinal. Наличие нескольких вариантов не означает, что все подключены: проверять ссылки только нужной сцены/префаба.

- [Assets/Scripts/World/StormZone.cs](<Assets/Scripts/World/StormZone.cs>) — Исходник C#: StormZone.
- [Assets/Scripts/Networking/SessionStorm.cs](<Assets/Scripts/Networking/SessionStorm.cs>) — Исходник C#: StormMessage, SessionController.
- [Assets/Scripts/World/StormWeather.cs](<Assets/Scripts/World/StormWeather.cs>) — Исходник C#: StormWeather.
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

- [Assets/Scripts/Harpoon/HarpoonGun.cs](<Assets/Scripts/Harpoon/HarpoonGun.cs>) — Исходник C#: HarpoonGun.
- [Assets/Scripts/Harpoon/HarpoonProjectile.cs](<Assets/Scripts/Harpoon/HarpoonProjectile.cs>) — Исходник C#: HarpoonProjectile, HarpoonState.
- [Assets/Scripts/Harpoon/HarpoonHookTarget.cs](<Assets/Scripts/Harpoon/HarpoonHookTarget.cs>) — Исходник C#: HarpoonHookTarget.
- [Assets/Scripts/Harpoon/HarpoonShipMount.cs](<Assets/Scripts/Harpoon/HarpoonShipMount.cs>) — Исходник C#: HarpoonShipMount.
- [Assets/Scripts/Networking/NetworkShip.Harpoon.cs](<Assets/Scripts/Networking/NetworkShip.Harpoon.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Ships/ShipV3HarpoonVisual.cs](<Assets/Scripts/Ships/ShipV3HarpoonVisual.cs>) — Исходник C#: ShipV3HarpoonVisual.
- [Assets/Resources/Ships/ShipV3HarpoonPort.prefab](<Assets/Resources/Ships/ShipV3HarpoonPort.prefab>) — Префаб Unity.
- [Assets/Resources/Ships/ShipV3HarpoonStarboard.prefab](<Assets/Resources/Ships/ShipV3HarpoonStarboard.prefab>) — Префаб Unity.
- [../NewShip/V3Preparation/HarpoonControls.md](<../NewShip/V3Preparation/HarpoonControls.md>) — Документация.

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

Рыбалка — NetworkFishing; предмет рыбы и его полёт — NetworkFish и NetworkFishProjectile. Использование рыбы как метательного предмета находится в NetworkWeapon.FishThrows.
Модели обычной рыбы, фугу и рыбы-меча заменены файлами из ../Blender/Лутабельные/Рыба. Исходники в Art/Blender/Loot/FishReplacement, игровые FBX и материалы URP в Assets/Models/Loot/Replacement/Fish, Pufferfish, Swordfish. BottleFishReplacementSetup сохраняет существующие FishVisual, PufferfishVisual и SwordfishVisual GUID и подменяет дочернюю геометрию двух специальных pickup. Коллайдеры, NetworkFish, NetworkFishProjectile, направление головы +Z, подбор, рыбалка, броски, раздувание фугу и втыкание рыбы-меча сохранены. Игровая проверка выполняется пользователем.

- [Assets/Scripts/Networking/NetworkFishing.cs](<Assets/Scripts/Networking/NetworkFishing.cs>) — Исходник C#: NetworkFishing.
- [Assets/Scripts/Networking/NetworkFish.cs](<Assets/Scripts/Networking/NetworkFish.cs>) — Исходник C#: InventoryItem, NetworkFish.
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

- [Assets/Scripts/World/EnvironmentTestGallery.cs](<Assets/Scripts/World/EnvironmentTestGallery.cs>) — Исходник C#: EnvironmentTestGallery.
- [Assets/Scripts/Editor/EnvironmentTestSetup.cs](<Assets/Scripts/Editor/EnvironmentTestSetup.cs>) — Исходник C#: EnvironmentTestSetup.
- [Assets/Scripts/World/WhirlpoolTest.cs](<Assets/Scripts/World/WhirlpoolTest.cs>) — Исходник C#: WhirlpoolTest.
- [Assets/Scripts/World/SeabedTerrain.cs](<Assets/Scripts/World/SeabedTerrain.cs>) — Исходник C#: SeabedTerrain.
- [Assets/Scripts/WaterGridGenerator.cs](<Assets/Scripts/WaterGridGenerator.cs>) — Исходник C#: WaterGridGenerator.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
- [Assets/Scripts/World/StormZone.cs](<Assets/Scripts/World/StormZone.cs>) — Исходник C#: StormZone.
- [Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader](<Assets/Houidisoft technology/Simple water/Shaders/SimpleWaterURP.shader>) — Шейдер.
- [Assets/Scripts/World/WhirlpoolVFX.cs](<Assets/Scripts/World/WhirlpoolVFX.cs>) — Исходник C#: WhirlpoolVFX.
- [Assets/Scripts/Ships/ShipV3TestSpawner.cs](<Assets/Scripts/Ships/ShipV3TestSpawner.cs>) — Исходник C#: ShipV3TestSpawner.
- [Assets/Scripts/Ships/ShipV3PlayerInteraction.cs](<Assets/Scripts/Ships/ShipV3PlayerInteraction.cs>) — Исходник C#: ShipV3PlayerInteraction.
- [Assets/Scripts/Ships/ShipV3InteractionTarget.cs](<Assets/Scripts/Ships/ShipV3InteractionTarget.cs>) — Исходник C#: ShipV3InteractionTarget.
- [Assets/Scripts/Ships/ShipV3BellContact.cs](<Assets/Scripts/Ships/ShipV3BellContact.cs>) — Исходник C#: ShipV3BellContact.
- [Assets/Scripts/Ships/ShipV3ClothMotion.cs](<Assets/Scripts/Ships/ShipV3ClothMotion.cs>) — Исходник C#: ShipV3ClothMotion.
- [Assets/Resources/Ships/ShipV3Test.prefab](<Assets/Resources/Ships/ShipV3Test.prefab>) — Префаб Unity.
- [Assets/Scripts/Editor/ShipV3GameplayRepair.cs](<Assets/Scripts/Editor/ShipV3GameplayRepair.cs>) — Исходник C#: ShipV3GameplayRepair.

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
