# PirateSlop — инструкции для Claude

Основные правила проекта — в AGENTS.md; они обязательны и имеют приоритет над этим файлом, кроме раздела «Среда Claude» ниже, который только описывает инструменты.

@AGENTS.md

## Порядок работы в каждом чате

1. Прочитать `lessons.md` (большой, ~1100 строк: новые уроки сверху; искать по теме задачи через grep, а не читать целиком каждый раз) и `PROJECT_MAP.md` (≈1 МБ: сначала оглавление `### ... (`id`)`, затем нужный раздел и связанный `Docs/ProjectMap/*.md`).
2. Читать только нужные исходники по точным путям из карты. Не сканировать проект целиком; при отсутствии пути — спросить разрешение на ограниченный поиск.
3. Отвечать по-русски, кратко; код, файлы, объекты — по-английски. Без комментариев в коде и XML-доков. Частичные правки показывать diff/блоком.
4. По умолчанию не запускать Play Mode, билды, тесты и профилирование. Исключение — целевой замер производительности существенных изменений (см. AGENTS.md); если замер не выполнен — писать «производительность ожидает проверки».
5. При исправлении пользователем моей ошибки — дописать урок в начало `lessons.md` (формат `## YYYY-MM-DD — Тема` + пункт).
6. Git: коммит и push только после явного подтверждения пользователя для конкретных изменений. Без force push, reset, clean, stash. Конфликты — объяснить простыми словами и ждать решения.
7. Новый ивент на тестовой сцене обязательно с телепортом F8 → Телепортация → Ивенты.
8. При добавлении/удалении/переименовании файлов — обновить тему в `Tools/Context/topics.json` и пересобрать карту `python Tools/Context/update_project_map.py`.

## Ключевые факты проекта

- Репозиторий: https://github.com/kmlqsf/PirateSlop, ветки `main` (текущая) и `Боты`. Git LFS обязателен (FBX, BLEND и др.).
- Unity `6000.6.0f1`, URP 17.6.0, FishNet (Tugboat + FishySteamworks), Steamworks.NET, Steam AppID 480 в `steam_appid.txt`.
- Сцены в билде: `Assets/Scenes/NetworkMenu.unity` → `Assets/Scenes/NetworkOcean.unity`. Play Mode стартует с NetworkMenu.
- Сеть: `Assets/Settings/Networking/SessionConfig.asset` — порт 7777, 30 игроков, тик 30. Номер ProtocolVersion поднимать при изменении сетевого протокола (оба клиента должны обновиться).
- Единственный игровой корабль: `Assets/Resources/Ships/ShipV3Test.prefab` (имя историческое, GUID сохранять). Игрок: `Assets/Prefabs/Networking/NetworkPlayer.prefab`.
- Код: `Assets/Scripts/**`, namespace `PirateSlop`. Редакторские настройщики — меню `PirateSlop > ...`.
- Исходники моделей: `Art/Blender/**`; экспорт FBX → `Assets/Models/**` (Forward −Z, Up Y, 1 ед. = 1 м). Стиль — stylized low-poly.
- Производительность: журнал `Docs/Performance/OptimizationPlan.md`, скрипты `Tools/Performance/`.
- Инспектор объектов: через Unity MCP `execute_code` → `return PirateSlop.Editor.FocusedInspector.CaptureAsset("<путь>", false, "<Компонент>");` (см. `Tools/Context/inspector.md`).
- Не включать GPU Resident Drawer (причина крашей сборки). NavMeshData/TerrainData — бинарные, не трогать eol.

## Среда Claude (Claude desktop + облачная сессия)

- Проект на ПК пользователя: `C:\Users\K\Project` (в PROJECT_MAP указан другой корень `D:\projects\Pirate_BR\PirateGame` — это ПК второго разработчика; все пути в карте относительные).
- Файлы: чтение через `device_stage_files` / `device_list_dir`, запись правок — локальная копия + `device_commit_files` с `expectedMtimeMs` (не перезаписывать изменённый пользователем файл). Перед правкой .cs/.asset в Unity — по возможности использовать Unity MCP (`apply_text_edits`, `script_apply_edits`, `manage_*`), чтобы редактор сразу увидел изменения.
- Unity: инструменты `unityMCP` (endpoint `http://127.0.0.1:8080/mcp`, пакет CoplayDev MCP for Unity). Работают только когда Unity Editor открыт с этим проектом и мост запущен (Window > MCP for Unity). Перед работой проверить активную сцену, isDirty, isPlaying, isCompiling. Учитывать урок 2026-10-19: refresh/компиляция может сохранить посторонние dirty-меши через FishNet.
- Blender: bridge `127.0.0.1:9876` (TCP JSON с `\0`-разделителем). В Claude desktop Blender MCP пока не подключён — до подключения моделирование через Blender недоступно.
- GameDev Hub (`http://localhost:3888`): необязателен; MCP `gamedevHub` в этой среде не подключён — работать по локальному контексту.
- Shell на ПК пользователя (git, python, powershell) пока недоступен: git-операции и пересборку карты выполняет пользователь либо они делаются после включения device_bash.
