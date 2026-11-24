using PirateSlop.World;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class ShipTutorial
    {
        static readonly Color tar = new(.12f,.10f,.08f), oak = new(.40f,.23f,.11f), brass = new(.92f,.69f,.31f),
            ivory = new(.96f,.90f,.72f), vermilion = new(.68f,.17f,.10f);
        GUIStyle speaker, dialogue, bearing;
        MaterialPropertyBlock paint;
        Vector2 dialoguePosition;
        int dialogueFrame = -1;
        Camera dialogueCamera;

        GameObject Group(string label, Vector3 point)
        {
            var obj = new GameObject(label);
            obj.transform.SetParent(props.transform, false);
            obj.transform.position = point;
            return obj;
        }

        void Part(GameObject parent, PrimitiveType shape, Vector3 position, Vector3 scale, Color color, Quaternion rotation)
        {
            var obj = GameObject.CreatePrimitive(shape);
            obj.transform.SetParent(parent.transform, false);
            obj.transform.SetLocalPositionAndRotation(position, rotation);
            obj.transform.localScale = scale;
            paint ??= new MaterialPropertyBlock();
            paint.SetColor("_Color", color);
            paint.SetColor("_BaseColor", color);
            obj.GetComponent<Renderer>().SetPropertyBlock(paint);
        }

        void BuildGate()
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var buoy = Group("Channel buoy", gate + TutorialBay.GateRight * (TutorialBay.GateHalfWidth * side));
                Part(buoy, PrimitiveType.Sphere, Vector3.zero, new Vector3(2.5f,2.9f,2.5f), tar, Quaternion.identity);
                Part(buoy, PrimitiveType.Cylinder, Vector3.up * 1.7f, new Vector3(.65f,1.55f,.65f), ivory, Quaternion.identity);
                Part(buoy, PrimitiveType.Cylinder, Vector3.up * 2.25f, new Vector3(.70f,.30f,.70f), vermilion, Quaternion.identity);
                Part(buoy, PrimitiveType.Sphere, Vector3.up * 3.45f, Vector3.one * .95f, brass, Quaternion.identity);
                buoy.transform.localScale = Vector3.one * 2.3f;
            }
        }

        void BuildTarget()
        {
            Vector3 direction = Vector3.ProjectOnPlane(cannon.Muzzle.forward, Vector3.up).normalized;
            var point = cannon.Muzzle.position + direction * 34;
            point.y = OceanSurface.Instance.Height(point);
            target = Group("Gunnery raft", point);
            target.transform.rotation = Quaternion.LookRotation(direction);
            for (int i = -2; i <= 2; i++)
                Part(target, PrimitiveType.Cylinder, new Vector3(i * 1.10f,0,0), new Vector3(1.18f,2.9f,1.18f), oak, Quaternion.Euler(90,0,0));
            Part(target, PrimitiveType.Cube, Vector3.up * 2f, new Vector3(.7f,4f,.7f), tar, Quaternion.identity);
            for (int ring = 0; ring < 4; ring++)
            {
                float diameter = 6.8f - ring * 1.6f;
                Part(target, PrimitiveType.Cylinder, new Vector3(0,4.7f,-ring*.09f),
                    new Vector3(diameter,.12f,diameter), ring % 2 == 0 ? vermilion : ivory, Quaternion.Euler(90,0,0));
            }
        }

        void OnGUI()
        {
            if (PirateSlop.GameVersionOverlay.InterfaceHidden) return;
            if (monkey == null || !monkey.Visual || string.IsNullOrEmpty(line) || SessionController.MenuOpen ||
                PlayerInventory.LootWindowOpen || RoguelikeUpgradeUI.WindowOpen || Event.current.type != EventType.Repaint) return;
            var camera = player.Motor.PlayerCamera;
            if (camera == null) return;
            float scale = Mathf.Clamp(Screen.height / 900f,.70f,1.65f);
            var old = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1));
            float width = Screen.width / scale, height = Screen.height / scale;
            Vector3 world = monkey.Visual.position + ship.transform.up * .95f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            Vector2 pin = new(screen.x / scale, height - screen.y / scale);
            bool visible = screen.z > 0 && pin.x > 0 && pin.y > 0 && pin.x < width && pin.y < height;
            if (!(step == Lesson.Helm && usedWheel) && step != Lesson.Welcome && step != Lesson.Search && step != Lesson.Carry && step < Lesson.Upgrade && !ShipSpyglassView.IsViewing)
            {
                Vector3 marker = camera.WorldToScreenPoint(focus + Vector3.up * .35f);
                if (marker.z > .5f)
                {
                    var center = new Vector2(marker.x / scale, height - marker.y / scale);
                    var saved = GUI.matrix;
                    GUIUtility.RotateAroundPivot(45, center);
                    Panel(new Rect(center.x-4,center.y-4,8,8),brass,1);
                    Panel(new Rect(center.x-2,center.y-2,4,4),tar,1);
                    GUI.matrix = saved;
                }
            }
            if (speaker == null)
            {
                speaker = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = brass } };
                dialogue = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, normal = { textColor = ivory } };
                bearing = new GUIStyle(speaker) { alignment = TextAnchor.MiddleCenter };
            }
            float boxWidth = Mathf.Min(355, width - 32);
            float boxHeight = 48 + dialogue.CalcHeight(new GUIContent(line),boxWidth - 32);
            if (step == Lesson.Helm) DrawGateMarker(camera, scale, width, height);

            var fallback = new Vector2(24f, height - boxHeight - 110f);
            var desired = fallback;
            if (visible && !(step == Lesson.Helm && usedWheel))
            {
                var aboveMonkey = new Vector2(Mathf.Clamp(pin.x - boxWidth * .5f, 12f, width - boxWidth - 12f),
                    Mathf.Clamp(pin.y - boxHeight - 10f, 20f, height - boxHeight - 24f));
                float edge = Mathf.Min(Mathf.Min(pin.x, width - pin.x), Mathf.Min(pin.y, height - pin.y));
                desired = Vector2.Lerp(fallback, aboveMonkey, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / 65f)));
            }
            if (dialogueCamera != camera || Time.frameCount > dialogueFrame + 1) dialoguePosition = desired;
            else if (dialogueFrame != Time.frameCount)
                dialoguePosition = Vector2.Lerp(dialoguePosition, desired, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f));
            dialogueCamera = camera;
            dialogueFrame = Time.frameCount;
            float x = Mathf.Clamp(dialoguePosition.x, 12f, width - boxWidth - 12f);
            float y = Mathf.Clamp(dialoguePosition.y, 20f, height - boxHeight - 24f);
            var box = new Rect(x,y,boxWidth,boxHeight);
            Panel(new Rect(x+2,y+4,boxWidth,boxHeight),new Color(0,0,0,.38f),14);
            Panel(box,new Color(.56f,.41f,.22f,.98f),14);
            Panel(new Rect(x+1.5f,y+1.5f,boxWidth-3,boxHeight-3),new Color(.065f,.115f,.12f,.97f),13);
            float tail = Mathf.Clamp(pin.x,x+16,x+boxWidth-16);
            if (visible && !(step == Lesson.Helm && usedWheel))
            {
                var saved = GUI.matrix;
                GUIUtility.RotateAroundPivot(45,new Vector2(tail,y+boxHeight-2));
                Panel(new Rect(tail-5,y+boxHeight-7,10,10),new Color(.065f,.115f,.12f),1);
                GUI.matrix = saved;
            }
            GUI.Label(new Rect(x+16,y+8,boxWidth-32,23),Localization.T("КОРАБЕЛЬНАЯ ОБЕЗЬЯНКА"),speaker);
            GUI.Label(new Rect(x+16,y+34,boxWidth-32,boxHeight-36),line,dialogue);
            GUI.matrix = old;
        }

        void DrawGateMarker(Camera camera, float scale, float width, float height)
        {
            var world = gate + Vector3.up * 13f;
            var projected = camera.WorldToScreenPoint(world);
            var pin = new Vector2(projected.x / scale, height - projected.y / scale);
            bool onScreen = projected.z > 0f && pin.x > 75f && pin.x < width - 75f && pin.y > 60f && pin.y < height - 80f;
            Vector2 direction = Vector2.down;
            if (!onScreen)
            {
                var center = new Vector2(width * .5f, height * .5f);
                if (projected.z <= 0f)
                {
                    float side = camera.transform.InverseTransformPoint(world).x;
                    direction = side < 0f ? Vector2.left : Vector2.right;
                }
                else direction = (pin - center).normalized;
                float borderX = (center.x - 82f) / Mathf.Max(.001f, Mathf.Abs(direction.x));
                float borderY = (center.y - 90f) / Mathf.Max(.001f, Mathf.Abs(direction.y));
                pin = center + direction * Mathf.Min(borderX, borderY);
            }

            var saved = GUI.matrix;
            if (onScreen)
            {
                GUIUtility.RotateAroundPivot(45f, pin);
                Panel(new Rect(pin.x - 7f, pin.y - 7f, 14f, 14f), tar, 2f);
                Panel(new Rect(pin.x - 5f, pin.y - 5f, 10f, 10f), brass, 1f);
            }
            else
            {
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                GUIUtility.RotateAroundPivot(angle, pin);
                GUI.Label(new Rect(pin.x - 13f, pin.y - 14f, 26f, 28f), "▶", bearing);
            }
            GUI.matrix = saved;
        }

        static void Panel(Rect rect, Color color, float radius) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
    }
}
