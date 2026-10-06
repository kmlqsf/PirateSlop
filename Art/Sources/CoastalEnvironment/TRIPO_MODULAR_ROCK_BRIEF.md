# Универсальный набор скальных модулей — концепт C

Назначение: исходные камни для сборки арок, пещер и лагун с проходами, скальных стен, отдельных высоких скал, небольших островов и низких рифов. Сборка из скачанных сканов отвергнута по замечанию пользователя. Арка из предоставленных Tripo GLB одобрена; пользователь разрешил изготовить остальные формы в том же стиле и заменить все 17 моделей тестовой галереи.

Подготовлены [референсы всех 15 моделей и таблица полигонов](../../../../Blender/Скалы/README.md): 15 папок по четыре PNG — спереди.png, сзаде.png, справа.png и слева.png. Во всех папках TRIPO.txt с индивидуальным Polygon Count для Triangle topology. Бюджеты: массивы 4000, монолиты 4500, широкие плиты 3000, клинья 2500, вытянутые глыбы 3000, валун 1800, обломки 600 и 800. Все комплекты просмотрены как практические 2D-референсы.

Пользователь предоставил все 15 моделей Tripo: в каждой папке расположен GLB с названием папки. Исходники содержат встроенные карты Base Color, Normal и Metallic/Roughness; общий фактический объём — 44 860 треугольников. Итоговый набор собран из целых модулей с исходными UV/PBR в CoastalTripoCollection.blend. Состав, экспорт и ограничения проверки описаны в [README_TRIPO_COLLECTION.md](../../Blender/World/CoastalEnvironment/README_TRIPO_COLLECTION.md). Игровая производительность оценивается отдельно от приёмки формы.

## Комплект

Пропорции относительные. Игровой размер, расположение и сохранение проходов задаются при сборке в Blender. Все варианты сохраняют одну породу, палитру и характер поверхности.

| Код | Модуль | Вариантов | Форма | Применение |
|---|---|---:|---|---|
| RockMass | Компактный скальный массив | 3 | Примерно равные ширина, высота и глубина; неправильный угловатый контур | Основная масса островов, стены, опоры, пещеры |
| RockMonolith | Вертикальный монолит | 3 | Высота около трёх ширин, глубина близка к ширине; один вариант с наклонной заострённой вершиной | Высокие скалы, столбы, вертикальные акценты, шпили |
| RockTerrace | Широкая толстая плита | 2 | Ширина : глубина : толщина ≈ 2.5 : 2 : 0.8; неровный низ, один скошенный край | Перекрытия, своды, террасы, низкие рифы |
| RockWedge | Клиновидная глыба | 2 | Широкое основание, один край выше, естественный диагональный скол | Плечи сводов, повороты стен, уступы, переходы по высоте |
| RockElongated | Вытянутая толстая глыба | 2 | Длина : ширина : толщина ≈ 3 : 1 : 0.8; асимметричный цельный объём | Длинные выступы, перемычки, выступающие вертикальные блоки |
| RockBoulder | Крупный опорный валун | 1 | Приземистый широкий угловатый камень с наклонными гранями | Основания, наружные выступы, одиночные небольшие скалы |
| RockFragment | Малый обломок | 2 | Компактные угловатые фрагменты с разными силуэтами | Группы у основания, россыпь, прикрытие небольших стыков |

Полный комплект — 15 уникальных моделей. Сначала создать только по одной модели RockMass, RockMonolith и RockTerrace: проверить общий камень, замкнутость, качество боков и нижней стороны, поведение нескольких повторённых модулей. Остальные варианты создавать после проверки этой основы. Варианты должны отличаться силуэтом, наклоном основных разломов и крупными сколами; одна перекрашенная копия не является новой формой.

## Общие требования

- Одна отдельная цельная глыба в каждом файле. Полноценные текстурированные перед, зад, бока, верх и низ; замкнутая геометрия без обрезанных краёв скана и внутренних пустот.
- Камень как в C: тёплый светлый серо-бежевый, большие неровные поверхности, глубокие естественные вертикальные разломы, редкие поперечные трещины, сколы и широкие минеральные цветовые пятна.
- Силуэт и крупные сколы создаются геометрией. Пористость, мелкие трещины и зерно — фактурой и normal map. Поверхность шероховатая, с умеренно резкими краями; не округлять каждый выступ одинаковой фаской.
- Каждый блок хорошо выглядит при повороте. Земля, вода, растения, зелёная верхушка и мокрая полоса не входят в модуль. Небольшой локальный мох допустим позже как отдельная вариация материала. Основной набор имеет нейтральный сухой камень.
- Сохранять исходный текстурированный GLB/PBR. Предпочтительно Base Color, Normal, Roughness и исходная UV-развёртка. Карта Metallic для камня нулевая. Для начала достаточно текстур 2K.
- Сохранить исходную геометрию и отдельную упрощённую копию, если она создаётся в Tripo. Целевые LOD и общие материалы готовятся после проверки формы. Число исходных полигонов само по себе не подтверждает хороший FPS.
- При генерации по изображениям каждый вход — отдельный блок на нейтральном фоне; общий C задаёт стиль всего комплекта. Для нескольких ракурсов показывать одну и ту же глыбу.

## Готовые промпты

Промпт создаёт один модуль. Для дополнительных вариантов сохранять материал и пропорции, меняя крупные сколы, силуэт и положение разломов. Названия используются при сохранении файлов.

### RockMass_A — компактный скальный массив

Single isolated solid rock module for a stylized realism coastal cliff kit. A compact massive irregular rock with roughly equal width, height and depth. Broad uneven angular faces, deep mostly vertical natural fractures, a few transverse cracks, large chipped corners and an asymmetric broken top. Warm pale grey beige coastal limestone, broad mineral color variation, rough grain and fine surface pores. Match a rugged vertical fractured block cliff, with substantial volume and believable sharp natural breaks. Fully closed three dimensional mesh, complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no masonry, no hollow shell, no smooth clay surface.

### RockMonolith_A — вертикальный монолит

Single isolated solid rock module for the same stylized realism coastal cliff kit. A tall massive irregular monolith, height about three times its width, depth close to its width. Deep long vertical fractures divide broad rough stone faces; a few transverse breaks and large chips. Asymmetric fractured crown and substantial natural thickness on every side. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no thin sheet, no artificial pillar, no smooth clay surface.

### RockTerrace_A — широкая толстая плита

Single isolated solid rock module for the same stylized realism coastal cliff kit. A wide substantial irregular rock slab, width to depth to thickness about 2.5 to 2 to 0.8. Thick natural volume, stepped broken top, uneven fully formed underside, heavy chipped ends, one naturally sloping edge. Mostly vertical fracture lines on the side faces and a few transverse cracks. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no thin panel, no concrete beam, no smooth clay surface.

### RockWedge_A — клиновидная глыба

Single isolated solid rock module for the same stylized realism coastal cliff kit. A thick massive irregular wedge shaped boulder, broad base, one tall end and one lower end, a rough diagonal natural fracture joining them. Broad angular faces, a broken irregular outline, deep vertical fissures and large chipped corners. Substantial rock volume, not a perfect triangular prism. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no masonry, no smooth clay surface.

### RockElongated_A — вытянутая толстая глыба

Single isolated solid rock module for the same stylized realism coastal cliff kit. A long thick irregular angular rock fragment, length to width to thickness about 3 to 1 to 0.8. Uneven asymmetric silhouette, deep long fractures, several large chips, one jagged end and a more massive opposite end. Broad rough natural faces and fully formed ends with substantial volume. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no thin sheet, no cut scan edges, no smooth clay surface.

### RockBoulder_A — крупный опорный валун

Single isolated solid rock module for the same stylized realism coastal cliff kit. A broad low massive angular boulder with a heavy irregular base, sloping outer faces, a broken uneven crown, deep fractures and a few large chipped corners. Natural asymmetric rugged volume, broader than tall, suitable for the foot of a coastal cliff or a small rocky outcrop. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no earth base, no smooth rounded clay surface.

### RockFragment_A — малый обломок

Single isolated solid rock module for the same stylized realism coastal cliff kit. A compact small irregular angular rock fragment with broad broken faces, a few sharp natural chips, one visible fracture and an asymmetric fully formed outline. A believable broken piece of a larger vertical fractured coastal cliff. Warm pale grey beige coastal limestone with broad mineral color variation, rough grain and fine surface pores. Fully closed three dimensional mesh with complete textured back, sides, top and bottom. Neutral dry stone, nonmetallic PBR material. One stone only, without vegetation, water or ground, no loose pile, no thin sheet, no smooth clay surface.

## Приёмка исходников

Осмотреть каждый блок спереди, сзади, сбоку и снизу. Большие сколы и общий силуэт должны читаться и без текстур; текстурированный камень должен сохранять детализацию при среднем ракурсе. Сначала из трёх проб собрать простые композиции: отдельный массив, высокий утёс, свод, пещерный вход и террасу. Это проверка пригодности модулей, не разрешение менять остальные игровые модели.

Tripo поддерживает текстурирование/PBR, ретопологию и ограничение количества полигонов: [официальное руководство](https://www.tripo3d.ai/blog/tripo-studio-tutorial-english). Вариант PBR GLB присутствует в [официальной документации Image to Model](https://developers.tripo3d.ai/en/docs/generation-image-to-model).
