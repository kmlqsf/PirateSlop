# Материалы тестовых песчаных островов

Оригинальные карты Poly Haven, лицензия [CC0](https://polyhaven.com/license).
Загружены 2026-10-11 через официальный API; контрольные суммы MD5 сверены с API.

| Каталог | Источник | Размер бесшовного участка |
| --- | --- | --- |
| coast_sand_01 | [Coast Sand 01](https://polyhaven.com/a/coast_sand_01) | 15 м |
| leafy_grass | [Leafy Grass](https://polyhaven.com/a/leafy_grass) | 2 м |
| brown_mud_03 | [Brown Mud 03](https://polyhaven.com/a/brown_mud_03) | 1.3 м |
| coast_sand_rocks_02 | [Coast Sand Rocks 02](https://polyhaven.com/a/coast_sand_rocks_02) | 15 м |

В каждом каталоге: Colour.jpg — Diffuse 2K; Normal.png — Normal GL 2K;
Roughness.jpg — Rough 2K. Исходные изображения сохранены без изменений.
Mask.png создаётся настройщиком Unity: R=0 (metallic), G=1 (AO), B=0.5,
A=1−roughness. Она предназначена для TerrainLayer URP.

Скальный пояс использует существующий материал CoastalRock с мировой проекцией.
Пальмы, кусты и папоротники взяты из существующих StarterIsland-префабов.
