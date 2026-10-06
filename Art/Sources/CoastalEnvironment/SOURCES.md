# Источники окружения C

Перечисленные ниже сторонние материалы и модели имеют CC0: допускаются коммерческое использование и изменения, указание автора не требуется. Пользовательские Tripo GLB описаны отдельно; лицензия CC0 этой таблицы на них не распространяется.

| Ассет | Что используется | Лицензия и источник |
|---|---|---|
| Poly Haven Rock Wall 02 | Бесшовные цвет и normal map природной береговой скалы для Unity и Blender | [Ассет](https://polyhaven.com/a/rock_wall_02), [CC0](https://polyhaven.com/license) |
| Poly Haven Fern 02 | Модель папоротника, цвет, normal и alpha map | [Ассет](https://polyhaven.com/a/fern_02), [CC0](https://polyhaven.com/license) |
| Yughues Free palm treeZ v3 | Прямая и изогнутая пальмы, общий цвет и normal map | [Ассет и CC0](https://opengameart.org/content/free-palm-treez-v3), readme.txt в yughues_palms |
| Poly Haven Rock Face 02 | Готовая скальная модель FBX и исходные UV-карты diffuse, normal, roughness, AO для модульной сборки арки | [Ассет](https://polyhaven.com/a/rock_face_02), [CC0](https://polyhaven.com/license) |
| Poly Haven Coastal Cliff 02 | Готовый скан утёса с UV-картами и уровнями детализации; открытый задник скрывать другими готовыми камнями | [Ассет](https://polyhaven.com/a/coastal_cliff_02), [CC0](https://polyhaven.com/license) |
| Poly Haven Rock Moss Set 01 | Шесть готовых камней с общим UV-атласом для основания и перекрытия стыков | [Ассет](https://polyhaven.com/a/rock_moss_set_01), [CC0](https://polyhaven.com/license) |

Дополнительные исходники Poly Haven Rock Surface, Grass Bermuda 01 и Island Tree 02 подготовлены как варианты для дальнейшей работы. UV-атласы сканов используются только с их исходной UV-развёрткой. Метаданные и контрольные суммы этих скачанных CC0-ассетов сохранены в files.json и sources.json.

Актуальные скалы собраны по концепту C из готовых Tripo-модулей в Art/Blender/World/CoastalEnvironment/CoastalTripoCollection.blend. Экспорты находятся в Assets/Models/World/CoastalEnvironment. Изменения видимого рельефа сохраняют исходную физическую оболочку; новые визуальные объекты не содержат коллайдеров.

История согласования: пользователь отверг процедурный slab-shell/voxel-прототип и последующую сборку из сканов. SeaArch_Huge_A_CReview.blend и SeaArch_Huge_A_NativeModules.blend не являются принятым результатом. Затем пользователь одобрил арку из собственных Tripo GLB и поручил заменить остальные модели в том же стиле.

Пользователь предоставил 15 готовых Tripo GLB в Blender/Скалы, по одному в каждой папке комплекта; имена GLB совпадают с именами папок. Во всех GLB встроены цвет, normal и Metallic/Roughness, фактический суммарный объём исходников — 44 860 треугольников. Требования, 7 типов и готовые промпты: [TRIPO_MODULAR_ROCK_BRIEF.md](TRIPO_MODULAR_ROCK_BRIEF.md). Принятая арка сохранена отдельно в SeaArch_Huge_A_Tripo.blend, сцена TripoArchScene: [README_TRIPO_ARCH.md](../../Blender/World/CoastalEnvironment/README_TRIPO_ARCH.md). Коллекция содержит 12 скальных форм и 5 растений для всех 17 существующих записей галереи: [README_TRIPO_COLLECTION.md](../../Blender/World/CoastalEnvironment/README_TRIPO_COLLECTION.md). Камень использует собственные карты GLB и shader CoastalTripoRock; карта Rock Wall 02 не заменяет его UV-атласы. Пальмы и папоротники используют перечисленные CC0-источники.

Прежние COL FBX, GUID и коллайдеры сохранены; визуальные LOD FBX заменены по существующим путям. Снимок исходной геометрии: Art/Blender/World/CoastalEnvironment/physical-baseline.json. Подключение выполняется через PirateSlop > Art > Apply Coastal Environment C. Тестовая галерея содержит все 17 моделей в одном ряду с зазором 30 м по видимым границам; PirateSlop > Art > Arrange Coastal Test Gallery повторяет раскладку. Повторное Prepare Environment Test сохраняет готовую галерею и игровые радиусы, обновляя версии каталога.

Береговая пена имеет отдельную пространственную сетку и ограниченный бюджет: до 4 запросов высоты волн и 256 штампов за кадр, до 24 запросов за 0.2 с; область 192 м, текстура 256². Береговые контуры строятся по объединённому сечению камня, исключая внутренние пересечения мешей и сохраняя воду в проходах. Материал камня не вычисляет процедурный шум в каждом пикселе; проходы тени и глубины используют единый буфер материала.

Игровая приёмка: обычная и тестовая карты, проход под аркой и в лагуне, мокрая полоса, пена при волнах, переключение LOD и нагрузка рядом с несколькими скалами. Билд и Play Mode при исправлении не запускались. Адресный CPU-замер 13 695 запросов BoatAttackOcean.HeightOffset занял 38.07 мс. Изолированная проверка новой пены на 13 728 точках: 64 обновления, среднее 0.573 мс, максимум после первого 1.856 мс, нарушения лимитов отсутствуют. Эти замеры не являются замером игрового FPS.
