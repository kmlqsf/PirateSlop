# PirateSlop — карта проекта

Снимок файлов: 2026-09-29. Корень: `C:\Users\K\Project`.

## Как пользоваться

Сначала прочитай `AGENTS.md`, `lessons.md` и эту карту. Выбери механику ниже; если нужного файла нет среди точек входа, открой соответствующий каталог из таблицы. Ищи имя внутри этих Markdown-файлов, затем читай исходник по указанному пути. Не запускай обзор папок или поиск файлов по всему проекту при каждом новом чате.

Карта описывает сохранённые файлы, а не живую сцену и не результаты игровых проверок. Назначения механик взяты из поддерживаемого `Tools/Context/topics.json`; в полном каталоге указаны тип файла, объявленные C#-типы и известные тематические связи. Для остальных файлов семантика не угадывается по имени. Подключение компонента и актуальную реализацию проверяй только для затронутой задачи.

Все пути относительно корня проекта. Полный каталог разбит на приложения, чтобы не загружать тысячи строк в каждый чат.

## Полный каталог

Учтено 3683 файлов без `.meta`. Ещё 3981 файлов `.meta` сопровождают ассеты/папки: их путь — путь ассета или папки плюс `.meta`; сохраняй их GUID. Сама карта и её автоматически созданные приложения не входят в подсчёт.

| Раздел | Назначение | Файлов |
| --- | --- | ---: |
| [.agents](<Docs/ProjectMap/.agents.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.cursor](<Docs/ProjectMap/.cursor.md>) | Ресурсы раздела; точный состав — в каталоге | 1 |
| [.opencode](<Docs/ProjectMap/.opencode.md>) | Ресурсы раздела; точный состав — в каталоге | 4 |
| [Art](<Docs/ProjectMap/Art.md>) | Исходники арта и Blender | 98 |
| [Assets](<Docs/ProjectMap/Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 3 |
| [Assets/Animations](<Docs/ProjectMap/Assets-Animations.md>) | Анимации | 51 |
| [Assets/Audio](<Docs/ProjectMap/Assets-Audio.md>) | Звуковые ресурсы и лицензии | 173 |
| [Assets/Branding](<Docs/ProjectMap/Assets-Branding.md>) | Оформление проекта | 1 |
| [Assets/Editor](<Docs/ProjectMap/Assets-Editor.md>) | Редакторские ресурсы | 1 |
| [Assets/Fog Particles](<Docs/ProjectMap/Assets-Fog Particles.md>) | Ресурсы раздела; точный состав — в каталоге | 17 |
| [Assets/Game](<Docs/ProjectMap/Assets-Game.md>) | Игровые подсистемы и эффекты | 79 |
| [Assets/Houidisoft technology](<Docs/ProjectMap/Assets-Houidisoft technology.md>) | Ресурсы раздела; точный состав — в каталоге | 15 |
| [Assets/JMO Assets](<Docs/ProjectMap/Assets-JMO Assets.md>) | Ресурсы раздела; точный состав — в каталоге | 488 |
| [Assets/Materials](<Docs/ProjectMap/Assets-Materials.md>) | Материалы | 146 |
| [Assets/Mirza](<Docs/ProjectMap/Assets-Mirza.md>) | Ресурсы раздела; точный состав — в каталоге | 186 |
| [Assets/Models](<Docs/ProjectMap/Assets-Models.md>) | Модели и связанные ресурсы | 789 |
| [Assets/Plugins](<Docs/ProjectMap/Assets-Plugins.md>) | Плагины | 5 |
| [Assets/Prefabs](<Docs/ProjectMap/Assets-Prefabs.md>) | Готовые игровые объекты | 127 |
| [Assets/Resources](<Docs/ProjectMap/Assets-Resources.md>) | Ресурсы, доступные для загрузки по имени | 37 |
| [Assets/Scenes](<Docs/ProjectMap/Assets-Scenes.md>) | Сохранённые сцены | 3 |
| [Assets/Scripts](<Docs/ProjectMap/Assets-Scripts.md>) | Игровой код и редакторские инструменты | 294 |
| [Assets/Settings](<Docs/ProjectMap/Assets-Settings.md>) | Настройки игровых систем и рендеринга | 43 |
| [Assets/Shaders](<Docs/ProjectMap/Assets-Shaders.md>) | Шейдеры | 7 |
| [Assets/Tests](<Docs/ProjectMap/Assets-Tests.md>) | Исходники проверок | 31 |
| [Assets/ThirdParty](<Docs/ProjectMap/Assets-ThirdParty.md>) | Сторонние ресурсы | 27 |
| [Assets/TutorialInfo](<Docs/ProjectMap/Assets-TutorialInfo.md>) | Ресурсы раздела; точный состав — в каталоге | 7 |
| [Assets/UI](<Docs/ProjectMap/Assets-UI.md>) | Ресурсы интерфейса | 32 |
| [Assets/_Recovery](<Docs/ProjectMap/Assets-_Recovery.md>) | Сохранённые восстановленные данные | 7 |
| [Docs](<Docs/ProjectMap/Docs.md>) | Документы и сохранённые отчёты | 29 |
| [Packages](<Docs/ProjectMap/Packages.md>) | Манифест, lock-файл и встроенные пакеты | 916 |
| [ProjectSettings](<Docs/ProjectMap/ProjectSettings.md>) | Настройки Unity | 28 |
| [Root](<Docs/ProjectMap/Root.md>) | Корневые инструкции, планы и служебные файлы | 29 |
| [ThirdParty](<Docs/ProjectMap/ThirdParty.md>) | Сторонние ресурсы | 2 |
| [Tools](<Docs/ProjectMap/Tools.md>) | Инструменты разработки и загрузчик контекста | 6 |

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

- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionSpectator.cs](<Assets/Scripts/Networking/SessionSpectator.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/BotSpectatorCamera.cs](<Assets/Scripts/Networking/BotSpectatorCamera.cs>) — Исходник C#: BotSpectatorCamera.
- [Assets/Scripts/Networking/SessionConfig.cs](<Assets/Scripts/Networking/SessionConfig.cs>) — Исходник C#: SessionConfig.
- [Assets/Scenes/NetworkMenu.unity](<Assets/Scenes/NetworkMenu.unity>) — Сцена Unity.
- [Assets/Scenes/NetworkOcean.unity](<Assets/Scenes/NetworkOcean.unity>) — Сцена Unity.
- [Assets/Prefabs/Networking/NetworkShip.prefab](<Assets/Prefabs/Networking/NetworkShip.prefab>) — Префаб Unity.
- [Assets/Prefabs/Networking/NetworkPlayer.prefab](<Assets/Prefabs/Networking/NetworkPlayer.prefab>) — Префаб Unity.
- [project.md](<project.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Движение корабля и палуба (`ship`)

Ключевые слова: корабль, палуба, helm, штурвал.

Корабль перемещается кинематически. Не переносить пассажира или предмет дважды вместе с кораблём; Rigidbody.GetPointVelocity не обязательно описывает это движение.
Управление и освобождение штурвала согласовывать с сервером; старые альтернативные контроллеры не выбирать только по имени.

- [Assets/Scripts/ShipController.cs](<Assets/Scripts/ShipController.cs>) — Исходник C#: ShipController.
- [Assets/Scripts/ShipDeckPassenger.cs](<Assets/Scripts/ShipDeckPassenger.cs>) — Исходник C#: ShipDeckPassenger.
- [Assets/Scripts/HelmInteraction.cs](<Assets/Scripts/HelmInteraction.cs>) — Исходник C#: HelmInteraction.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Interaction/DirectShipControls.cs](<Assets/Scripts/Interaction/DirectShipControls.cs>) — Исходник C#: DirectShipControls.
- [Assets/Scripts/Stations/CapstanStation.cs](<Assets/Scripts/Stations/CapstanStation.cs>) — Исходник C#: CapstanStation.
- [Assets/Scripts/Networking/NetworkShip.Anchor.cs](<Assets/Scripts/Networking/NetworkShip.Anchor.cs>) — Исходник C#: NetworkShip.
- [Assets/Prefabs/Networking/NetworkShip.prefab](<Assets/Prefabs/Networking/NetworkShip.prefab>) — Префаб Unity.
- [Assets/Prefabs/Ships/ShipSkeletonTest.prefab](<Assets/Prefabs/Ships/ShipSkeletonTest.prefab>) — Префаб Unity.
- [unity.md](<unity.md>) — Документация.
- [multiplayer-plan.md](<multiplayer-plan.md>) — Документация.

### Паруса и канаты (`sails`)

Ключевые слова: паруса, парус, канаты, rigging.

Разделять управление натяжением и визуальную геометрию канатов. Изменение модели не должно менять сетевую занятость.
Настройка канатов и обновление их арта — отдельные редакторские операции; перед повторным запуском изучить соответствующий setup.

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
- [unity.md](<unity.md>) — Документация.
- [blender.md](<blender.md>) — Документация.

### Пушки, ядра и лафеты (`cannons`)

Ключевые слова: пушки, пушка, cannon, ядра, мортира.

Проверять серверные условия выстрела, загрузки и занятости; локальные эффекты не подтверждают сетевой выстрел.
Для движения ядра и лафета учитывать движение корабля. Баланс брать из текущих полей и ассетов, а не старых записей.

- [Assets/Scripts/Cannons/SimpleCannon.cs](<Assets/Scripts/Cannons/SimpleCannon.cs>) — Исходник C#: SimpleCannon.
- [Assets/Scripts/Cannons/NetworkCannon.cs](<Assets/Scripts/Cannons/NetworkCannon.cs>) — Исходник C#: CannonPlacement, NetworkCannon.
- [Assets/Scripts/Cannons/Cannonball.cs](<Assets/Scripts/Cannons/Cannonball.cs>) — Исходник C#: Cannonball.
- [Assets/Scripts/Cannons/CannonShotDamage.cs](<Assets/Scripts/Cannons/CannonShotDamage.cs>) — Исходник C#: CannonShotDamage.
- [Assets/Scripts/Cannons/CannonCarriage.cs](<Assets/Scripts/Cannons/CannonCarriage.cs>) — Исходник C#: CannonCarriage.
- [Assets/Scripts/Cannons/MortarTrajectory.cs](<Assets/Scripts/Cannons/MortarTrajectory.cs>) — Исходник C#: MortarTrajectory.
- [Assets/Scripts/Networking/NetworkLooseCannonball.cs](<Assets/Scripts/Networking/NetworkLooseCannonball.cs>) — Исходник C#: NetworkLooseCannonball.
- [Assets/Scripts/Editor/CannonInventorySetup.cs](<Assets/Scripts/Editor/CannonInventorySetup.cs>) — Исходник C#: CannonInventorySetup.
- [combat-balance.md](<combat-balance.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Персонаж, камера и анимации (`player`)

Ключевые слова: игрок, персонаж, камера, анимации.

Ввод и камера принадлежат локальному игроку; движение наблюдателей и анимации сверять с сетевым состоянием.
Проверять привязку компонентов к NetworkPlayer. Совпадение имён костей не гарантирует совместимость анимаций.
NetworkPlayer использует Tripo/Mixamo Walking.fbx из NewPirate. Лицо и борода имеют исправленные веса Head; исходник и способ сохранения FBX — Art/Blender/Characters/NewPirate/README.md.

- [Assets/Scripts/AdvancedPlayerController.cs](<Assets/Scripts/AdvancedPlayerController.cs>) — Исходник C#: AdvancedPlayerController.
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
- [animation-review.md](<animation-review.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Личное оружие и урон (`weapons`)

Ключевые слова: оружие, пистолет, бой, combat.

Разделять локальный отклик оружия и серверное подтверждение урона/расхода боеприпасов.
Настройки оружия искать через FirearmDefinition и используемые ссылки; не копировать числовой баланс из истории.
SabreAnimation использует отдельный хват Mixamo и переносит движение игровой сабли в координаты камеры для первого лица. Слой SabreCombat активен при выбранной сабле; Ready использует New_SabreReady. NewPirateAnimationBatch сохраняет эту стойку при переимпорте.

- [Assets/Scripts/Player/PirateWeapon.cs](<Assets/Scripts/Player/PirateWeapon.cs>) — Исходник C#: IWeaponTarget, PirateWeapon.
- [Assets/Scripts/Networking/NetworkWeapon.cs](<Assets/Scripts/Networking/NetworkWeapon.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Player/FirearmDefinition.cs](<Assets/Scripts/Player/FirearmDefinition.cs>) — Исходник C#: FirearmDefinition, FirearmCombat.
- [Assets/Scripts/Player/FirearmHandling.cs](<Assets/Scripts/Player/FirearmHandling.cs>) — Исходник C#: FirearmHandling.
- [Assets/Scripts/Player/CombatHealth.cs](<Assets/Scripts/Player/CombatHealth.cs>) — Исходник C#: CombatHealth.
- [Assets/Scripts/Networking/NetworkHealth.cs](<Assets/Scripts/Networking/NetworkHealth.cs>) — Исходник C#: NetworkHealth.
- [Assets/Scripts/Editor/FirearmSetup.cs](<Assets/Scripts/Editor/FirearmSetup.cs>) — Исходник C#: FirearmSetup.
- [Assets/Scripts/Player/SabreAnimation.cs](<Assets/Scripts/Player/SabreAnimation.cs>) — Исходник C#: SabreAnimation.
- [Assets/Scripts/Editor/NewPirateAnimationBatch.cs](<Assets/Scripts/Editor/NewPirateAnimationBatch.cs>) — Исходник C#: NewPirateAnimationBatch.
- [firearm-foundation.md](<firearm-foundation.md>) — Документация.
- [combat-balance.md](<combat-balance.md>) — Документация.

### Предметы, лут и инвентарь (`loot`)

Ключевые слова: лут, предметы, инвентарь, inventory.

Подбор и расход предметов подтверждает сервер. Сверять идентификаторы предметов, иконки и каталог.
NetworkWeapon имеет отдельные partial-файлы; для морского лута начать с NetworkWeapon.SeaLoot.cs.

- [Assets/Scripts/Player/PlayerInventory.cs](<Assets/Scripts/Player/PlayerInventory.cs>) — Исходник C#: PlayerInventory.
- [Assets/Scripts/Loot/LootCatalog.cs](<Assets/Scripts/Loot/LootCatalog.cs>) — Исходник C#: LootCatalog, Entry.
- [Assets/Scripts/Loot/InventoryIcons.cs](<Assets/Scripts/Loot/InventoryIcons.cs>) — Исходник C#: InventoryIcons.
- [Assets/Scripts/Networking/NetworkWeapon.cs](<Assets/Scripts/Networking/NetworkWeapon.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkWeapon.SeaLoot.cs](<Assets/Scripts/Networking/NetworkWeapon.SeaLoot.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Networking/NetworkLootChest.cs](<Assets/Scripts/Networking/NetworkLootChest.cs>) — Исходник C#: NetworkLootChest.
- [Assets/Settings/Loot/DefaultLoot.asset](<Assets/Settings/Loot/DefaultLoot.asset>) — Настройки или данные Unity.
- [Assets/Scripts/Editor/LootSetup.cs](<Assets/Scripts/Editor/LootSetup.cs>) — Исходник C#: LootSetup.
- [Assets/Scripts/Player/ShipSpyglassView.cs](<Assets/Scripts/Player/ShipSpyglassView.cs>) — Исходник C#: ShipSpyglassView.
- [Assets/Scripts/Editor/SpyglassSetup.cs](<Assets/Scripts/Editor/SpyglassSetup.cs>) — Исходник C#: SpyglassSetup.
- [rum-loot.md](<rum-loot.md>) — Документация.
- [combat-balance.md](<combat-balance.md>) — Документация.

### Повреждения корпуса, ремонт и затопление (`repair`)

Ключевые слова: ремонт, repairing, разрушение, затопление, damage.

Текущая точка входа ремонта — NetworkHullRepair, а не старый путь Scripts/Repair/ShipRepair.cs.
Урон, ремонт фрагментов и затопление связаны с секциями корабля. Старое описание накладных досок не считать актуальной архитектурой.

- [Assets/Scripts/Networking/NetworkHullRepair.cs](<Assets/Scripts/Networking/NetworkHullRepair.cs>) — Исходник C#: NetworkHullRepair.
- [Assets/Scripts/ShipDestruction/ShipDestruction.cs](<Assets/Scripts/ShipDestruction/ShipDestruction.cs>) — Исходник C#: ShipSectionSnapshot, ShipDestructionEvent, ShipDestruction.
- [Assets/Scripts/ShipDestruction/ShipDamageSection.cs](<Assets/Scripts/ShipDestruction/ShipDamageSection.cs>) — Исходник C#: ShipDamageSection.
- [Assets/Scripts/ShipDestruction/ShipFlooding.cs](<Assets/Scripts/ShipDestruction/ShipFlooding.cs>) — Исходник C#: ShipBreach, ShipFlooding.
- [Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs](<Assets/Scripts/ShipDestruction/ShipDestructionProfile.cs>) — Исходник C#: ShipSectionState, ShipSectionType, ShipDamageReason, ShipAmmoMultiplier, ShipSectionDefinition, ShipDestructionProfile, ShipFragmentConnection.
- [Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs](<Assets/Scripts/ShipDestruction/ShipStructuralGraph.cs>) — Исходник C#: ShipStructuralGraph.
- [Assets/Scripts/Editor/ShipDestructionSetup.cs](<Assets/Scripts/Editor/ShipDestructionSetup.cs>) — Исходник C#: ShipDestructionSetup, Manifest, Record.
- [ship-destruction.md](<ship-destruction.md>) — Документация.
- [unity.md](<unity.md>) — Документация.

### Мир, острова и океан (`world`)

Ключевые слова: мир, острова, карта, океан, water.

Согласовывать генерацию карты и её состояние между участниками; seed и профиль брать из используемых ассетов.
CPU-поверхность воды используется игровой логикой: визуальные волны нельзя менять независимо от OceanSurface без проверки связи.

- [Assets/Scripts/World/ProceduralWorld.cs](<Assets/Scripts/World/ProceduralWorld.cs>) — Исходник C#: ProceduralWorld.
- [Assets/Scripts/World/WorldProfile.cs](<Assets/Scripts/World/WorldProfile.cs>) — Исходник C#: WorldDecoration, WorldProfile.
- [Assets/Scripts/World/WorldGenerator.cs](<Assets/Scripts/World/WorldGenerator.cs>) — Исходник C#: WorldGenerator.
- [Assets/Scripts/World/BalancedWorldGenerator.cs](<Assets/Scripts/World/BalancedWorldGenerator.cs>) — Исходник C#: BalancedWorldGenerator.
- [Assets/Scripts/World/WorldDecorationPlacer.cs](<Assets/Scripts/World/WorldDecorationPlacer.cs>) — Исходник C#: WorldDecorationPlacer.
- [Assets/Scripts/OceanSurface.cs](<Assets/Scripts/OceanSurface.cs>) — Исходник C#: OceanSurface.
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

- [Assets/Scripts/Networking/SessionController.cs](<Assets/Scripts/Networking/SessionController.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionConfig.cs](<Assets/Scripts/Networking/SessionConfig.cs>) — Исходник C#: SessionConfig.
- [Assets/Scripts/Networking/SessionAuthenticator.cs](<Assets/Scripts/Networking/SessionAuthenticator.cs>) — Исходник C#: HelloMessage, AdmissionMessage, PopulationMessage, SessionAuthenticator.
- [Assets/Scripts/Networking/SteamParty.cs](<Assets/Scripts/Networking/SteamParty.cs>) — Исходник C#: SteamParty.
- [Assets/Scripts/Networking/NetworkPlayer.cs](<Assets/Scripts/Networking/NetworkPlayer.cs>) — Исходник C#: CaptainInput, CaptainState, NetworkPlayer.
- [Assets/Scripts/Networking/NetworkShip.cs](<Assets/Scripts/Networking/NetworkShip.cs>) — Исходник C#: NetworkShip.
- [Assets/Scripts/Networking/SimulationState.cs](<Assets/Scripts/Networking/SimulationState.cs>) — Исходник C#: PlayerCommand, PlayerState, ShipState.
- [Assets/Settings/Networking/SessionConfig.asset](<Assets/Settings/Networking/SessionConfig.asset>) — Настройки или данные Unity.
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

- [Assets/Scripts/Editor/GltfPropImporter.cs](<Assets/Scripts/Editor/GltfPropImporter.cs>) — Исходник C#: GltfPropImporter.
- [Assets/Scripts/Editor/PirateCharacterImport.cs](<Assets/Scripts/Editor/PirateCharacterImport.cs>) — Исходник C#: PirateCharacterImport.
- [Assets/Scripts/Editor/SailRiggingArtSetup.cs](<Assets/Scripts/Editor/SailRiggingArtSetup.cs>) — Исходник C#: SailRiggingArtSetup.
- [Assets/Scripts/Editor/MainShipSetup.cs](<Assets/Scripts/Editor/MainShipSetup.cs>) — Исходник C#: MainShipSetup.
- [blender.md](<blender.md>) — Документация.
- [unity.md](<unity.md>) — Документация.
- [frigate.md](<frigate.md>) — Документация.

### Звуки и голос (`audio`)

Ключевые слова: звук, звуки, голос, voice.

Назначения звуков хранить в существующем GameAudioBank. Источники и лицензии проверять в CREDITS.
Голосовой чат — отдельная система от игровых звуков.

- [Assets/Scripts/Audio/GameAudio.cs](<Assets/Scripts/Audio/GameAudio.cs>) — Исходник C#: GameAudio.
- [Assets/Scripts/Audio/GameAudioBank.cs](<Assets/Scripts/Audio/GameAudioBank.cs>) — Исходник C#: SoundCue, GameAudioBank, Entry.
- [Assets/Scripts/Audio/GameplayAudio.cs](<Assets/Scripts/Audio/GameplayAudio.cs>) — Исходник C#: GameplayAudio.
- [Assets/Scripts/Audio/PirateVoiceChat.cs](<Assets/Scripts/Audio/PirateVoiceChat.cs>) — Исходник C#: PirateVoiceChat.
- [Assets/Scripts/Networking/NetworkPlayer.Voice.cs](<Assets/Scripts/Networking/NetworkPlayer.Voice.cs>) — Исходник C#: NetworkPlayer.
- [Assets/Scripts/World/StormWeather.cs](<Assets/Scripts/World/StormWeather.cs>) — Исходник C#: StormWeather.
- [Assets/Scripts/Editor/AudioBankWindow.cs](<Assets/Scripts/Editor/AudioBankWindow.cs>) — Исходник C#: AudioBankWindow, Page.
- [Assets/Resources/GameAudioBank.asset](<Assets/Resources/GameAudioBank.asset>) — Настройки или данные Unity.
- [AudioIntegration.md](<AudioIntegration.md>) — Документация.
- [Assets/Audio/CREDITS.md](<Assets/Audio/CREDITS.md>) — Документация.

### Меню и HUD (`ui`)

Ключевые слова: интерфейс, меню, hud.

SessionController разделён на partial-файлы меню. Различать меню сессии и игровой HUD.
Сохранённое оформление меню и его runtime-поведение имеют разные точки входа.

- [Assets/Scripts/Networking/SessionMenu.cs](<Assets/Scripts/Networking/SessionMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/Networking/SessionPartyMenu.cs](<Assets/Scripts/Networking/SessionPartyMenu.cs>) — Исходник C#: SessionController.
- [Assets/Scripts/UI/PlayerHud.cs](<Assets/Scripts/UI/PlayerHud.cs>) — Исходник C#: PlayerHud.
- [Assets/Scripts/UI/PirateHudStyle.cs](<Assets/Scripts/UI/PirateHudStyle.cs>) — Исходник C#: PirateHudStyle.
- [Assets/Scripts/UI/HudLayout.cs](<Assets/Scripts/UI/HudLayout.cs>) — Исходник C#: HudLayout, Scope.
- [Assets/Scripts/Player/MenuBackdrop.cs](<Assets/Scripts/Player/MenuBackdrop.cs>) — Исходник C#: MenuBackdrop.
- [Assets/Scripts/Editor/MenuPresentationSetup.cs](<Assets/Scripts/Editor/MenuPresentationSetup.cs>) — Исходник C#: MenuPresentationSetup.
- [unity.md](<unity.md>) — Документация.
- [qol-roadmap.md](<qol-roadmap.md>) — Документация.

### Шторм, зона и объёмный туман (`storm`)

Ключевые слова: шторм, зона, туман, fog, brzone.

Логика зоны и её сетевое состояние находятся в StormZone и SessionStorm; визуал объёмного шторма — в BRZoneVolumetric.
В каталоге Assets/Game также есть BRZoneV2, BRZoneV3 и BRZoneFinal. Наличие нескольких вариантов не означает, что все подключены: проверять ссылки только нужной сцены/префаба.

- [Assets/Scripts/World/StormZone.cs](<Assets/Scripts/World/StormZone.cs>) — Исходник C#: StormZone.
- [Assets/Scripts/Networking/SessionStorm.cs](<Assets/Scripts/Networking/SessionStorm.cs>) — Исходник C#: StormMessage, SessionController.
- [Assets/Scripts/World/StormWeather.cs](<Assets/Scripts/World/StormWeather.cs>) — Исходник C#: StormWeather.
- [Assets/Game/BRZoneVolumetric/StormVolumeController.cs](<Assets/Game/BRZoneVolumetric/StormVolumeController.cs>) — Исходник C#: StormVolumeController.
- [Assets/Game/BRZoneVolumetric/SeaMistRendererFeature.cs](<Assets/Game/BRZoneVolumetric/SeaMistRendererFeature.cs>) — Исходник C#: SeaMistRendererFeature.
- [Assets/Game/BRZoneVolumetric/SeaMist.mat](<Assets/Game/BRZoneVolumetric/SeaMist.mat>) — Материал Unity.

### Гарпун и корабельное крепление (`harpoon`)

Ключевые слова: гарпун.

Наведение, выстрел, трос и прочность установки — HarpoonGun; снаряд и цель зацепления вынесены отдельно. Сетевая часть корабля — NetworkShip.Harpoon.

- [Assets/Scripts/Harpoon/HarpoonGun.cs](<Assets/Scripts/Harpoon/HarpoonGun.cs>) — Исходник C#: HarpoonGun.
- [Assets/Scripts/Harpoon/HarpoonProjectile.cs](<Assets/Scripts/Harpoon/HarpoonProjectile.cs>) — Исходник C#: HarpoonProjectile, HarpoonState.
- [Assets/Scripts/Harpoon/HarpoonHookTarget.cs](<Assets/Scripts/Harpoon/HarpoonHookTarget.cs>) — Исходник C#: HarpoonHookTarget.
- [Assets/Scripts/Harpoon/HarpoonShipMount.cs](<Assets/Scripts/Harpoon/HarpoonShipMount.cs>) — Исходник C#: HarpoonShipMount.
- [Assets/Scripts/Networking/NetworkShip.Harpoon.cs](<Assets/Scripts/Networking/NetworkShip.Harpoon.cs>) — Исходник C#: NetworkShip.

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

- [Assets/Scripts/Networking/NetworkFishing.cs](<Assets/Scripts/Networking/NetworkFishing.cs>) — Исходник C#: NetworkFishing.
- [Assets/Scripts/Networking/NetworkFish.cs](<Assets/Scripts/Networking/NetworkFish.cs>) — Исходник C#: InventoryItem, NetworkFish.
- [Assets/Scripts/Networking/NetworkFishProjectile.cs](<Assets/Scripts/Networking/NetworkFishProjectile.cs>) — Исходник C#: NetworkFishProjectile.
- [Assets/Scripts/Networking/NetworkWeapon.FishThrows.cs](<Assets/Scripts/Networking/NetworkWeapon.FishThrows.cs>) — Исходник C#: NetworkWeapon.
- [Assets/Scripts/Player/FishingRodBend.cs](<Assets/Scripts/Player/FishingRodBend.cs>) — Исходник C#: FishingRodBend, Part.
- [Assets/Scripts/Editor/FishingSetup.cs](<Assets/Scripts/Editor/FishingSetup.cs>) — Исходник C#: FishingSetup.

### Тестовая карта и водоворот (`test-gallery`)

Ключевые слова: тестовая карта, галерея, водоворот, whirlpool.

Точка входа тестовой карты — EnvironmentTestGallery, подготовка — EnvironmentTestSetup. Водоворот имеет отдельные файлы поведения и визуала; не путать этот режим со старой SampleScene.

- [Assets/Scripts/World/EnvironmentTestGallery.cs](<Assets/Scripts/World/EnvironmentTestGallery.cs>) — Исходник C#: EnvironmentTestGallery.
- [Assets/Scripts/Editor/EnvironmentTestSetup.cs](<Assets/Scripts/Editor/EnvironmentTestSetup.cs>) — Исходник C#: EnvironmentTestSetup.
- [Assets/Scripts/World/WhirlpoolTest.cs](<Assets/Scripts/World/WhirlpoolTest.cs>) — Исходник C#: WhirlpoolTest.
- [Assets/Scripts/World/WhirlpoolVFX.cs](<Assets/Scripts/World/WhirlpoolVFX.cs>) — Исходник C#: WhirlpoolVFX.

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
