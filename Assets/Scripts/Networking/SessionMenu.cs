using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        int menuPage;
        bool useTestMap;
        GUIStyle menuTitle, menuLabel, menuSmall, menuButton, menuInput, menuSlider, menuThumb;
        Texture2D menuShade, menuField, menuFieldFocus, menuThumbTexture;
        Font menuDisplayFont, menuBodyFont;
        readonly Color menuGold = new(.92f,.74f,.42f);
        void PrepareMenu()
        {
            if(menuTitle!=null) return;
            var sc = GameObject.Find("SettingsCanvas");
            if (sc != null) Destroy(sc);
            menuDisplayFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman"},64);
            menuBodyFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman","Segoe UI"},22);
            menuTitle=new GUIStyle(GUI.skin.label){font=menuDisplayFont,fontSize=56,fontStyle=FontStyle.Bold,normal={textColor=new Color(.98f,.96f,.91f)}};
            menuLabel=new GUIStyle(GUI.skin.label){font=menuBodyFont,fontSize=20,wordWrap=true,normal={textColor=new Color(.88f,.88f,.85f)}};
            menuSmall=new GUIStyle(menuLabel){fontSize=14,normal={textColor=new Color(.85f,.72f,.52f,.85f)}};
            menuButton=new GUIStyle(menuLabel){font=menuDisplayFont,fontSize=22,fontStyle=FontStyle.Normal,alignment=TextAnchor.MiddleLeft,wordWrap=false};
            menuField=MenuTexture(new Color(.04f,.035f,.03f,.75f));
            menuFieldFocus=MenuTexture(new Color(.08f,.065f,.05f,.9f));
            menuThumbTexture=MenuTexture(menuGold);
            menuInput=new GUIStyle(GUI.skin.textField){font=menuBodyFont,fontSize=22,padding=new RectOffset(15,15,10,10),border=new RectOffset(),normal={background=menuField,textColor=PirateHudStyle.Paper},hover={background=menuFieldFocus,textColor=PirateHudStyle.Paper},focused={background=menuFieldFocus,textColor=PirateHudStyle.Paper},active={background=menuFieldFocus,textColor=PirateHudStyle.Paper}};
            menuSlider=new GUIStyle(){fixedHeight=5,margin=new RectOffset(0,0,8,0),normal={background=menuFieldFocus}};
            menuThumb=new GUIStyle(){fixedWidth=12,fixedHeight=21,normal={background=menuThumbTexture},hover={background=menuThumbTexture},active={background=menuThumbTexture}};
            menuShade=new Texture2D(128,1,TextureFormat.RGBA32,false); menuShade.wrapMode=TextureWrapMode.Clamp;
            for(int x=0;x<128;x++)
            {
                float t=x/127f;
                float a=Mathf.Lerp(.52f,0f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.02f,.85f,t)));
                menuShade.SetPixel(x,0,new Color(.035f,.025f,.02f,a));
            }
            menuShade.Apply();
            if(!connecting && !playing) address=PlayerPrefs.GetString("LastEndpoint","127.0.0.1:"+Config.Port);
        }
        void MenuText(Rect rect,string text,bool small=false) => GUI.Label(rect,text,small?menuSmall:menuLabel);
        bool MenuLink(Rect rect,string text)
        {
            bool hover=GUI.enabled && rect.Contains(Event.current.mousePosition);
            var previous=menuSmall.normal.textColor;
            menuSmall.normal.textColor=hover?PirateHudStyle.Paper:menuGold;
            GUI.Label(rect,text,menuSmall);
            menuSmall.normal.textColor=previous;
            if(hover) PirateHudStyle.Brush(new Rect(rect.x,rect.yMax-2,rect.width,2),menuGold,true);
            return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }
        Texture2D MenuTexture(Color color)
        {
            var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixel(0,0,color); texture.Apply(); return texture;
        }
        void MenuVerticalRail(float x,float startY,float height)
        {
            Color railColor=new(menuGold.r,menuGold.g,menuGold.b,.35f);
            PirateHudStyle.Fill(new Rect(x,startY,1.5f,height),railColor);
            PirateHudStyle.Diamond(new Vector2(x+.75f,startY),4,menuGold);
            PirateHudStyle.Diamond(new Vector2(x+.75f,startY+height),4,menuGold);
        }
        void MenuRule(float x,float y,float width)
        {
            PirateHudStyle.Brush(new Rect(x,y,width,1.5f),new Color(menuGold.r,menuGold.g,menuGold.b,.4f),true);
        }
        bool MenuAction(float x,float y,float width,string label,bool primary=false)
        {
            var rect=new Rect(x,y,width,48);
            string control="menu_"+x+"_"+y+"_"+label;
            bool hover=GUI.enabled && (rect.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl()==control);
            float shift=hover?8f:0f;
            if(hover)
            {
                PirateHudStyle.Fill(new Rect(x+18,y+2,width-18,44),new Color(menuGold.r,menuGold.g,menuGold.b,.08f));
                PirateHudStyle.Diamond(new Vector2(x+12,y+24),5,menuGold);
                PirateHudStyle.Brush(new Rect(x+32+shift,y+40,Mathf.Min(width-48,label.Length*13f),1.5f),new Color(menuGold.r,menuGold.g,menuGold.b,.5f),true);
            }
            else if(primary)
            {
                PirateHudStyle.Diamond(new Vector2(x+12,y+24),3,new Color(menuGold.r,menuGold.g,menuGold.b,.45f));
            }
            menuButton.fontSize=width<180?18:22;
            menuButton.normal.textColor=GUI.enabled?(hover?new Color(1f,.98f,.88f):primary?menuGold:new Color(.82f,.84f,.85f,.9f)):PirateHudStyle.Muted;
            GUI.Label(new Rect(x+32+shift,y,width-36,48),label,menuButton);
            GUI.SetNextControlName(control);
            bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);
            if(clicked) GameAudio.Play(SoundCue.Select,Vector3.zero,1f,true);
            return clicked;
        }
        void DrawSessionMenu()
        {
            if(BotDebugPanel.ConsumedInput || DeveloperMenu.IsOpen || dedicated || Automated || !MenuOpen) return;
            PrepareMenu();
            var oldMatrix=GUI.matrix; var oldColor=GUI.color;
            GUI.color=Color.white; GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuShade);
            float scale=Mathf.Min(Screen.height/900f,Screen.width/1000f), width=Screen.width/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float x=64, panelWidth=450;
            MenuText(new Rect(x,45,panelWidth,25),"М О Р Е   /   П О Р О Х   /   С В О Б О Д А",true);
            GUI.Label(new Rect(x-4,75,610,80),"PIRATE SLOP",menuTitle);
            MenuText(new Rect(x,165,panelWidth,35),playing ? "Ваше приключение продолжается" : "Открытый океан · Подними паруса",true);
            MenuVerticalRail(x+12,220,500);
            float y=225;
            if(connecting)
            {
                MenuText(new Rect(x,y,panelWidth,70),status);
                float progress=ProceduralWorld.Instance!=null ? ProceduralWorld.Instance.Progress : 0f;
                GUI.color=new Color(.1f,.2f,.22f); GUI.DrawTexture(new Rect(x,y+95,panelWidth,5),Texture2D.whiteTexture);
                GUI.color=menuGold; GUI.DrawTexture(new Rect(x,y+95,panelWidth*Mathf.Clamp01(progress),5),Texture2D.whiteTexture); GUI.color=Color.white;
                MenuText(new Rect(x,y+115,panelWidth,30),"Подготовка моря и островов…",true);
                if(MenuAction(x,y+180,panelWidth,"Отменить подключение")) Disconnect();
            }
            else if(chooseObserver)
            {
                MenuText(new Rect(x,y,panelWidth,50),"СВОБОДНАЯ КАМЕРА?");
                MenuText(new Rect(x,y+60,panelWidth,100),"На карте будут только боты. Ваш корабль не появится.\nWASD и мышь — полёт, Q / E — высота.",true);
                if(MenuAction(x,y+180,panelWidth,"Да · наблюдать за ботами",true)) { observerSelected=true; chooseObserver=false; }
                if(MenuAction(x,y+250,panelWidth,"Нет · играть на своём корабле")) { observerSelected=false; chooseObserver=false; }
            }
            else if(menuPage==5) { DrawSteamParty(x,y-42,panelWidth); }
            else if(menuPage==1)
            {
                MenuText(new Rect(x,y,panelWidth,35),"СОЗДАТЬ ЭКСПЕДИЦИЮ");
                MenuText(new Rect(x,y+42,panelWidth,25),"Адрес IPv4:порт",true);
                address=GUI.TextField(new Rect(x,y+68,panelWidth,48),address,64,menuInput);
                MenuText(new Rect(x,y+124,panelWidth,25),"Карта",true);
                float halfW=(panelWidth-10)*.5f;
                if(MenuChoice(x,y+150,halfW,"Обычная карта",!useTestMap)) useTestMap=false;
                if(MenuChoice(x+halfW+10,y+150,halfW,"Тестовая карта",useTestMap)) useTestMap=true;
                if(!useTestMap)
                {
                    MenuText(new Rect(x,y+204,panelWidth,25),"Номер карты · пусто — случайная",true);
                    seedInput=GUI.TextField(new Rect(x,y+230,panelWidth,48),seedInput,12,menuInput);
                    DrawBotToggle(x,y+286,panelWidth);
                }
                else
                {
                    MenuText(new Rect(x,y+208,panelWidth,45),"Галерея объектов, водоворот и спавны.\nЗона шторма и боты отключены.",true);
                }
                float actionY=!useTestMap?y+330:y+265;
                string actionText=!useTestMap && fillWithBots && observerSelected?"Наблюдать за ботами":"Выйти в море";
                if(MenuAction(x,actionY,panelWidth,actionText,true))
                {
                    PlayerPrefs.SetString("LastEndpoint",address);
                    PlayerPrefs.Save();
                    Begin(true,address,useTestMap);
                }
                if(MenuAction(x,actionY+65,panelWidth,"Назад")) menuPage=0;
            }
            else if(menuPage==2)
            {
                MenuText(new Rect(x,y,panelWidth,35),"ПРИСОЕДИНИТЬСЯ");
                MenuText(new Rect(x,y+48,panelWidth,25),"Адрес IPv4:порт",true);
                address=GUI.TextField(new Rect(x,y+78,panelWidth,52),address,64,menuInput);
                MenuText(new Rect(x,y+150,panelWidth,70),"Введите адрес, который сообщил капитан вашей сессии.",true);
                if(MenuAction(x,y+285,panelWidth,"Подключиться",true))
                {
                    PlayerPrefs.SetString("LastEndpoint",address);
                    PlayerPrefs.Save();
                    Begin(false,address);
                }
                if(MenuAction(x,y+350,panelWidth,"Назад")) menuPage=0;
            }
            else if(menuPage==3)
            {
                var bank=Resources.Load<GameAudioBank>("GameAudioBank");
                MenuText(new Rect(x,y,panelWidth,32),"ЗВУК");
                if(bank!=null)
                {
                    bank.Master=MenuVolume(x,y+50,panelWidth,"Общая громкость",bank.Master,"AudioMaster");
                    bank.Music=MenuVolume(x,y+110,panelWidth,"Музыка",bank.Music,"AudioMusic");
                    bank.Effects=MenuVolume(x,y+170,panelWidth,"Эффекты",bank.Effects,"AudioEffects");
                    bank.Ambience=MenuVolume(x,y+230,panelWidth,"Море и ветер",bank.Ambience,"AudioAmbience");
                    bank.Interface=MenuVolume(x,y+290,panelWidth,"Интерфейс",bank.Interface,"AudioInterface");
                }
                if(MenuAction(x,y+350,panelWidth,"Голосовой чат")) menuPage=6;
                if(MenuAction(x,y+415,panelWidth,"Назад")) { PlayerPrefs.Save(); menuPage=7; }
            }
            else if(menuPage==6) DrawVoiceMenu(x,y,panelWidth);
            else if(menuPage==4)
            {
                MenuText(new Rect(x,y,panelWidth,35),"УПРАВЛЕНИЕ");
                MenuText(new Rect(x,y+55,panelWidth,280),"WASD — движение · E — взаимодействие\nShift + E — снять пушку\nЛКМ — огонь / действие\nПКМ — отмена / еда\n1–6 — предметы · G — бросить\nF1 — вид от третьего лица\nТруба: колесо — зум, нажатие — метка\nПушка: мышь — наведение\nE / Esc — выйти из прицела");
                
                float sens = PlayerPrefs.GetFloat("MouseSensitivity", 1.0f);
                float nextSens = MenuVolume(x, y+270, panelWidth, "Чувствительность мыши", sens / 5f, "MouseSens_UI") * 5f;
                if(!Mathf.Approximately(sens, nextSens)) PlayerPrefs.SetFloat("MouseSensitivity", nextSens);
                
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=7;
            }
            else if(menuPage==7)
            {
                MenuText(new Rect(x,y,panelWidth,35),"НАСТРОЙКИ");
                if(MenuAction(x,y+60,panelWidth,"Графика")) menuPage=8;
                if(MenuAction(x,y+128,panelWidth,"Звук")) menuPage=3;
                if(MenuAction(x,y+196,panelWidth,"Управление")) menuPage=4;
                if(MenuAction(x,y+264,panelWidth,"Игра и Интерфейс")) menuPage=9;
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=0;
            }
            else if(menuPage==8)
            {
                MenuText(new Rect(x,y,panelWidth,35),"ГРАФИКА");
                bool isFullscreen = MenuToggle(x, y+60, panelWidth, Screen.fullScreen, "Полноэкранный режим");
                if(isFullscreen != Screen.fullScreen) { Screen.fullScreen = isFullscreen; PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0); }
                bool isVSync = MenuToggle(x, y+100, panelWidth, QualitySettings.vSyncCount > 0, "Вертикальная синхронизация");
                if (isVSync != (QualitySettings.vSyncCount > 0)) { QualitySettings.vSyncCount = isVSync ? 1 : 0; PlayerPrefs.SetInt("VSync", isVSync ? 1 : 0); }
                
                if(MenuAction(x, y+150, panelWidth, "Разрешение: " + Screen.currentResolution.width + "x" + Screen.currentResolution.height))
                {
                    int resIndex = -1; var res = Screen.resolutions;
                    if(res.Length > 0) {
                        for(int i=0; i<res.Length; i++) { if (res[i].width == Screen.currentResolution.width && res[i].height == Screen.currentResolution.height) { resIndex = i; break; } }
                        resIndex = (resIndex + 1) % res.Length;
                        Screen.SetResolution(res[resIndex].width, res[resIndex].height, Screen.fullScreen);
                    }
                }
                if(MenuAction(x, y+218, panelWidth, "Качество: " + QualitySettings.names[QualitySettings.GetQualityLevel()]))
                {
                    int q = (QualitySettings.GetQualityLevel() + 1) % QualitySettings.names.Length;
                    QualitySettings.SetQualityLevel(q); PlayerPrefs.SetInt("QualityLevel", q);
                }
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=7;
            }
            else if(menuPage==9)
            {
                MenuText(new Rect(x,y,panelWidth,35),"ИГРА И ИНТЕРФЕЙС");
                bool hm = MenuToggle(x, y+60, panelWidth, PlayerPrefs.GetInt("ShowHitmarkers", 1) == 1, "Хитмаркеры");
                PlayerPrefs.SetInt("ShowHitmarkers", hm ? 1 : 0);
                bool di = MenuToggle(x, y+100, panelWidth, PlayerPrefs.GetInt("ShowDamageIndicators", 1) == 1, "Индикаторы урона");
                PlayerPrefs.SetInt("ShowDamageIndicators", di ? 1 : 0);
                bool kf = MenuToggle(x, y+140, panelWidth, PlayerPrefs.GetInt("ShowKillFeed", 1) == 1, "Журнал убийств");
                PlayerPrefs.SetInt("ShowKillFeed", kf ? 1 : 0);
                bool cp = MenuToggle(x, y+180, panelWidth, PlayerPrefs.GetInt("ShowCompass", 1) == 1, "Компас");
                PlayerPrefs.SetInt("ShowCompass", cp ? 1 : 0);
                bool iy = MenuToggle(x, y+220, panelWidth, PlayerPrefs.GetInt("InvertY", 0) == 1, "Инверсия мыши (ось Y)");
                PlayerPrefs.SetInt("InvertY", iy ? 1 : 0);
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=7;
            }
            else
            {
                if(playing)
                {
                    if(MenuAction(x,y,panelWidth,"Вернуться на палубу",true)) AdvancedPlayerController.SetCursor(true);
                    if(MenuAction(x,y+58,panelWidth,"Покинуть сессию")) Disconnect();
                    if(steamSession && party.MatchLobby.m_SteamID != 0 && MenuLink(new Rect(x,y-35,panelWidth,32),"Копировать ID Steam-сессии: "+party.MatchLobby)) GUIUtility.systemCopyBuffer=party.MatchLobby.ToString();
                }
                else
                {
                    if(MenuAction(x,y,panelWidth,"Выйти в море",true)) menuPage=party != null && party.InLobby ? 5 : 1;
                    if(MenuAction(x,y+58,panelWidth,"Присоединиться к сессии")) menuPage=party != null && party.InLobby ? 5 : 2;
                }
                if(MenuAction(x,y+116,panelWidth,"Команда · Steam")) menuPage=5;
                if(MenuAction(x,y+174,panelWidth,"Настройки")) menuPage=7;
                if(MenuAction(x,y+232,panelWidth,"Выйти из игры")) Application.Quit();
                if(!playing && MenuAction(x,y+290,panelWidth,"Тестовая карта")) Begin(true,"127.0.0.1:"+Config.Port,true);
            }
            if(!string.IsNullOrEmpty(error))
            {
                var errorRect=new Rect(x+panelWidth+48,310,Mathf.Min(440,width-panelWidth-x-80),115);
                PirateHudStyle.Fill(errorRect,new Color(.09f,.035f,.025f,.95f));
                var previous=menuSmall.normal.textColor;
                menuSmall.normal.textColor=new Color(1f,.76f,.59f);
                MenuText(new Rect(errorRect.x+16,errorRect.y+12,errorRect.width-32,errorRect.height-24),error,true);
                menuSmall.normal.textColor=previous;
            }
            MenuText(new Rect(x,856,width-128,25),playing ? "Игроки: "+(population-botPopulation)+" · Боты: "+botPopulation+" · Команды: "+teamPopulation+" · Мест: "+population+" / "+MaxPlayers : "СОБЕРИ КОМАНДУ  /  ОТКРОЙ НЕИЗВЕСТНЫЕ БЕРЕГА",true);
            GUI.matrix=oldMatrix; GUI.color=oldColor;
        }
        void DrawBotToggle(float x, float y, float width)
        {
            bool previous = fillWithBots;
            fillWithBots = MenuToggle(x,y,width,fillWithBots,"Заполнить свободные места ботами");
            if (fillWithBots && !previous) chooseObserver = true;
            if (!fillWithBots) { observerSelected = false; chooseObserver = false; }
        }
        bool MenuToggle(float x,float y,float width,bool value,string label)
        {
            bool next=value;
            if(GUI.Button(new Rect(x,y,width,35),GUIContent.none,GUIStyle.none))
            {
                next=!value;
                GameAudio.Play(SoundCue.Select,Vector3.zero,1f,true);
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
            bool hover=GUI.enabled && rect.Contains(Event.current.mousePosition);
            Color ink=selected?new Color(.08f,.20f,.22f,.98f):hover?new Color(.05f,.12f,.14f,.92f):new Color(.025f,.06f,.075f,.85f);
            PirateHudStyle.Fill(new Rect(x+4,y+2,width-8,42),ink);
            Color accent=selected?menuGold:hover?new Color(menuGold.r,menuGold.g,menuGold.b,.7f):new Color(.35f,.45f,.45f,.6f);
            PirateHudStyle.Brush(new Rect(x,y+44,width,2),accent,true);
            if(selected) PirateHudStyle.Brush(new Rect(x,y,width,2),accent,true);
            PirateHudStyle.Diamond(new Vector2(x+16,y+23),selected?7:4,accent);
            menuButton.fontSize=width<200?17:19;
            menuButton.normal.textColor=selected?PirateHudStyle.Paper:hover?new Color(.85f,.88f,.85f):new Color(.6f,.7f,.7f);
            GUI.Label(new Rect(x+28,y,width-36,46),label,menuButton);
            bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);
            if(clicked && !selected) GameAudio.Play(SoundCue.Select,Vector3.zero,1f,true);
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
        float MenuVolume(float x,float y,float width,string label,float value,string key)
        {
            MenuText(new Rect(x,y,width,28),label+"  "+Mathf.RoundToInt(value*100)+"%",true);
            PirateHudStyle.Brush(new Rect(x,y+42,width*value,3),menuGold,true);
            float next=GUI.HorizontalSlider(new Rect(x,y+35,width,22),value,0f,1f,menuSlider,menuThumb);
            if(!Mathf.Approximately(value,next)) PlayerPrefs.SetFloat(key,next);
            return next;
        }
        void ReleaseMenu()
        {
            foreach(var texture in new[]{menuShade,menuField,menuFieldFocus,menuThumbTexture}) if(texture!=null) Destroy(texture);
            if(menuDisplayFont!=null) Destroy(menuDisplayFont);
            if(menuBodyFont!=null) Destroy(menuBodyFont);
        }
    }
}
