# Инструкции ИИ для PirateSlop

## Режим работы по умолчанию

Эти правила уточняют и заменяют противоречащие им требования к объёму работы и проверкам в `project.md`, `unity.md` и `blender.md`. Инструкции более высокого приоритета сохраняют силу.

- Экономить токены: выполнять только основную логику и минимальные изменения по запросу.
- По умолчанию не запускать билды, тесты, игровые/визуальные проверки и дополнительные диагностические прогоны. Пользователь проверяет результат сам и сообщает об ошибках. Запускать такие проверки только по явному запросу либо если этого требует инструкция более высокого приоритета.
- Не расширять задачу дополнительной полировкой, отчётами и несвязанными исправлениями. Читать лишь необходимый для изменения контекст.
- В конце кратко сообщать, что изменено и что пользователю проверить самому. Не заявлять об успешных проверках, если они не выполнялись.
- Отвечать кратко, без длинных планов и повторяющихся обновлений.

## Контекст проекта

Перед выполнением задачи прочитай `project.md` в корне репозитория.
Для Unity, C#, сцен, префабов и импорта ассетов дополнительно прочитай `unity.md`.
Для моделирования, Blender и FBX дополнительно прочитай `blender.md`; при передаче модели в игру прочитай оба файла.

Документация — по-русски; ответы пользователю, код, имена файлов и объектов — по-английски.
Выполняй задачу до сохранённого результата, пригодного для ручной проверки, в пределах доступных инструментов. Не заявляй об успешной проверке без фактического выполнения.
Сохраняй чужие изменения и Unity GUID. Перед правками проверяй текущую реализацию и Git.
Коммиты и push разрешены только после явного подтверждения пользователя в чате для конкретных изменений; соблюдай раздел Git в `project.md`.

Эти четыре файла должны находиться рядом в корне репозитория. Для ИИ-клиента, который не читает AGENTS.md, пользователь должен явно указать прочитать их либо подключить этот файл через поддерживаемый клиентом механизм инструкций.

Описание механизма Codex: https://developers.openai.com/codex/guides/agents-md.

## User preferences — 2026-09-08

These preferences supersede conflicting earlier style and context-reading requirements in this project's instructions. Higher-priority instructions still apply.

- Reply in English even when the user writes in Russian. Start with the task content; omit greetings, apologies, and introductory filler.
- For code answers, provide only the requested code unless the user explicitly asks for an explanation. Do not explain implementation choices or Unity functions unsolicited.
- When only part of a file changes, use a diff or the changed method/block. Never output the entire existing script or class for a partial change. In displayed snippets only, omitted code may be represented by `// ... existing code ...`; never replace real file contents with placeholders.
- Do not generate code comments or XML documentation, including summary tags. The omission marker above is only for displayed excerpts.
- Read only files directly needed for the current change. Do not scan folders or read adjacent classes merely for context. Read a dependency only when it directly affects the change; avoid repeating already-read instructions.
- If the exact file path is unknown, ask the user for it instead of searching across the project.
- Continue making the actual requested edits; a code-only response preference does not replace performing the task. Keep any required progress or completion messages minimal.
