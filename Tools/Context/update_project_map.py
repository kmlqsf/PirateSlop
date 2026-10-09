from pathlib import Path
from collections import defaultdict
from datetime import date
import json
import os
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Docs/ProjectMap'
SKIP = {'.git', 'Library', 'Temp', 'Logs', 'obj', 'bin', 'Builds', 'UserSettings', '.vs', '.idea', '__pycache__', 'node_modules'}
TYPES = {'.cs': 'Исходник C#', '.prefab': 'Префаб Unity', '.unity': 'Сцена Unity', '.mat': 'Материал Unity', '.asset': 'Настройки или данные Unity', '.shader': 'Шейдер', '.shadergraph': 'Граф шейдера', '.hlsl': 'Код шейдера', '.compute': 'Вычислительный шейдер', '.anim': 'Клип анимации', '.controller': 'Контроллер анимации', '.overridecontroller': 'Переопределения анимаций', '.fbx': 'Модель / анимации FBX', '.blend': 'Редактируемая сцена Blender', '.glb': 'Модель GLB', '.gltf': 'Модель glTF', '.obj': 'Модель OBJ', '.png': 'Изображение / текстура', '.jpg': 'Изображение / текстура', '.jpeg': 'Изображение / текстура', '.tga': 'Текстура', '.exr': 'HDR-текстура', '.hdr': 'HDR-текстура', '.wav': 'Аудио', '.ogg': 'Аудио', '.mp3': 'Аудио', '.md': 'Документация', '.txt': 'Текстовые данные', '.json': 'Конфигурация / данные JSON', '.ps1': 'Инструмент PowerShell', '.py': 'Инструмент Python', '.dll': 'Скомпилированная библиотека', '.asmdef': 'Описание сборки Unity', '.asmref': 'Ссылка на сборку Unity', '.inputactions': 'Действия Input System', '.vfx': 'Граф визуального эффекта', '.uss': 'Стили UI', '.uxml': 'Разметка UI', '.ttf': 'Шрифт', '.otf': 'Шрифт', '.zip': 'Архив'}
SECTIONS = {'Scripts': 'Игровой код и редакторские инструменты', 'Game': 'Игровые подсистемы и эффекты', 'Prefabs': 'Готовые игровые объекты', 'Scenes': 'Сохранённые сцены', 'Models': 'Модели и связанные ресурсы', 'Materials': 'Материалы', 'Shaders': 'Шейдеры', 'Animations': 'Анимации', 'Audio': 'Звуковые ресурсы и лицензии', 'Resources': 'Ресурсы, доступные для загрузки по имени', 'Settings': 'Настройки игровых систем и рендеринга', 'UI': 'Ресурсы интерфейса', 'Editor': 'Редакторские ресурсы', 'Tests': 'Исходники проверок', 'Plugins': 'Плагины', 'ThirdParty': 'Сторонние ресурсы', '_Recovery': 'Сохранённые восстановленные данные', 'Screenshots': 'Сохранённые изображения', 'Branding': 'Оформление проекта'}

def save(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content.rstrip() + '\n', encoding='utf-8')

def link(path, base=ROOT):
    relative = os.path.relpath(ROOT / path, base).replace('\\', '/')
    return f'[{path}](<{relative}>)'

def group(path):
    parts = Path(path).parts
    if parts[0] == 'Assets' and len(parts) > 2:
        return 'Assets/' + parts[1]
    return parts[0] if len(parts) > 1 else 'Root'

def describe(path):
    p = ROOT / path
    result = TYPES.get(p.suffix.lower(), 'Файл ' + (p.suffix or 'без расширения'))
    if p.suffix == '.cs':
        source = p.read_text(encoding='utf-8-sig', errors='replace')
        symbols = re.findall(r'\b(?:class|struct|interface|enum)\s+(\w+)', source)
        if symbols:
            result += ': ' + ', '.join(dict.fromkeys(symbols))
    if path in membership:
        result += '; ' + ', '.join(membership[path])
    return result.replace('|', '\\|')

catalog = json.loads((ROOT / 'Tools/Context/topics.json').read_text(encoding='utf-8-sig'))
tracked_captures = set(subprocess.check_output(['git', 'ls-files', '-z', '--', 'Captures'], cwd=ROOT).decode('utf-8').split('\0'))
membership = defaultdict(list)
for topic in catalog['topics']:
    for path in topic['files'] + topic['docs']:
        membership[path].append(topic['title'])

files = []
meta_count = 0
for folder, dirs, names in os.walk(ROOT):
    dirs[:] = sorted(d for d in dirs if d not in SKIP and not (Path(folder) / d).is_symlink() and Path(folder) / d != OUT)
    for name in sorted(names):
        p = Path(folder) / name
        relative = p.relative_to(ROOT).as_posix()
        if relative.startswith('Captures/') and relative not in tracked_captures:
            continue
        if relative.startswith('Art/Blender/World/CoastalEnvironment/CoastalTripo_Pre'):
            continue
        if p.suffix == '.meta':
            meta_count += 1
            continue
        if relative == 'PROJECT_MAP.md':
            continue
        files.append(relative)

groups = defaultdict(list)
for path in sorted(files):
    groups[group(path)].append(path)

lines = ['# PirateSlop — карта проекта', '', f'Снимок файлов: {date.today().isoformat()}. Корень: `C:\\Users\\K\\Project`.', '',
    '## Как пользоваться', '',
    'Сначала прочитай `AGENTS.md`, `lessons.md` и эту карту. Выбери механику ниже; если нужного файла нет среди точек входа, открой соответствующий каталог из таблицы. Ищи имя внутри этих Markdown-файлов, затем читай исходник по указанному пути. Не запускай обзор папок или поиск файлов по всему проекту при каждом новом чате.', '',
    'Карта описывает сохранённые файлы, а не живую сцену и не результаты игровых проверок. Назначения механик взяты из поддерживаемого `Tools/Context/topics.json`; в полном каталоге указаны тип файла, объявленные C#-типы и известные тематические связи. Для остальных файлов семантика не угадывается по имени. Подключение компонента и актуальную реализацию проверяй только для затронутой задачи.', '',
    'Все пути относительно корня проекта. Полный каталог разбит на приложения, чтобы не загружать тысячи строк в каждый чат.', '',
    '## Полный каталог', '',
    f'Учтено {len(files)} файлов без `.meta`. Ещё {meta_count} файлов `.meta` сопровождают ассеты/папки: их путь — путь ассета или папки плюс `.meta`; сохраняй их GUID. Сама карта и её автоматически созданные приложения не входят в подсчёт.', '',
    '| Раздел | Назначение | Файлов |', '| --- | --- | ---: |']

for key, paths in sorted(groups.items()):
    filename = key.replace('/', '-') + '.md'
    description = SECTIONS.get(key.split('/')[-1], {'Root': 'Корневые инструкции, планы и служебные файлы', 'Art': 'Исходники арта и Blender', 'Docs': 'Документы и сохранённые отчёты', 'Tools': 'Инструменты разработки и загрузчик контекста', 'Packages': 'Манифест, lock-файл и встроенные пакеты', 'ProjectSettings': 'Настройки Unity', 'ThirdParty': 'Сторонние зависимости'}.get(key, 'Ресурсы раздела; точный состав — в каталоге'))
    lines.append(f'| [{key}](<Docs/ProjectMap/{filename}>) | {description} | {len(paths)} |')
    page = [f'# {key}', '', '[Главная карта](../../PROJECT_MAP.md)', '', description + '.', '', 'Автоматический каталог. Описания обозначают тип файла и известную тему, а не подтверждение использования в игре.', '']
    by_folder = defaultdict(list)
    for path in paths:
        by_folder[str(Path(path).parent).replace('\\', '/')].append(path)
    for parent, items in sorted(by_folder.items()):
        page += [f'## {parent}', '', '| Файл | Краткое описание |', '| --- | --- |']
        page += [f'| {link(path, OUT)} | {describe(path)} |' for path in items]
        page.append('')
    save(OUT / filename, '\n'.join(page))

lines += ['', 'Не индексируются генерируемые сборки, кэши и локальное состояние: ' + ', '.join(f'`{p}/`' for p in sorted(SKIP)) + '. В Captures индексируются только отслеживаемые Git файлы; промежуточные резервные CoastalTripo_Pre*.blend не индексируются. Зависимости из Unity PackageCache представлены манифестом/lock-файлом; встроенные пакеты из `Packages/` перечислены полностью. Скрытые конфиги вне исключённых папок включены только как пути, их содержимое не копируется.', '', '## Механики и точки входа', '']
missing = []
for topic in catalog['topics']:
    lines += [f"### {topic['title']} (`{topic['id']}`)", '', 'Ключевые слова: ' + ', '.join(topic['aliases']) + '.', '']
    lines += [note for note in topic['notes']]
    lines.append('')
    for path in topic['files'] + topic['docs']:
        if (ROOT / path).is_file():
            lines.append(f'- {link(path)} — {describe(path).split("; ")[0]}.')
        else:
            missing.append(path)
            lines.append(f'- `{path}` — отсутствует в текущем снимке; не использовать как готовый путь.')
    lines.append('')

lines += ['## Источники актуальных настроек', '']
lines += [f'- {link(path)}.' for path in catalog['liveSources']]
lines += ['', '## Поддержание карты', '',
    '- При добавлении, удалении, переименовании файлов или изменении назначения системы обновляй карту в той же задаче. Темы и пояснения редактируй в `Tools/Context/topics.json`, затем пересобери каталог.',
    '- Обновление: `python Tools/Context/update_project_map.py` из корня проекта (либо абсолютный путь к скрипту). Нужен Python 3. Скрипт перечисляет файлы и читает C#-объявления; Unity/Blender не запускает. Это обслуживание карты, а не обязательное действие каждого нового чата.',
    '- Не редактируй автоматически созданные приложения вручную: изменения будут заменены при обновлении. Новые подробные пояснения добавляй в тематический источник.',
    '- Если путь устарел, проверь конкретный путь и исправь запись. При отсутствии записи и невозможности восстановить путь из ссылок запроси разрешение на ограниченный поиск; не начинай с общего сканирования.',
    '- `Tools/Context/context.ps1 <тема>` остаётся необязательным помощником для живых значений сохранённых конфигов. Он не заменяет эту карту как первый источник путей.', '']
save(ROOT / 'PROJECT_MAP.md', '\n'.join(lines))
print(f'Indexed {len(files)} files in {len(groups)} catalogs; {meta_count} companion meta files; missing topic paths: {len(missing)}')
for path in missing:
    print('MISSING: ' + path)
