using UnityEngine;
using System.Linq;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        int menuPage;
        bool useTestMap;
        GUIStyle menuTitle, menuLabel, menuSmall, menuButton, menuInput, menuSlider, menuThumb, menuFooter;
        Texture2D menuShade, menuField, menuFieldFocus, menuThumbTexture;
        Texture2D menuTitlePrint;
        Font menuDisplayFont, menuBodyFont;
        readonly Color menuGold = new(.77f,.66f,.45f);
        readonly Color menuIvory = new(.92f,.89f,.81f);
        readonly Color menuMuted = new(.48f,.58f,.61f);
        readonly System.Collections.Generic.List<string> menuControls = new();
        readonly System.Collections.Generic.List<string> menuPreviousControls = new();
        readonly System.Collections.Generic.Dictionary<string, MenuAccent> menuAccents = new();
        string menuFocus, menuSubmit;
        int menuContext = -1, menuControlIndex, menuInputFrame = -1;
        Vector2 menuPointer;
        bool menuPointerMode = true, menuWasVisible;
        float nextMenuHoverAudio, nextMenuPadMove;
        sealed class MenuAccent
        {
            public float From, Target, Started;
            public float Value => Mathf.Lerp(From, Target, Mathf.SmoothStep(0, 1, Mathf.Clamp01((Time.unscaledTime - Started) / .16f)));
        }
        static bool MenuDeveloperTools => DeveloperMenu.Available;
        void PrepareMenu()
        {
            if(menuTitle!=null) return;
            var sc = GameObject.Find("SettingsCanvas");
            if (sc != null) Destroy(sc);
            menuDisplayFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman"},64);
            menuBodyFont=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},22);
            menuTitle=new GUIStyle(GUI.skin.label){font=menuDisplayFont,fontSize=68,fontStyle=FontStyle.Bold,normal={textColor=menuIvory}};
            menuTitlePrint=Resources.Load<Texture2D>("Menu/TitlePrint");
            menuLabel=new GUIStyle(GUI.skin.label){font=menuBodyFont,fontSize=20,wordWrap=true,normal={textColor=menuIvory}};
            menuSmall=new GUIStyle(menuLabel){fontSize=16,normal={textColor=new Color(.78f,.80f,.76f)}};
            menuFooter=new GUIStyle(menuSmall){fontSize=12,normal={textColor=new Color(.63f,.70f,.71f,.65f)}};
            MenuInkColor(menuFooter,menuFooter.normal.textColor);
            menuButton=new GUIStyle(menuLabel){font=menuDisplayFont,fontSize=22,fontStyle=FontStyle.Normal,alignment=TextAnchor.MiddleLeft,wordWrap=false,padding=new RectOffset()};
            menuField=MenuTexture(new Color(.025f,.065f,.08f,.72f));
            menuFieldFocus=MenuTexture(new Color(.045f,.11f,.13f,.88f));
            menuThumbTexture=MenuTexture(menuGold);
            menuInput=new GUIStyle(GUI.skin.textField){font=menuBodyFont,fontSize=22,padding=new RectOffset(15,15,10,10),border=new RectOffset(),normal={background=menuField,textColor=PirateHudStyle.Paper},hover={background=menuFieldFocus,textColor=PirateHudStyle.Paper},focused={background=menuFieldFocus,textColor=PirateHudStyle.Paper},active={background=menuFieldFocus,textColor=PirateHudStyle.Paper}};
            menuSlider=new GUIStyle(){fixedHeight=5,margin=new RectOffset(0,0,8,0),normal={background=menuFieldFocus}};
            menuThumb=new GUIStyle(){fixedWidth=12,fixedHeight=21,normal={background=menuThumbTexture},hover={background=menuThumbTexture},active={background=menuThumbTexture}};
            menuShade=new Texture2D(256,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp};
            for(int x=0;x<256;x++)
            {
                float t=x/255f;
                float a=.78f*(1f-Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.4f,1f,t)));
                menuShade.SetPixel(x,0,new Color(.018f,.055f,.072f,a));
            }
            menuShade.Apply(false,true);
            if(!connecting && !playing) address=PlayerPrefs.GetString("LastEndpoint","127.0.0.1:"+Config.Port);
        }
        void MenuText(Rect rect,string text,bool small=false) => MenuInk(rect,text,small?menuSmall:menuLabel);
        void MenuInk(Rect rect,string text,GUIStyle style)
        {
            var color=style.normal.textColor;
            MenuInkColor(style,new Color(.012f,.035f,.044f,.65f));
            GUI.Label(new Rect(rect.x+1,rect.y+1.5f,rect.width,rect.height),text,style);
            MenuInkColor(style,color);
            GUI.Label(rect,text,style);
        }
        void MenuInkColor(GUIStyle style,Color color)
        {
            style.normal.textColor=style.hover.textColor=style.active.textColor=style.focused.textColor=color;
        }
        void BeginMenuNavigation()
        {
            int context=menuPage+(connecting?100:chooseObserver?200:playing?300:0);
            if(context!=menuContext || !menuWasVisible)
            {
                menuContext=context;
                menuFocus=menuSubmit=null;
                GUI.FocusControl(null);
                menuPreviousControls.Clear();
                menuAccents.Clear();
            }
            menuWasVisible=true;
            var current=Event.current;
            if(current.type==EventType.Repaint || current.type==EventType.MouseDown)
            {
                if(current.type==EventType.MouseDown || (current.mousePosition-menuPointer).sqrMagnitude>.25f) menuPointerMode=true;
                menuPointer=current.mousePosition;
            }
            string focused=GUI.GetNameOfFocusedControl();
            bool editing=!string.IsNullOrEmpty(focused) && !focused.StartsWith("menu_action_");
            if(current.type==EventType.KeyDown && GUIUtility.hotControl==0 && (!editing || current.keyCode==KeyCode.Tab))
            {
                if(current.keyCode==KeyCode.DownArrow || current.keyCode==KeyCode.UpArrow || current.keyCode==KeyCode.Tab)
                {
                    MoveMenuFocus(current.keyCode==KeyCode.UpArrow || current.keyCode==KeyCode.Tab && current.shift?-1:1);
                    current.Use();
                }
                else if(current.keyCode==KeyCode.Return || current.keyCode==KeyCode.KeypadEnter || current.keyCode==KeyCode.Space)
                {
                    menuSubmit=menuFocus;
                    current.Use();
                }
            }
#if ENABLE_INPUT_SYSTEM
            if(current.type==EventType.Repaint && menuInputFrame!=Time.frameCount)
            {
                menuInputFrame=Time.frameCount;
                var pad=UnityEngine.InputSystem.Gamepad.current;
                if(pad!=null && GUIUtility.hotControl==0)
                {
                    float vertical=pad.dpad.ReadValue().y;
                    if(Mathf.Abs(vertical)<.5f) vertical=pad.leftStick.ReadValue().y;
                    if(Mathf.Abs(vertical)<.6f) nextMenuPadMove=0;
                    else if(Time.unscaledTime>=nextMenuPadMove)
                    {
                        MoveMenuFocus(vertical>0?-1:1);
                        nextMenuPadMove=Time.unscaledTime+.22f;
                    }
                    if(pad.buttonSouth.wasPressedThisFrame) menuSubmit=menuFocus;
                    if(pad.buttonEast.wasPressedThisFrame && menuPage!=0) menuPage=menuPage==15?8:menuPage==6?3:menuPage==3 || menuPage==4 || menuPage==8 || menuPage==9?7:0;
                }
            }
#endif
            menuControls.Clear();
            menuControlIndex=0;
        }
        void MoveMenuFocus(int direction)
        {
            if(menuPreviousControls.Count==0) return;
            int index=menuPreviousControls.IndexOf(menuFocus);
            index=(Mathf.Max(0,index)+direction+menuPreviousControls.Count)%menuPreviousControls.Count;
            menuFocus=menuPreviousControls[index];
            menuPointerMode=false;
            GUI.FocusControl(menuFocus);
            MenuHoverSound();
        }
        string RegisterMenuControl(Rect rect,string name=null)
        {
            string control=name??"menu_action_"+menuContext+"_"+menuControlIndex++;
            if(!GUI.enabled) return control;
            menuControls.Add(control);
            if(menuFocus==null) menuFocus=control;
            if(menuPointerMode && rect.Contains(Event.current.mousePosition) && menuFocus!=control)
            {
                menuFocus=control;
                MenuHoverSound();
            }
            return control;
        }
        void MenuHoverSound()
        {
            if(Time.unscaledTime<nextMenuHoverAudio) return;
            GameAudio.Play(SoundCue.UpgradeHover,Vector3.zero,.22f,true);
            nextMenuHoverAudio=Time.unscaledTime+.08f;
        }
        bool MenuControl(Rect rect,string control)
        {
            GUI.SetNextControlName(control);
            bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);
            if(GUI.enabled && menuSubmit==control) { clicked=true; menuSubmit=null; }
            if(clicked) { menuFocus=control; GUI.FocusControl(control); }
            return clicked;
        }
        float MenuHighlight(string control,bool selected)
        {
            if(!menuAccents.TryGetValue(control,out var accent))
            {
                accent=new MenuAccent{From=selected?1:0,Target=selected?1:0,Started=Time.unscaledTime};
                menuAccents.Add(control,accent);
            }
            float target=selected?1:0;
            if(accent.Target!=target) { accent.From=accent.Value; accent.Target=target; accent.Started=Time.unscaledTime; }
            return accent.Value;
        }
        bool MenuLink(Rect rect,string text)
        {
            string control=RegisterMenuControl(rect);
            bool hover=GUI.enabled && menuFocus==control;
            var previous=menuSmall.normal.textColor;
            menuSmall.normal.textColor=hover?PirateHudStyle.Paper:menuGold;
            MenuInk(rect,text,menuSmall);
            menuSmall.normal.textColor=previous;
            if(hover) PirateHudStyle.Brush(new Rect(rect.x,rect.yMax-2,rect.width,2),menuGold,true);
            bool clicked=MenuControl(rect,control);
            if(clicked) GameAudio.Play(SoundCue.Select,Vector3.zero,.6f,true);
            return clicked;
        }
        Texture2D MenuTexture(Color color)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixel(0,0,color); texture.Apply(); return texture;
        }
        void MenuDiamond(Vector2 center,float size,Color color)
        {
            var matrix=GUI.matrix;
            GUI.matrix=matrix*Matrix4x4.TRS(center,Quaternion.Euler(0,0,45),Vector3.one);
            PirateHudStyle.Fill(new Rect(-size*.5f,-size*.5f,size,size),color);
            GUI.matrix=matrix;
        }
        void MenuRule(float x,float y,float width)
        {
            PirateHudStyle.Brush(new Rect(x,y,width,1.5f),new Color(menuGold.r,menuGold.g,menuGold.b,.4f),true);
        }
        bool MenuAction(float x,float y,float width,string label,bool primary=false,bool secondary=false)
        {
            var rect=new Rect(x,y,width,secondary?40:48);
            string control=RegisterMenuControl(rect);
            float accent=MenuHighlight(control,GUI.enabled && menuFocus==control);
            menuButton.fontSize=secondary?17:width<180?18:primary?25:22;
            menuButton.fontStyle=primary?FontStyle.Bold:FontStyle.Normal;
            Color idle=secondary?new Color(.61f,.68f,.67f):primary?menuIvory:new Color(.85f,.85f,.79f);
            menuButton.normal.textColor=GUI.enabled?Color.Lerp(idle,new Color(.89f,.79f,.58f),accent):menuMuted;
            float shift=accent*3f, textScale=1f+accent*.015f;
            var matrix=GUI.matrix;
            Vector3 pivot=new(x+32+shift,y+rect.height*.5f,0);
            GUI.matrix=matrix*Matrix4x4.Translate(pivot)*Matrix4x4.Scale(new Vector3(textScale,textScale,1))*Matrix4x4.Translate(-pivot);
            MenuInk(new Rect(x+32+shift,y,(width-40)/textScale,rect.height),label,menuButton);
            GUI.matrix=matrix;
            if(accent>.001f)
            {
                var color=new Color(menuGold.r,menuGold.g,menuGold.b,accent);
                MenuDiamond(new Vector2(x+11+shift,y+rect.height*.5f),primary?6:4.5f,color);
                float end=x+32+shift+menuButton.CalcSize(new GUIContent(label)).x*textScale+18;
                float length=Mathf.Min(48,width-(end-x)-12);
                if(length>=12) PirateHudStyle.Brush(new Rect(end,y+rect.height*.5f,Mathf.Max(1,length*accent),1.5f),color,true);
                else PirateHudStyle.Brush(new Rect(x+32+shift,y+rect.height-3,32*accent,1.5f),color,true);
            }
            bool clicked=MenuControl(rect,control);
            if(clicked) GameAudio.Play(SoundCue.Select,Vector3.zero,.6f,true);
            return clicked;
        }
        void DrawSessionMenu()
        {
            if (PerformanceBenchmarkActive) return;
            if(MatchResultVisible || playing && (BotDebugPanel.ConsumedInput || DeveloperMenu.IsOpen) || dedicated || Automated || !MenuOpen || (PirateSlop.Customization.SailCustomizationUI.IsOpen && PirateSlop.Customization.SailCustomizationUI.Instance != null)) { menuWasVisible=false; return; }
            PrepareMenu();
            var oldMatrix=GUI.matrix; var oldColor=GUI.color;
            GUI.color=Color.white; GUI.DrawTexture(new Rect(0,0,Screen.width*.45f,Screen.height),menuShade);
            float scale=Mathf.Min(Screen.height/900f,Screen.width/1000f), width=Screen.width/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            if(menuPage==5 && party!=null && party.InLobby && !connecting && !chooseObserver)
            {
                scale=Mathf.Min(Screen.height/900f,Screen.width/1280f);
                width=Screen.width/scale;
                GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
                BeginMenuNavigation();
                DrawCrewLobby(width);
                CompleteMenuNavigation();
                GUI.enabled=true; GUI.matrix=oldMatrix; GUI.color=oldColor;
                return;
            }
            float x=Mathf.Clamp(width*.04f,48f,84f), panelWidth=450;
            float titleWidth=Mathf.Min(540,width*.36f);
            if(menuTitlePrint!=null)
            {
                float titleHeight=titleWidth*menuTitlePrint.height/menuTitlePrint.width;
                GUI.color=new Color(.012f,.035f,.044f,.7f);
                GUI.DrawTexture(new Rect(x-3,92,titleWidth,titleHeight),menuTitlePrint);
                GUI.color=menuIvory;
                GUI.DrawTexture(new Rect(x-4,90,titleWidth,titleHeight),menuTitlePrint);
                GUI.color=Color.white;
            }
            else MenuInk(new Rect(x-4,75,610,90),"PIRATE SLOP",menuTitle);
            MenuText(new Rect(x,180,panelWidth,30),playing ? L("Ваше приключение продолжается") : L("Порох. Ром. Последний корабль."),true);
            MenuRule(x,230,76);
            MenuDiamond(new Vector2(x+84,231),3,new Color(menuGold.r,menuGold.g,menuGold.b,.55f));
            float y=274;
            BeginMenuNavigation();
            if(connecting)
            {
                MenuText(new Rect(x,y,panelWidth,70),status);
                float progress=ProceduralWorld.Instance!=null ? ProceduralWorld.Instance.Progress : 0f;
                GUI.color=new Color(.1f,.2f,.22f); GUI.DrawTexture(new Rect(x,y+95,panelWidth,5),Texture2D.whiteTexture);
                GUI.color=menuGold; GUI.DrawTexture(new Rect(x,y+95,panelWidth*Mathf.Clamp01(progress),5),Texture2D.whiteTexture); GUI.color=Color.white;
                MenuText(new Rect(x,y+115,panelWidth,30),L("Подготовка моря и островов…"),true);
                if(MenuAction(x,y+180,panelWidth,L("Отменить подключение"))) Disconnect();
            }
            else if(chooseObserver)
            {
                MenuText(new Rect(x,y,panelWidth,50),L("СВОБОДНАЯ КАМЕРА?"));
                MenuText(new Rect(x,y+60,panelWidth,100),L("На карте будут только боты. Ваш корабль не появится.\nWASD и мышь — полёт, Q / E — высота."),true);
                if(MenuAction(x,y+180,panelWidth,L("Да · наблюдать за ботами"),true)) { observerSelected=true; chooseObserver=false; }
                if(MenuAction(x,y+250,panelWidth,L("Нет · играть на своём корабле"))) { observerSelected=false; chooseObserver=false; }
            }
            else if(menuPage==10) DrawPerformanceMenu(x,y,panelWidth);
            else if(menuPage==11) DrawFleetPerformanceMenu(x,y,panelWidth);
            else if(menuPage==12) DrawPlayersMenu(x,y,panelWidth);
            else if(menuPage==13) DrawBindingsMenu(x,y,panelWidth);
            else if(menuPage==15) DrawPictureMenu(x,y,panelWidth);
            else if(menuPage==14) DrawHowToPlay(x,y,panelWidth);
            else if(menuPage==5) { DrawSteamParty(x,y-42,panelWidth); }
            else if(menuPage==1)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("СОЗДАТЬ ЭКСПЕДИЦИЮ"));
                MenuText(new Rect(x,y+42,panelWidth,25),L("Адрес IPv4:порт"),true);
                RegisterMenuControl(new Rect(x,y+68,panelWidth,48),"menu_endpoint");
                GUI.SetNextControlName("menu_endpoint");
                address=GUI.TextField(new Rect(x,y+68,panelWidth,48),address,64,menuInput);
                MenuText(new Rect(x,y+124,panelWidth,25),L("Карта"),true);
                float halfW=(panelWidth-10)*.5f;
                if(!MenuDeveloperTools) useTestMap=false;
                if(MenuChoice(x,y+150,MenuDeveloperTools?halfW:panelWidth,L("Обычная карта"),!useTestMap)) useTestMap=false;
                if(MenuDeveloperTools && MenuChoice(x+halfW+10,y+150,halfW,L("Тестовая карта"),useTestMap)) useTestMap=true;
                if(!useTestMap)
                {
                    MenuText(new Rect(x,y+204,panelWidth,25),L("Номер карты · пусто — случайная"),true);
                    RegisterMenuControl(new Rect(x,y+230,panelWidth,48),"menu_seed");
                    GUI.SetNextControlName("menu_seed");
                    seedInput=GUI.TextField(new Rect(x,y+230,panelWidth,48),seedInput,12,menuInput);
                    DrawBotToggle(x,y+286,panelWidth);
                }
                else
                {
                    MenuText(new Rect(x,y+208,panelWidth,45),L("Галерея объектов, водоворот и спавны.\nЗона шторма и боты отключены."),true);
                }
                float actionY=!useTestMap?y+330:y+265;
                string actionText=!useTestMap && fillWithBots && observerSelected?L("Наблюдать за ботами"):L("Выйти в море");
                if(MenuAction(x,actionY,panelWidth,actionText,true))
                {
                    PlayerPrefs.SetString("LastEndpoint",address);
                    PlayerPrefs.Save();
                    Begin(true,address,useTestMap);
                }
                if(MenuAction(x,actionY+65,panelWidth,L("Назад"))) menuPage=0;
            }
            else if(menuPage==2)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("ПРИСОЕДИНИТЬСЯ"));
                MenuText(new Rect(x,y+48,panelWidth,25),L("Адрес IPv4:порт"),true);
                RegisterMenuControl(new Rect(x,y+78,panelWidth,52),"menu_endpoint");
                GUI.SetNextControlName("menu_endpoint");
                address=GUI.TextField(new Rect(x,y+78,panelWidth,52),address,64,menuInput);
                MenuText(new Rect(x,y+150,panelWidth,70),L("Введите адрес, который сообщил капитан вашей сессии."),true);
                if(MenuAction(x,y+285,panelWidth,L("Подключиться"),true))
                {
                    PlayerPrefs.SetString("LastEndpoint",address);
                    PlayerPrefs.Save();
                    Begin(false,address);
                }
                if(MenuAction(x,y+350,panelWidth,L("Назад"))) menuPage=0;
            }
            else if(menuPage==3)
            {
                var bank=Resources.Load<GameAudioBank>("GameAudioBank");
                MenuText(new Rect(x,y,panelWidth,32),L("ЗВУК"));
                if(bank!=null)
                {
                    bank.Master=MenuVolume(x,y+50,panelWidth,L("Общая громкость"),bank.Master,"AudioMaster");
                    bank.Music=MenuVolume(x,y+110,panelWidth,L("Музыка"),bank.Music,"AudioMusic");
                    bank.Effects=MenuVolume(x,y+170,panelWidth,L("Эффекты"),bank.Effects,"AudioEffects");
                    bank.Ambience=MenuVolume(x,y+230,panelWidth,L("Море и ветер"),bank.Ambience,"AudioAmbience");
                    bank.Interface=MenuVolume(x,y+290,panelWidth,L("Интерфейс"),bank.Interface,"AudioInterface");
                }
                if(MenuAction(x,y+350,panelWidth,L("Голосовой чат"))) menuPage=6;
                if(MenuAction(x,y+415,panelWidth,L("Назад"))) { PlayerPrefs.Save(); menuPage=7; }
            }
            else if(menuPage==6) DrawVoiceMenu(x,y,panelWidth);
            else if(menuPage==4)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("УПРАВЛЕНИЕ"));
                float sens = GameSettings.MouseSensitivity;
                float nextSens = MenuSlider(x, y+50, panelWidth, L("Чувствительность мыши")+"  "+sens.ToString("0.00"), sens / 5f) * 5f;
                if(!Mathf.Approximately(sens, nextSens)) GameSettings.MouseSensitivity = nextSens;
                bool invert = MenuToggle(x, y+115, panelWidth, GameSettings.InvertY, L("Инверсия мыши (ось Y)"));
                if(invert != GameSettings.InvertY) GameSettings.InvertY = invert;
                if(MenuAction(x,y+165,panelWidth,L("Назначение клавиш"))) { menuPage=13; listeningAction=-1; }
                string controls = L("ЛКМ — огонь / действие · ПКМ — отмена / еда\n1–6 — предметы\nShift + взаимодействие — снять пушку\nТруба: колесо — зум, нажатие — метка");
                if (MenuDeveloperTools) controls += "\n" + L("F1 — вид от третьего лица");
                controls += "\n" + Localization.F("{0} — скрыть / вернуть интерфейс", GameVersionOverlay.CaptureKey);
                MenuText(new Rect(x,y+228,panelWidth,130),controls,true);
                if(MenuAction(x,y+360,panelWidth,L("Назад"))) { PlayerPrefs.Save(); menuPage=7; }
            }
            else if(menuPage==7)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("НАСТРОЙКИ"));
                if(MenuAction(x,y+60,panelWidth,L("Графика"))) menuPage=8;
                if(MenuAction(x,y+128,panelWidth,L("Звук"))) menuPage=3;
                if(MenuAction(x,y+196,panelWidth,L("Управление"))) menuPage=4;
                if(MenuAction(x,y+264,panelWidth,L("Игра и Интерфейс"))) menuPage=9;
                if(MenuAction(x,y+328,panelWidth,L("Папка логов и ошибок"),secondary:true)) PirateSlop.ErrorLog.OpenFolder();
                if(MenuAction(x,y+390,panelWidth,L("Назад"))) menuPage=0;
            }
            else if(menuPage==8)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("ГРАФИКА"));
                bool isFullscreen = MenuToggle(x, y+60, panelWidth, Screen.fullScreen, L("Полноэкранный режим"));
                if(isFullscreen != Screen.fullScreen) { Screen.fullScreen = isFullscreen; PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0); }
                bool isVSync = MenuToggle(x, y+100, panelWidth, QualitySettings.vSyncCount > 0, L("Вертикальная синхронизация"));
                if (isVSync != (QualitySettings.vSyncCount > 0)) { QualitySettings.vSyncCount = isVSync ? 1 : 0; PlayerPrefs.SetInt("VSync", isVSync ? 1 : 0); }
                
                if(MenuAction(x, y+150, panelWidth, L("Разрешение: ") + Screen.width + "x" + Screen.height))
                {
                    int resIndex = -1;
                    var res = Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct()
                        .OrderByDescending(r => (long)r.x * r.y).ThenByDescending(r => r.x).ToArray();
                    if(res.Length > 0) {
                        for(int i=0; i<res.Length; i++) { if (res[i].x == Screen.width && res[i].y == Screen.height) { resIndex = i; break; } }
                        resIndex = (resIndex + 1) % res.Length;
                        Screen.SetResolution(res[resIndex].x, res[resIndex].y, Screen.fullScreenMode);
                    }
                }
                if(MenuAction(x, y+218, panelWidth, L("Качество: ") + QualitySettings.names[QualitySettings.GetQualityLevel()]))
                {
                    int q = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
                    QualitySettings.SetQualityLevel(q); PlayerPrefs.SetInt("QualityLevel", q);
                }
                bool optimizedClouds = MenuToggle(x, y+270, panelWidth, PirateSlop.Performance.PerformanceRenderOverrides.OptimizedClouds, L("Оптимизация облаков и зоны"));
                if (optimizedClouds != PirateSlop.Performance.PerformanceRenderOverrides.OptimizedClouds) PirateSlop.Performance.PerformanceRenderOverrides.SetOptimizedClouds(optimizedClouds);
                float fov = GameSettings.Fov;
                float nextFov = 60f + MenuSlider(x, y+315, panelWidth, L("Поле зрения")+"  "+Mathf.RoundToInt(fov)+"°", (fov-60f)/50f) * 50f;
                if(!Mathf.Approximately(fov, Mathf.Round(nextFov))) GameSettings.Fov = nextFov;
                float bright = GameSettings.Brightness;
                float nextBright = MenuSlider(x, y+375, panelWidth, L("Яркость")+"  "+(bright>=0?"+":"")+bright.ToString("0.00"), (bright+1f)*.5f) * 2f - 1f;
                if(!Mathf.Approximately(bright, nextBright)) GameSettings.Brightness = nextBright;
                if(MenuAction(x, y+435, panelWidth, L("Режим для дальтоников")+": "+GameSettings.ColorblindName(GameSettings.Colorblind), secondary:true))
                    GameSettings.Colorblind = (ColorblindMode)(((int)GameSettings.Colorblind + 1) % 4);
                if(MenuAction(x,y+485,panelWidth,L("Свет и изображение"))) menuPage=15;
                if(MenuAction(x,y+540,panelWidth,L("Назад"))) { PlayerPrefs.Save(); menuPage=7; }
            }
            else if(menuPage==9)
            {
                MenuText(new Rect(x,y,panelWidth,35),L("ИГРА И ИНТЕРФЕЙС"));
                bool hm = MenuToggle(x, y+60, panelWidth, GameSettings.Hitmarkers, L("Хитмаркеры"));
                if(hm != GameSettings.Hitmarkers) GameSettings.Hitmarkers = hm;
                bool di = MenuToggle(x, y+100, panelWidth, GameSettings.DamageIndicators, L("Индикаторы урона"));
                if(di != GameSettings.DamageIndicators) GameSettings.DamageIndicators = di;
                bool cp = MenuToggle(x, y+140, panelWidth, GameSettings.Compass, L("Компас"));
                if(cp != GameSettings.Compass) GameSettings.Compass = cp;
                bool cc = MenuToggle(x, y+180, panelWidth, GameSettings.Captions, L("Субтитры звуковых сигналов"));
                if(cc != GameSettings.Captions) GameSettings.Captions = cc;
                float languageY = y + 230;
                if (MenuDeveloperTools)
                {
                    bool visible = GameTelemetry.Visible;
                    bool telemetry = MenuToggle(x, y + 220, panelWidth, visible, L("Полная телеметрия"));
                    if (telemetry != visible) GameTelemetry.SetVisible(telemetry);
                    languageY += 40;
                }
                if(MenuAction(x,languageY,panelWidth,L("Язык")+": "+(Localization.Current==GameLanguage.Russian?L("Русский"):"English"),secondary:true))
                    Localization.Current = Localization.Current==GameLanguage.Russian?GameLanguage.English:GameLanguage.Russian;
                if(MenuAction(x,languageY+75,panelWidth,L("Назад"))) { PlayerPrefs.Save(); menuPage=7; }
            }
            else
            {
                if(playing)
                {
                    if(MenuAction(x,y,panelWidth,L("Вернуться на палубу"),true)) AdvancedPlayerController.SetCursor(true);
                    if(MenuAction(x,y+58,panelWidth,L("Покинуть сессию"))) Disconnect();
                    if(HostingSession && MenuAction(x,y+104,panelWidth,L("Игроки сессии"),secondary:true)) menuPage=12;
                    if(steamSession && party.MatchLobby.m_SteamID != 0 && MenuLink(new Rect(x,y-35,panelWidth,32),L("Копировать ID Steam-сессии: ")+party.MatchLobby)) GUIUtility.systemCopyBuffer=party.MatchLobby.ToString();
                }
                else
                {
                    if(MenuAction(x,y,panelWidth,L("ВЫЙТИ В МОРЕ"),true)) menuPage=party != null && party.InLobby ? 5 : 1;
                    if(MenuAction(x,y+54,panelWidth,L("Обучение · Первый выход"))) Begin(true,"127.0.0.1:"+Config.Port,tutorial:true);
                    if(MenuAction(x,y+104,panelWidth,L("Присоединиться к сессии"),secondary:true)) menuPage=party != null && party.InLobby ? 5 : 2;
                }
                if(MenuAction(x,y+146,panelWidth,L("Лобби · Steam"))) menuPage=5;
                bool isLeader = party == null || !party.InLobby || party.IsLeader;
                if(isLeader && MenuAction(x,y+198,panelWidth,L("Кастомизация"))) PirateSlop.Customization.SailCustomizationUI.Open();
                if(MenuAction(x,y+250,panelWidth,L("Настройки"))) menuPage=7;
                if(MenuAction(x,y+304,panelWidth,L("Как играть"))) menuPage=14;
                if(MenuDeveloperTools && MenuAction(x,y+358,panelWidth,L("Диагностика"))) menuPage=10;
                if(MenuAction(x,MenuDeveloperTools?y+410:y+358,panelWidth,L("Выйти из игры"))) Application.Quit();
                if(MenuDeveloperTools && (!playing || LoadTestActive))
                {
                    MenuRule(x+32,y+478,112);
                    GUI.Label(new Rect(x+32,y+491,panelWidth,20),!playing?L("ТЕСТИРОВАНИЕ"):L("ДЛЯ РАЗРАБОТЧИКА"),menuFooter);
                    if(!playing && MenuAction(x,y+515,panelWidth,L("Тестовая карта"),secondary:true)) Begin(true,"127.0.0.1:"+Config.Port,true);
                    if(!playing && MenuAction(x,y+557,panelWidth,L("Тест нагрузки · без AI"),secondary:true)) BeginLoadTest();
                    if(playing && LoadTestActive && MenuAction(x,y+515,panelWidth,L("Движение нагрузки · F7"),secondary:true)) ToggleLoadTestMotion();
                }
            }
            if(!string.IsNullOrEmpty(error))
            {
                var errorRect=new Rect(x+panelWidth+48,310,Mathf.Min(440,width-panelWidth-x-80),115);
                PirateHudStyle.Fill(errorRect,new Color(.028f,.065f,.078f,.9f));
                var previous=menuSmall.normal.textColor;
                menuSmall.normal.textColor=new Color(.91f,.76f,.64f);
                MenuText(new Rect(errorRect.x+12,errorRect.y+6,errorRect.width-24,errorRect.height-12),error,true);
                menuSmall.normal.textColor=previous;
            }
            if(playing) GUI.Label(new Rect(x,868,width-128,22),L("Игроки: ")+(population-botPopulation)+L(" · Боты: ")+botPopulation+L(" · Команды: ")+teamPopulation+L(" · Мест: ")+population+" / "+MaxPlayers,menuFooter);
            CompleteMenuNavigation();
            GUI.matrix=oldMatrix; GUI.color=oldColor;
        }
        bool pictureWorldTab;
        void DrawPictureMenu(float x, float y, float width)
        {
            MenuText(new Rect(x,y,width,35), L("Свет и изображение"));
            float half = (width - 10f) * .5f;
            DrawPresetChoice(x, y+48, half, PicturePreset.Scene, "Как в сцене");
            DrawPresetChoice(x+half+10, y+48, half, PicturePreset.SeaLight, "Морской свет");
            DrawPresetChoice(x, y+97, half, PicturePreset.Trailer, "Трейлер");
            DrawPresetChoice(x+half+10, y+97, half, PicturePreset.Custom, "Свой профиль");

            if (MenuChoice(x, y+146, half, L("Цвет и блики"), !pictureWorldTab)) pictureWorldTab = false;
            if (MenuChoice(x+half+10, y+146, half, L("Мир и камера"), pictureWorldTab)) pictureWorldTab = true;
            if (GameSettings.Preset != PicturePreset.Scene && !pictureWorldTab)
            {
                var look = GameSettings.Look;
                bool changed = DrawPictureSlider(x, y+206, width, "Контраст", ref look.contrast, -30, 30);
                changed |= DrawPictureSlider(x, y+268, width, "Насыщенность", ref look.saturation, -40, 40);
                changed |= DrawPictureSlider(x, y+330, width, "Холодные тени · тёплый свет", ref look.splitTone, 0, 100);
                changed |= DrawPictureSlider(x, y+392, width, "Свечение бликов", ref look.bloom, 0, 60);
                changed |= DrawPictureSlider(x, y+454, width, "Затемнение по краям", ref look.vignette, 0, 40);
                if (changed) GameSettings.SetCustomLook(look);
            }
            if (pictureWorldTab)
            {
                if (GameSettings.Preset != PicturePreset.Scene)
                {
                    var look = GameSettings.Look;
                    bool changed = DrawPictureSlider(x, y+206, width, "Солнечный свет и небо", ref look.sunlight, 0, 100);
                    changed |= DrawPictureSlider(x, y+268, width, "Отражения и пена", ref look.seaDetail, 0, 100);
                    if (changed) GameSettings.SetCustomLook(look);
                }
                int smoothing = GameSettings.ObserverSmoothing;
                if (DrawPictureSlider(x, y+330, width, "Плавность камеры наблюдателя", ref smoothing, 0, 100))
                    GameSettings.ObserverSmoothing = smoothing;
            }
            if (MenuAction(x, y+530, width, L("Назад"))) { PlayerPrefs.Save(); menuPage=8; }
        }

        void DrawPresetChoice(float x, float y, float width, PicturePreset preset, string label)
        {
            if (MenuChoice(x, y, width, L(label), GameSettings.Preset == preset) && GameSettings.Preset != preset)
                GameSettings.Preset = preset;
        }

        bool DrawPictureSlider(float x, float y, float width, string label, ref int value, int min, int max)
        {
            float position = Mathf.InverseLerp(min, max, value);
            float next = MenuSlider(x, y, width, L(label) + "  " + value, position);
            int result = Mathf.RoundToInt(Mathf.Lerp(min, max, next));
            if (result == value) return false;
            value = result;
            return true;
        }

        void DrawHowToPlay(float x, float y, float width)
        {
            var use = KeyBindings.Label(GameAction.Use);
            MenuText(new Rect(x, y, width, 32), L("Как играть"));
            DrawHelpStep(x, y + 38, width, L("Дайте ход"),
                L("Удерживайте ЛКМ за рукоять шпиля и идите по кругу, чтобы поднять якорь. Возьмите канат ЛКМ и потяните мышь вниз. Поворачивайте штурвал ЛКМ и движением мыши."));
            DrawHelpStep(x, y + 140, width, L("Зарядите и выстрелите"),
                Localization.F("Рычаг выдаёт ядро, если потянуть его ЛКМ вниз. Возьмите ядро ЛКМ и поднесите к дулу. Нажмите {0} у пушки, чтобы прицелиться, затем ЛКМ для выстрела.", use));
            DrawHelpStep(x, y + 242, width, L("Закройте пробоину"),
                L("Выберите доски в инвентаре. Наведитесь на подсвеченную пробоину и удерживайте ЛКМ до установки доски."));
            DrawHelpStep(x, y + 324, width, L("Откачайте воду"),
                Localization.F("Спуститесь в трюм. Наведитесь на насос и удерживайте {0}. Процент затопления виден у насоса.", use));
            MenuText(new Rect(x, y + 412, width, 32), Localization.F("Карта океана — {0}", KeyBindings.Label(GameAction.Map)), true);
            if (MenuAction(x, y + 450, width, L("Назад"))) menuPage = 0;
        }
        void DrawHelpStep(float x, float y, float width, string title, string description)
        {
            MenuText(new Rect(x, y, width, 24), title);
            MenuText(new Rect(x, y + 25, width, 74), description, true);
        }
        void CompleteMenuNavigation()
        {
            if(Event.current.type!=EventType.Repaint) return;
            menuPreviousControls.Clear();
            menuPreviousControls.AddRange(menuControls);
            if(!menuControls.Contains(menuFocus)) menuFocus=menuControls.Count>0?menuControls[0]:null;
            menuSubmit=null;
        }
        void DrawBotToggle(float x, float y, float width)
        {
            if (!MenuDeveloperTools) { fillWithBots = false; observerSelected = false; chooseObserver = false; return; }
            bool previous = fillWithBots;
            fillWithBots = MenuToggle(x,y,width,fillWithBots,L("Заполнить свободные места ботами"));
            if (fillWithBots && !previous) chooseObserver = true;
            if (!fillWithBots) { observerSelected = false; chooseObserver = false; }
        }
        bool MenuToggle(float x,float y,float width,bool value,string label)
        {
            var rect=new Rect(x,y,width,35);
            string control=RegisterMenuControl(rect);
            bool next=value;
            if(MenuControl(rect,control))
            {
                next=!value;
                GameAudio.Play(SoundCue.Select,Vector3.zero,.6f,true);
            }
            PirateHudStyle.Fill(new Rect(x,y+6,22,22),GUI.enabled?menuGold:PirateHudStyle.Muted);
            PirateHudStyle.Fill(new Rect(x+2,y+8,18,18),new Color(.025f,.07f,.085f));
            if(next)
            {
                MenuCheckStroke(new Vector2(x+5,y+17),new Vector2(x+10,y+22));
                MenuCheckStroke(new Vector2(x+10,y+22),new Vector2(x+18,y+12));
            }
            MenuText(new Rect(x+30,y,width-30,35),label,true);
            return next;
        }
        bool MenuChoice(float x,float y,float width,string label,bool selected)
        {
            var rect=new Rect(x,y,width,46);
            string control=RegisterMenuControl(rect);
            bool hover=GUI.enabled && menuFocus==control;
            Color accent=selected?menuGold:hover?menuIvory:menuMuted;
            PirateHudStyle.Brush(new Rect(x+28,y+44,Mathf.Min(width-36,60),1.5f),accent,true);
            MenuDiamond(new Vector2(x+12,y+23),selected?5:3,accent);
            menuButton.fontSize=width<200?17:19;
            menuButton.fontStyle=FontStyle.Normal;
            menuButton.normal.textColor=!GUI.enabled?menuMuted:selected?menuGold:menuIvory;
            MenuInk(new Rect(x+28,y,width-36,46),label,menuButton);
            bool clicked=MenuControl(rect,control);
            if(clicked && !selected) GameAudio.Play(SoundCue.Select,Vector3.zero,.6f,true);
            return clicked;
        }
        void MenuCheckStroke(Vector2 from,Vector2 to)
        {
            int steps=Mathf.CeilToInt(Vector2.Distance(from,to));
            for(int i=0;i<=steps;i++)
            {
                var point=Vector2.Lerp(from,to,i/(float)Mathf.Max(1,steps));
                PirateHudStyle.Fill(new Rect(point.x-1.5f,point.y-1.5f,3,3),GUI.enabled?PirateHudStyle.Paper:PirateHudStyle.Muted);
            }
        }
        float nextSliderAudio;
        static string L(string text) => Localization.T(text);
        int listeningAction = -1;
        float MenuSlider(float x,float y,float width,string label,float value)
        {
            MenuText(new Rect(x,y,width,28),label,true);
            PirateHudStyle.Brush(new Rect(x,y+42,width*Mathf.Clamp01(value),3),menuGold,true);
            float next=GUI.HorizontalSlider(new Rect(x,y+35,width,22),value,0f,1f,menuSlider,menuThumb);
            if(!Mathf.Approximately(value,next) && Time.unscaledTime >= nextSliderAudio) { GameAudio.Play(SoundCue.UISlider, Vector3.zero, .35f, true); nextSliderAudio = Time.unscaledTime + .1f; }
            return next;
        }
        void DrawBindingsMenu(float x,float y,float width)
        {
            MenuText(new Rect(x,y,width,35),L("НАЗНАЧЕНИЕ КЛАВИШ"));
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(listeningAction>=0 && keyboard!=null)
            {
                if(keyboard.escapeKey.wasPressedThisFrame) listeningAction=-1;
                else foreach(var control in keyboard.allKeys)
                    if(control!=null && control.wasPressedThisFrame && control.keyCode!=UnityEngine.InputSystem.Key.Escape)
                    { KeyBindings.Set((GameAction)listeningAction,control.keyCode); listeningAction=-1; break; }
            }
            for(int i=0;i<KeyBindings.Count;i++)
            {
                var action=(GameAction)i;
                float row=y+45+i*30;
                MenuText(new Rect(x,row,width-150,28),KeyBindings.Name(action),true);
                string value=listeningAction==i?L("Нажмите клавишу…"):"[ "+KeyBindings.Label(action)+" ]";
                if(MenuLink(new Rect(x+width-150,row,150,28),value)) listeningAction=i;
            }
            if(MenuAction(x,y+540,width,L("Сбросить по умолчанию"),secondary:true)) { KeyBindings.Reset(); listeningAction=-1; }
            if(MenuAction(x,y+590,width,L("Назад"))) { listeningAction=-1; menuPage=4; }
        }
        float MenuVolume(float x,float y,float width,string label,float value,string key)
        {
            MenuText(new Rect(x,y,width,28),label+"  "+Mathf.RoundToInt(value*100)+"%",true);
            PirateHudStyle.Brush(new Rect(x,y+42,width*value,3),menuGold,true);
            float next=GUI.HorizontalSlider(new Rect(x,y+35,width,22),value,0f,1f,menuSlider,menuThumb);
            if(!Mathf.Approximately(value,next))
            {
                PlayerPrefs.SetFloat(key,next);
                if (Time.unscaledTime >= nextSliderAudio) { GameAudio.Play(SoundCue.UISlider, Vector3.zero, .35f, true); nextSliderAudio = Time.unscaledTime + .1f; }
            }
            return next;
        }
        void ReleaseMenu()
        {
            foreach(var texture in new[]{menuShade,menuField,menuFieldFocus,menuThumbTexture}) if(texture!=null) Destroy(texture);
            if(menuDisplayFont!=null) Destroy(menuDisplayFont);
            if(menuBodyFont!=null) Destroy(menuBodyFont);
            menuTitle=menuLabel=menuSmall=menuButton=menuInput=menuSlider=menuThumb=menuFooter=null;
            menuTitlePrint=null;
            menuAccents.Clear();
            menuControls.Clear();
            menuPreviousControls.Clear();
            menuFocus=menuSubmit=null;
            menuWasVisible=false;
        }
    }
}
