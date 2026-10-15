var session = PirateSlop.Networking.SessionController.Instance;
if (!UnityEditor.EditorApplication.isPlaying || session == null || session.SessionBusy)
    throw new System.InvalidOperationException("Start NetworkMenu Play Mode with no active session.");
System.Collections.IEnumerator Run()
{
    PirateSlop.Performance.PerformanceBenchmarkController controller = null;
    PirateSlop.Performance.PerformanceCapture capture = null;
    UnityEngine.GameObject fixture = null;
    bool complete = false;
    string[] segments = { "Idle", "First character", "Second character", "Repeated death" };
    try
    {
        session.BeginPerformanceBenchmark();
        double deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 120;
        while (!session.BenchmarkReady || UnityEngine.Camera.main == null || !session.BenchmarkEventsReady)
        {
            if (UnityEngine.Time.realtimeSinceStartupAsDouble > deadline)
                throw new System.TimeoutException("Character probe world preparation timed out.");
            yield return null;
        }
        yield return null;
        controller = session.GetComponent<PirateSlop.Performance.PerformanceBenchmarkController>();
        controller.enabled = false;
        capture = session.GetComponent<PirateSlop.Performance.PerformanceCapture>();
        capture.Finish("CANCELLED", "Setup only; followed by a separate Editor character impact probe.", segments);
        while (PirateSlop.Performance.PerformanceCapture.Saving) yield return null;
        PirateSlop.Networking.NetworkPlayer shooter = null, first = null, second = null;
        foreach (var player in PirateSlop.Networking.NetworkPlayer.Active)
        {
            if (player == null || !player.IsServerInitialized) continue;
            if (player.TeamId.Value == 1 && shooter == null) shooter = player;
            if (player.TeamId.Value == 2)
            {
                if (first == null) first = player;
                else if (second == null) second = player;
            }
        }
        if (shooter == null || first == null || second == null || first.Ship == null)
            throw new System.InvalidOperationException("Expected authoritative characters from two teams.");
        if (session.LoadTestMoving) session.ToggleLoadTestMotion();
        var platform = first.Ship.transform;
        var view = UnityEngine.Camera.main;
        var weapon = shooter.GetComponent<PirateSlop.Networking.NetworkWeapon>();
        var inventory = shooter.GetComponent<PirateSlop.PlayerInventory>();
        bool equipped = false;
        for (int slot = 0; slot < PirateSlop.PlayerInventory.SlotCount; slot++)
            if (inventory.PistolAt(slot)) { equipped = weapon.SelectServerSlot(slot); break; }
        if (!equipped) throw new System.InvalidOperationException("Character probe shooter has no selectable pistol.");
        var health = first.GetComponent<PirateSlop.CombatHealth>();
        var target = first;
        var targetLocal = session.Config.PlayerLocalSpawn + UnityEngine.Vector3.right * 20;
        var shooterLocal = targetLocal + new UnityEngine.Vector3(2.5f, 0, 0);
        fixture = new UnityEngine.GameObject("CharacterProbePlatform");
        fixture.transform.SetParent(platform, false);
        fixture.transform.localPosition = targetLocal - UnityEngine.Vector3.up * .3f;
        var floor = fixture.AddComponent<UnityEngine.BoxCollider>();
        floor.size = new UnityEngine.Vector3(10, .5f, 10);
        void Place(PirateSlop.Networking.NetworkPlayer player, UnityEngine.Vector3 local)
        {
            player.GetComponent<PirateSlop.Networking.NetworkHealth>().Respawn(platform.TransformPoint(local), platform.eulerAngles.y);
        }
        System.Collections.IEnumerator Delay(float seconds)
        {
            double until = UnityEngine.Time.realtimeSinceStartupAsDouble + seconds;
            while (UnityEngine.Time.realtimeSinceStartupAsDouble < until)
            {
                if (target == null || platform == null || view == null)
                    throw new System.InvalidOperationException("Character probe objects were removed.");
                view.transform.SetPositionAndRotation(platform.TransformPoint(targetLocal + new UnityEngine.Vector3(5, 2.5f, -3)),
                    UnityEngine.Quaternion.LookRotation(target.transform.position + UnityEngine.Vector3.up - platform.TransformPoint(targetLocal + new UnityEngine.Vector3(5, 2.5f, -3))));
                yield return null;
            }
        }
        Place(shooter, shooterLocal);
        var metadata = PirateSlop.Performance.PerformanceBenchmarkController.CreateMetadata("character-hits-editor");
        metadata.routeVersion = "character-hits-v2";
        metadata.warmState = "Fresh Play session; 20m beside ship on a temporary box platform; normal scene warmup; impacts/corpses not prewarmed; server action without input prediction";
        metadata.role = "Editor local host with synthetic participants; not remote-client proof";
        metadata.expectedShips = 4; metadata.expectedPlayers = 12;
        metadata.warmupSeconds = 20; metadata.requestedSeconds = 70;
        metadata.tickRate = session.Config.TickRate;
        metadata.fov = view.fieldOfView; metadata.nearClip = view.nearClipPlane; metadata.farClip = view.farClipPlane;
        capture.Prepare(metadata); capture.View = view; capture.Segment = 0;
        yield return Delay(20);
        capture.StartRecording();
        yield return Delay(5);
        for (int phase = 0; phase < 3; phase++)
        {
            capture.Segment = phase + 1;
            target = phase == 1 ? second : first;
            health = target.GetComponent<PirateSlop.CombatHealth>();
            Place(target, targetLocal);
            Place(shooter, shooterLocal);
            yield return Delay(3);
            for (int shot = 0; shot < 3; shot++)
            {
                var eye = shooter.transform.position + UnityEngine.Vector3.up * 1.65f;
                var direction = (target.transform.position + UnityEngine.Vector3.up * .9f - eye).normalized;
                var definition = shooter.GetComponent<PirateSlop.FirearmHandling>().Pistol;
                var resolved = PirateSlop.FirearmTrace.Resolve(shooter.gameObject, eye, definition.MuzzlePoint(eye, direction, true), direction, definition.Ballistics.Range, out var hit, definition.TracerSpeed);
                if (hit.collider == null || hit.collider.GetComponentInParent<PirateSlop.CombatHealth>() != health)
                    throw new System.InvalidOperationException("Character probe line of fire is obstructed by " + (hit.collider != null ? hit.collider.name : "no target") + "; no shot fired.");
                PirateSlop.Performance.PerformanceCapture.Event("CharacterProbe:shot", target, target.transform.position, phase * 3 + shot);
                float before = health.Current;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                if (!weapon.TryAct(0, direction, UnityEngine.Vector3.up * 1.65f, true, 41719 + phase * 3 + shot))
                    throw new System.InvalidOperationException("Character probe authoritative shot rejected: pistol=" + inventory.PistolSelected + ", swimming=" + shooter.GetComponent<AdvancedPlayerController>().IsSwimming + ", locked=" + shooter.GetComponent<AdvancedPlayerController>().LocomotionLocked + ", loaded=" + shooter.GetComponent<PirateSlop.PirateWeapon>().Loaded);
                timer.Stop();
                PirateSlop.Performance.PerformanceCapture.Event("CharacterProbe:shotCpuMs", target, target.transform.position, (float)timer.Elapsed.TotalMilliseconds);
                if (health.Current >= before)
                    throw new System.InvalidOperationException("Character probe shot did not damage the intended character.");
                yield return Delay(1);
                if (shot < 2 || phase < 2)
                {
                    if (!weapon.TryAct(1, direction, UnityEngine.Vector3.up * 1.65f, true))
                        throw new System.InvalidOperationException("Character probe reload rejected.");
                    yield return Delay(4);
                }
            }
            if (!health.IsDead) throw new System.InvalidOperationException("Character probe expected a lethal third shot.");
            yield return Delay(7);
        }
        capture.Finish("COMPLETE", "Separate Editor character impact probe; not a Release FPS baseline or remote input test.", segments);
        complete = true;
    }
    finally
    {
        if (!complete && capture != null) capture.Finish("INVALID", "Character probe failed; inspect Console for the failing invariant.", segments);
        if (fixture != null) UnityEngine.Object.Destroy(fixture);
        if (controller != null) { controller.Finish("CANCELLED", "Character probe cleanup."); controller.enabled = true; }
        else if (session.PerformanceBenchmarkActive) session.EndPerformanceBenchmark();
    }
}
session.StartCoroutine(Run());
return "Character impact probe started; fresh world, 20s warmup, 9 authoritative pistol shots across 2 characters/3 deaths, then cleanup.";
