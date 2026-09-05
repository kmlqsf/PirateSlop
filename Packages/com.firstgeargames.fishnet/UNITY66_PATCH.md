# Unity 6.6 compatibility patch
Upstream FishNet 4.7.2 (release label 4.7.2R), commit de19b5d66459f60400ffd0edc443c4da173a01e7. Embedded verbatim with these changes:
- Runtime/Managing/Scened/UnitySceneToken.cs: collision-free process-local integer tokens for opaque Scene handles; no truncation of 64-bit handles. Used by SceneManager, SceneLookupData, UnloadedScene and scene comparers.
- NetworkObserver.Deinitialize: destroy cloned conditions directly. Initialize always instantiates its own condition copies; Unity 6.6 removed GetInstanceID and the sign test is no longer valid.
Do not replace with a floating upstream version. Revalidate scene loading, observation and cleanup when upgrading. License and upstream source are preserved.
