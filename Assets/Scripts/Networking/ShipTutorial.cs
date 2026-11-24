using System.Collections.Generic;
using PirateSlop.Ships;
using PirateSlop.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateSlop.Networking
{
    public sealed partial class ShipTutorial : MonoBehaviour
    {
        enum Lesson { Welcome, Anchor, Sail, Helm, Stop, Dispense, PickUp, Load, Fire, ReturnFire, Gauge, Repair, Pump, Climb, Search, Carry, Deck, Loot, Upgrade, Done }
        static ShipTutorial active;
        Lesson step;
        NetworkPlayer player;
        NetworkShip ship;
        ShipMonkey monkey;
        ShipV3Features fittings;
        SailSystem sails;
        SimpleCannon cannon;
        ShipFlooding flood;
        ShipBilgePump pump;
        ShipSpyglass spyglass;
        NetworkLootChest chest;
        NetworkWeapon weapon;
        CannonHands hands;
        Transform gauge;
        GameObject props, target;
        readonly List<Cannonball> issued = new();
        Vector3 gate, lastShipPoint, focus, meetingPoint;
        Lesson? guideLesson;
        ShipBreach breach;
        bool LeakOpen => flood.TryGetOpening(breach.SectionId, breach.FragmentIndex, out _);
        float since, nextTick, nextBreach, gazeTime, initialTension;
        const int MizzenSail = 4;
        bool hit, lootTaken, pickedUp, usedWheel;
        int upgrades;
        ShipSpyglassView scope;
        Renderer gaugeFace;
        Vector3 battery;
        string line;
        string Use => KeyBindings.Label(GameAction.Use);
        string CarryKey => KeyBindings.Label(GameAction.Carry);

        void Start()
        {
            active = this;
            player = GetComponent<NetworkPlayer>();
            hands = GetComponent<CannonHands>();
            weapon = GetComponent<NetworkWeapon>();
            scope = GetComponent<ShipSpyglassView>();
        }

        void Setup()
        {
            ship = player.Ship;
            monkey = ship.GetComponent<ShipMonkey>();
            meetingPoint = monkey.StartTutorialGreeting(transform.position);
            monkey.SetGuideFocus(transform.position + ship.transform.up * 1.5f);
            fittings = ship.GetComponent<ShipV3Features>();
            fittings.BallDispensed += OnBall;
            flood = ship.GetComponent<ShipFlooding>();
            flood.TutorialLimits = true;
            sails = ship.GetComponent<SailSystem>();
            pump = ship.GetComponentInChildren<ShipBilgePump>();
            spyglass = ship.GetComponentInChildren<ShipSpyglass>();
            var instrument = ship.GetComponentInChildren<MastFloodGauge>();
            gauge = instrument != null ? instrument.transform : sails.RopeHandles[0].transform;
            gaugeFace = instrument != null ? instrument.GetComponent<Renderer>() : null;
            props = new GameObject("FirstVoyage");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(props, ship.gameObject.scene);
            gate = TutorialBay.Gate(ProceduralWorld.Instance.Layout.SeaLevel);
            BuildGate();
            initialTension = sails.Tension(MizzenSail);
            ship.DropAnchorServerRpc();
            PrepareGun();
            Enter(Lesson.Welcome);
        }

        void PrepareGun()
        {
            cannon = ship.GetComponentInChildren<SimpleCannon>();
            if (cannon != null) return;
            var guns = ship.GetComponent<NetworkCannon>();
            foreach (var hint in SessionController.Instance.Config.BotMotion.CannonSites)
            {
                if (hint.x <= 0 || !BotCannonPlacement.Find(ship, hint, out var site)) continue;
                guns.Place(site.Point, site.Rotation);
                cannon = ship.GetComponentInChildren<SimpleCannon>();
                if (cannon != null) return;
            }
            for (float z = -7; z <= 8; z += 1f)
                for (float x = 2.1f; x <= 4.5f; x += .5f)
                    if (BotCannonPlacement.Find(ship, new Vector3(x, 4.3f, z), out var site))
                    {
                        guns.Place(site.Point, site.Rotation);
                        cannon = ship.GetComponentInChildren<SimpleCannon>();
                        return;
                    }
            foreach (var hint in SessionController.Instance.Config.BotMotion.CannonSites)
            {
                if (hint.x <= 0) continue;
                guns.Place(hint, Quaternion.Euler(0, 90, 0));
                cannon = ship.GetComponentInChildren<SimpleCannon>();
                return;
            }
        }

        void Enter(Lesson next)
        {
            step = next; since = Time.time; gazeTime = 0;
            lastShipPoint = ship.transform.position;
        }

        void OnBall(Cannonball ball)
        {
            issued.RemoveAll(b => b == null);
            issued.Add(ball);
        }

        public static bool HitTarget(Collider surface)
        {
            if (active == null || active.target == null || active.step != Lesson.Fire && active.step != Lesson.Load ||
                !surface.transform.IsChildOf(active.target.transform)) return false;
            active.hit = true;
            active.target.SetActive(false);
            return true;
        }

        internal static void Collected(NetworkFish pickup)
        {
            if (active == null || active.pickedUp) return;
            var ball = pickup.GetComponent<Cannonball>();
            if (ball != null && active.issued.Contains(ball)) active.pickedUp = true;
        }

        internal static bool CanCarry(NetworkLootChest source) =>
            active != null && active.chest == source && active.step >= Lesson.Carry;

        internal static void TookLoot(NetworkLootChest source)
        {
            if (active != null && active.chest == source) active.lootTaken = true;
        }

        void Update()
        {
            if (SessionController.Instance == null || !SessionController.Instance.TutorialActive || !player.IsSpawned) return;
            if (ship == null) { if (player.Ship != null) Setup(); return; }
            if (!pickedUp && step <= Lesson.PickUp)
            {
                if (hands.HeldBall != null && issued.Contains(hands.HeldBall)) pickedUp = true;
            }
            if (step >= Lesson.Dispense && step <= Lesson.Fire && (cannon == null || !cannon.isActiveAndEnabled))
            {
                PrepareGun();
                if (cannon == null) return;
                if (target != null && !hit) { Destroy(target); BuildTarget(); }
            }
            if (Time.time < nextTick) return;
            nextTick = Time.time + .1f;
            var here = transform.position;
            var head = here + Vector3.up * 1.5f;
            focus = head;
            var currentLesson = step;
            switch (currentLesson)
            {
                case Lesson.Welcome:
                    line = Localization.T("Ага, вот и капитан! Я покажу корабль. Сначала снимемся с якоря. Иди за мной!");
                    if (Time.time - since > 5 && (Vector3.Distance(monkey.Visual.position, here) < 4 || Time.time - since > 12)) Enter(Lesson.Anchor);
                    break;
                case Lesson.Anchor:
                    focus = ship.Capstan.transform.position;
                    line = Localization.T("Хватай рукоять шпиля левой кнопкой мыши. Держи и иди по кругу, пока якорь не поднимется.");
                    if (!ship.AnchorDropped) Enter(Lesson.Sail);
                    break;
                case Lesson.Sail:
                    focus = sails.RopeHandles[MizzenSail].transform.position;
                    line = Localization.T("За штурвалом кормовой парус — бизань. Зажми ЛКМ на его канате и тяни мышь вниз, чтобы распустить парус.");
                    if (sails.Tension(MizzenSail) >= Mathf.Min(1f, initialTension + .55f)) Enter(Lesson.Helm);
                    break;
                case Lesson.Helm:
                    usedWheel |= ship.Helm.LastHumanControlTime >= since || ship.Helm.Driver != null;
                    focus = usedWheel ? gate + Vector3.up * 13f : ship.Helm.transform.position;
                    line = Localization.T("Берись за штурвал левой кнопкой и поворачивай мышью. Ищи золотую метку и проведи нас между буями.");
                    var now = ship.transform.position;
                    if (usedWheel && CrossedGate(lastShipPoint, now)) Enter(Lesson.Stop);
                    lastShipPoint = now;
                    break;
                case Lesson.Stop:
                    focus = ship.Capstan.transform.position;
                    line = Localization.F("Отлично прошли! Удерживай [{0}] у шпиля, чтобы сбросить якорь. Со стоянки стрелять удобнее.", Use);
                    if (ship.AnchorDropped)
                    {
                        if (cannon == null && Time.time >= nextBreach) { PrepareGun(); nextBreach = Time.time + 2f; }
                        if (cannon == null) line = Localization.T("Готовим место на правом борту. Свободную палубу у фальшборта оставь для пушки.");
                        if (cannon != null) { BuildTarget(); Enter(Lesson.Dispense); }
                    }
                    break;
                case Lesson.Dispense:
                    focus = fittings.DispenserGrip.position;
                    line = Localization.T("К корме, к маске с рычагом! Зажми ЛКМ на ручке и тяни вниз до упора. Маска выплюнет ядро.");
                    if (issued.Exists(b => b != null) || pickedUp) Enter(Lesson.PickUp);
                    break;
                case Lesson.PickUp:
                    var ball = issued.FindLast(b => b != null);
                    focus = ball != null ? ball.transform.position : fittings.DispenserGrip.position;
                    line = Localization.T("Вот наше ядро! Возьми его ЛКМ и неси к пушке. Потеряешь — тяни рычаг ещё раз.");
                    if (pickedUp) Enter(Lesson.Load);
                    break;
                case Lesson.Load:
                    focus = cannon.Muzzle.position;
                    line = Localization.T("Поднеси ядро к дулу. Если потерял его, возьми ещё одно у маски.");
                    if (cannon.IsLoaded || hit) Enter(Lesson.Fire);
                    break;
                case Lesson.Fire:
                    focus = target.transform.position;
                    line = Localization.F("У пушки нажми [{0}], наведи ствол на красно-белый щит и нажми ЛКМ. Промах? Возьми новое ядро у маски.", Use);
                    if (hit)
                    {
                        battery = TutorialBay.ShoreBattery(gate.y);
                        CombatVfx.Fire(battery, (ship.transform.position - battery).normalized, true);
                        GameAudio.Play(SoundCue.Cannon, battery);
                        Enter(Lesson.ReturnFire);
                    }
                    break;
                case Lesson.ReturnFire:
                    line = Localization.T("Береговая пушка отвечает! Держись за борт!");
                    focus = battery;
                    if (Time.time - since > 1.25f && Time.time >= nextBreach)
                    {
                        nextBreach = Time.time + 1;
                        if (OpenLeak()) Enter(Lesson.Gauge);
                    }
                    break;
                case Lesson.Gauge:
                    focus = gaugeFace != null ? gaugeFace.bounds.center : gauge.position;
                    line = Localization.T("Попали! А у нас течь. Глянь на колбу мачты. Синее — вода в трюме. Полна? Красное вытеснит воду и покажет, сколько осталось до гибели.");
                    if (Time.time - since > 5 && (Looking(focus, .4f) || Time.time - since > 13)) Enter(Lesson.Repair);
                    break;
                case Lesson.Repair:
                    bool inHold = ship.transform.InverseTransformPoint(here).y < 3.2f;
                    focus = inHold ? ship.transform.TransformPoint(breach.LocalPoint) : BilgeEntrance;
                    line = !inHold ? Localization.T("За мной к люку. Вход в трюм с носовой стороны. Внизу найдёшь пробоину.") : Localization.T("Вот пробоина! Выбери молоток, наведи на повреждение и бей отдельными нажатиями ЛКМ, пока течь не закроется.");
                    if (!LeakOpen) Enter(Lesson.Pump);
                    else if (!weapon.HasRepairMallet)
                    {
                        if (Time.time >= nextBreach)
                        {
                            nextBreach = Time.time + 2;
                            weapon.AddItem(InventoryItem.Mallet);
                        }
                        if (!weapon.HasRepairMallet)
                            line = Localization.T("Освободи ячейку, капитан! Я дам тебе молоток для ремонта.");
                    }
                    break;
                case Lesson.Pump:
                    focus = ship.transform.InverseTransformPoint(here).y < 3.2f ? pump.LeverGrip.position : BilgeEntrance;
                    line = Localization.F("Течь закрыта. Спустись через люк и держи [{0}] у насоса, пока не откачаешь воду.", Use);
                    if (flood.Level < .07f) { SpawnChest(); Enter(Lesson.Climb); }
                    break;
                case Lesson.Climb:
                    focus = spyglass.transform.position;
                    line = Localization.F("Пора за добычей! Подойди к вантам и нажми [{0}]. Держи [{1}], чтобы подняться к подзорной трубе.", CarryKey, KeyBindings.Label(GameAction.Forward));
                    if (Vector3.Distance(here, focus) < 4) Enter(Lesson.Search);
                    break;
                case Lesson.Search:
                    focus = spyglass.transform.position;
                    line = Localization.F("Посмотри в трубу [{0}]. Огляди воду и найди ближайший сундук по столбу света.", Use);
                    bool spotted = scope.Sights(spyglass, chest);
                    gazeTime = spotted ? gazeTime + .1f : 0;
                    if (gazeTime >= .4f) Enter(Lesson.Carry);
                    break;
                case Lesson.Carry:
                    focus = chest.transform.position;
                    line = Localization.F("Нашёл! Выходи из трубы [{0}], спускайся к воде и возьми сундук [{1}].", Use, CarryKey);
                    if (chest.Carrier != null || chest.SupportingShip == ship) Enter(Lesson.Deck);
                    break;
                case Lesson.Deck:
                    focus = chest.Carrier == null ? chest.transform.position : ship.transform.TransformPoint(new Vector3(0,4,1));
                    line = Localization.F("Донеси его на корабль. Встань на палубу и поставь сундук [{0}]. Уронил в воду? Его можно подобрать снова.", KeyBindings.Label(GameAction.Drop));
                    if (chest.SupportingShip == ship) Enter(Lesson.Loot);
                    break;
                case Lesson.Loot:
                    focus = chest != null ? chest.transform.position : head;
                    line = Localization.F("Открой сундук [{0}] и забери добычу в инвентарь.", Use);
                    if (lootTaken) Enter(Lesson.Upgrade);
                    break;
                case Lesson.Upgrade:
                    line = Localization.F("Теперь закрой сундук и открой улучшения [{0}]. Выбери одну из предложенных карт.", KeyBindings.Label(GameAction.Upgrades));
                    if (player.UpgradeState != null && player.UpgradeState.Taken.Count > upgrades) Enter(Lesson.Done);
                    break;
                case Lesson.Done:
                    line = Localization.F("Вот это рейс, капитан! Добыча наша, улучшение работает. [{0}] — закончить обучение.", Use);
                    break;
            }
            if (guideLesson != currentLesson)
            {
                monkey.SetGuideDestination(GuideStation(currentLesson));
                guideLesson = currentLesson;
            }
            monkey.SetGuideFocus(focus);
        }

        Vector3 BilgeEntrance => ship.transform.TransformPoint(new Vector3(0f, 4.12f, 10.8f));

        Vector3 GuideStation(Lesson lesson)
        {
            return lesson switch
            {
                Lesson.Welcome => ship.transform.TransformPoint(meetingPoint),
                Lesson.Anchor or Lesson.Stop => ship.Capstan.transform.position,
                Lesson.Sail => sails.RopeHandles[MizzenSail].transform.position,
                Lesson.Gauge => gauge.position,
                Lesson.Helm => ship.Helm.transform.position,
                Lesson.Dispense or Lesson.PickUp => fittings.DispenserGrip.position,
                Lesson.Load or Lesson.Fire or Lesson.ReturnFire => cannon.transform.position,
                Lesson.Repair or Lesson.Pump => BilgeEntrance,
                Lesson.Climb or Lesson.Search => spyglass.transform.position,
                Lesson.Carry or Lesson.Deck => ship.transform.TransformPoint(new Vector3(3.8f, 4.5f, 2f)),
                Lesson.Loot when chest != null => chest.transform.position,
                _ => ship.transform.TransformPoint(meetingPoint)
            };
        }

        internal static bool FocusGreeting(AdvancedPlayerController motor)
        {
            if (active == null || active.ship == null || active.player.Motor != motor ||
                active.step != Lesson.Welcome || Time.time - active.since >= 2f || SessionController.MenuOpen) return false;
            var guide = active.monkey;
            motor.LookAtPoint(guide.Head != null ? guide.Head.position : guide.Visual.position + active.ship.transform.up * .7f, 1000f);
            return true;
        }

        void LateUpdate()
        {
            if (step == Lesson.Done && Time.time - since > 1 && !SessionController.MenuOpen && !RoguelikeUpgradeUI.WindowOpen &&
                Keyboard.current != null && Keyboard.current[KeyBindings.Use].wasPressedThisFrame)
                SessionController.Instance.Disconnect();
        }

        bool CrossedGate(Vector3 from, Vector3 to)
        {
            float before = Vector3.Dot(from - gate, TutorialBay.GateForward);
            float after = Vector3.Dot(to - gate, TutorialBay.GateForward);
            if (before * after > 0f || Mathf.Abs(after - before) < .001f) return false;
            var crossing = Vector3.Lerp(from, to, -before / (after - before));
            return Mathf.Abs(Vector3.Dot(crossing - gate, TutorialBay.GateRight)) < TutorialBay.GateHalfWidth - 2f;
        }

        bool Looking(Vector3 point, float margin)
        {
            Vector3 screen = player.Motor.PlayerCamera.WorldToViewportPoint(point);
            return screen.z > 0 && Mathf.Abs(screen.x - .5f) < margin && Mathf.Abs(screen.y - .5f) < margin;
        }

        bool OpenLeak()
        {
            if (!ship.GetComponent<ShipDestruction>().TryOpenScriptedBreach(true, null, out breach)) return false;
            CombatVfx.Impact(ship.transform.TransformPoint(breach.LocalPoint), -ship.transform.right, true, true);
            flood.SetLevel(.24f);
            if (!weapon.HasRepairMallet) weapon.AddItem(InventoryItem.Mallet);
            return true;
        }

        void SpawnChest()
        {
            var config = SessionController.Instance.Config;
            Vector3 point = ship.transform.position + ship.transform.right * 29 + ship.transform.forward * 14;
            point.y = OceanSurface.Instance.Height(point);
            chest = Instantiate(config.Loot.ChestPrefab, point, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(chest.gameObject, ship.gameObject.scene);
            chest.Catalog = config.Loot;
            var random = new MapRandom(751);
            chest.Fill(ref random);
            chest.PlaceTutorialCargo(point);
            ship.ServerManager.Spawn(chest.NetworkObject);
            upgrades = player.UpgradeState.Taken.Count;
        }

        void OnDestroy()
        {
            if (active == this) active = null;
            if (fittings != null) fittings.BallDispensed -= OnBall;
            if (props != null) Destroy(props);
        }
    }
}
