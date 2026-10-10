# Удочка и катушка

`FishingRodReplacement.blend` — нормализованная удочка из модели пользователя; `FishingReelSources.blend` — исходные крепление/ось и шпуля с упакованными PBR-картами. `FishingRodAssembly.blend` — готовая сборка, намотка, рукоятка и маркеры существующих направляющих колец.

`AssembleFishingReel.py` выполняется внутри подключённого Blender MCP. Он подгружает исходные сцены, создаёт отдельную сборку, сохраняет BLEND и экспортирует `Assets/Models/Fishing/Replacement/FishingRodReplacement.fbx`. Предыдущая активная сцена сохраняется. AssemblyLinePreview предназначен для рендера и не экспортируется: в игре леска строится через FishingRodReel.

Unity: PirateSlop → Replace Fishing Rod Model. FishingRodReplacementSetup сохраняет GUID FishingRod/DroppedRod, назначает материалы, компоненты изгиба/катушки, леску игрока и обезьянки, обновляет коллайдер pickup. Импорт Read/Write нужен для деформации отдельных runtime-копий мешей.

Шпуля, намотка и рукоятка — дети ReelSpoolPivot; крепление не вращается. Леска идёт через шесть LineGuide, выход совпадает с TipLocalPoint(0,.133,1.69). Серверный reelActive останавливает намотку при отпускании ЛКМ и управляет отображением у наблюдателей. ProtocolVersion136. Компиляция/ссылки и рендер сборки проверяются отдельно от игрового/сетевого сценария.
