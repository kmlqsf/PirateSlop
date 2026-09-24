# Инструкции ИИ для PirateSlop

## Режим работы по умолчанию

Эти правила уточняют и заменяют противоречащие им требования к объёму работы и проверкам в `project.md`, `unity.md` и `blender.md`. Инструкции более высокого приоритета сохраняют силу.

- Экономить токены: выполнять только основную логику и минимальные изменения по запросу.
- По умолчанию не запускать билды, тесты, игровые/визуальные проверки и дополнительные диагностические прогоны. Пользователь проверяет результат сам и сообщает об ошибках. Запускать такие проверки только по явному запросу либо если этого требует инструкция более высокого приоритета.
- Не расширять задачу дополнительной полировкой, отчётами и несвязанными исправлениями. Читать лишь необходимый для изменения контекст.
- В конце кратко сообщать, что изменено и что пользователю проверить самому. Не заявлять об успешных проверках, если они не выполнялись.
- Отвечать кратко, без длинных планов и повторяющихся обновлений.

## Контекст проекта

Для короткого отчёта по конкретному объекту или префабу Unity используй `PirateSlop.Editor.FocusedInspector` через рабочий Unity MCP. Инструкция и примеры: `Tools/Context/inspector.md`. Начинай с одного объекта и фильтра компонента; отчёт не подтверждает игровой приёмки.

Перед выполнением задачи запусти `./Tools/Context/context.ps1 <тема>` из корня репозитория; без темы выводится обзор, `-List` показывает темы и русские синонимы. Скрипт также работает по абсолютному пути из любой папки. Используй одну или несколько тем, непосредственно относящихся к задаче.
Загрузчик заменяет обязательное полное чтение `project.md` и `unity.md`: история сохранена, но не нужна для каждой задачи. Читай указанные в отчёте исходники и разделы документов только по необходимости. Для моделирования прочитай `blender.md`; для передачи модели в Unity также применимые правила импорта из `unity.md`.
Если загрузчик недоступен или тема не покрыта, прочитай необходимые разделы `project.md` и профильного документа. Не считай карту путей или исторические записи доказательством состояния живой сцены. При переименовании файла обнови затронутую запись в `Tools/Context/topics.json`.

Документация и ответы пользователю — по-русски; код, имена файлов и объектов — по-английски.
Выполняй задачу до сохранённого результата, пригодного для ручной проверки, в пределах доступных инструментов. Не заявляй об успешной проверке без фактического выполнения.
Сохраняй чужие изменения и Unity GUID. Перед правками проверяй текущую реализацию и Git.
Коммиты и push разрешены только после явного подтверждения пользователя в чате для конкретных изменений; соблюдай раздел Git в `project.md`.

Эти четыре файла остаются рядом в корне репозитория. Для ИИ-клиента, который не читает AGENTS.md, пользователь должен явно указать прочитать этот файл либо подключить его через поддерживаемый механизм инструкций. Команды загрузчика описаны в `Tools/Context/README.md`.

Описание механизма Codex: https://developers.openai.com/codex/guides/agents-md.

## Создание 3D-моделей (Blender)

При запросе на создание новой 3D-модели в Blender обязательно создавай и используй двух субагентов:
1. **Арт-директор (Концепт)**: продумывает дизайн, генерирует 2-3 варианта 2D-референсов (с помощью инструмента generate_image) и составляет техническое задание для процедурной генерации (какие примитивы и модификаторы использовать). При генерации изображений указывай: отображать только саму деталь без лишних объектов, стиль ближе к low-poly (в стилистике корабля), на простом сером фоне, как будто это готовая модель во вьюпорте Blender.
2. **Моделлер (Blender/Python)**: пишет Python-скрипт для создания модели в Blender на основе ТЗ и самостоятельно исполняет его в открытом Blender через подключенный MCP сервер.

**Важно:** Перед написанием кода и фактическим созданием модели обязательно покажи пользователю сгенерированные 2D-референсы и дождись явного утверждения одного из них.

## Взаимодействие с Git (Git-агент)

При любом взаимодействии с Git (получение изменений, коммиты, отправка, слияние, разрешение конфликтов) обязательно создавай/вызывай субагента `git_manager`.
- Субагент отвечает за работу с репозиторием `https://github.com/kmlqsf/PirateSlop`.
- Выполняет получение файлов, подготовку коммитов, слияния и отправку на GitHub.
- Коммиты и push разрешены только после явного подтверждения пользователя в чате для конкретных изменений (соблюдать правила из `project.md`). Никаких force push и разрушительных сбросов.
- **Разрешение конфликтов**: если возникает конфликт слияния/rebase, агент обязан описать простым языком, что конфликтующий файл делает для игры и в чём суть конфликта, после чего остановиться и ждать явного решения пользователя.

## User preferences — 2026-09-08

These preferences supersede conflicting earlier style and context-reading requirements in this project's instructions. Higher-priority instructions still apply.

- Always reply in Russian. Start with the task content; omit greetings, apologies, and introductory filler.
- For code answers, provide only the requested code unless the user explicitly asks for an explanation. Do not explain implementation choices or Unity functions unsolicited.
- When only part of a file changes, use a diff or the changed method/block. Never output the entire existing script or class for a partial change. In displayed snippets only, omitted code may be represented by `// ... existing code ...`; never replace real file contents with placeholders.
- Do not generate code comments or XML documentation, including summary tags. The omission marker above is only for displayed excerpts.
- Read only files directly needed for the current change. Do not scan folders or read adjacent classes merely for context. Read a dependency only when it directly affects the change; avoid repeating already-read instructions.
- If the exact file path is unknown, ask the user for it instead of searching across the project.
- Continue making the actual requested edits; a code-only response preference does not replace performing the task. Keep any required progress or completion messages minimal.
