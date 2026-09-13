using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        int menuPage;
        GUIStyle menuTitle, menuLabel, menuSmall, menuButton, menuInput, menuSlider, menuThumb;
        Texture2D menuShade, menuField, menuFieldFocus, menuThumbTexture;
        Font menuDisplayFont, menuBodyFont;
        readonly Color menuGold = new(.83f,.68f,.39f);
        void PrepareMenu()
        {
            if(menuTitle!=null) return;
            menuDisplayFont=Font.CreateDynamicFontFromOSFont(new[]{"Georgia","Times New Roman"},64);
            menuBodyFont=Font.CreateDynamicFontFromOSFont(new[]{"Segoe UI","Arial"},22);
            menuTitle=new GUIStyle(GUI.skin.label){font=menuDisplayFont,fontSize=64,fontStyle=FontStyle.Bold,normal={textColor=PirateHudStyle.Paper}};
            menuLabel=new GUIStyle(GUI.skin.label){font=menuBodyFont,fontSize=21,wordWrap=true,normal={textColor=PirateHudStyle.Paper}};
            menuSmall=new GUIStyle(menuLabel){fontSize=16,normal={textColor=new Color(.73f,.81f,.8f)}};
            menuButton=new GUIStyle(menuLabel){fontSize=20,alignment=TextAnchor.MiddleLeft,wordWrap=false};
            menuField=MenuTexture(new Color(.025f,.07f,.085f,.96f));
            menuFieldFocus=MenuTexture(new Color(.065f,.145f,.16f,.98f));
            menuThumbTexture=MenuTexture(menuGold);
            menuInput=new GUIStyle(GUI.skin.textField){font=menuBodyFont,fontSize=22,padding=new RectOffset(15,15,10,10),border=new RectOffset(),normal={background=menuField,textColor=PirateHudStyle.Paper},hover={background=menuFieldFocus,textColor=PirateHudStyle.Paper},focused={background=menuFieldFocus,textColor=PirateHudStyle.Paper},active={background=menuFieldFocus,textColor=PirateHudStyle.Paper}};
            menuSlider=new GUIStyle(){fixedHeight=5,margin=new RectOffset(0,0,8,0),normal={background=menuFieldFocus}};
            menuThumb=new GUIStyle(){fixedWidth=12,fixedHeight=21,normal={background=menuThumbTexture},hover={background=menuThumbTexture},active={background=menuThumbTexture}};
            menuShade=new Texture2D(128,1,TextureFormat.RGBA32,false); menuShade.wrapMode=TextureWrapMode.Clamp;
            for(int x=0;x<128;x++) menuShade.SetPixel(x,0,new Color(.012f,.032f,.043f,Mathf.Lerp(.97f,.03f,Mathf.SmoothStep(0f,1f,Mathf.InverseLerp(.13f,.8f,x/127f)))));
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
        void MenuRule(float x,float y,float width)
        {
            PirateHudStyle.Brush(new Rect(x,y,width,3),menuGold,true);
            for(int i=0;i<=24;i++)
            {
                float height=i%6==0?10:i%2==0?6:3;
                PirateHudStyle.Fill(new Rect(x+i*width/24,y-height,1,height),new Color(menuGold.r,menuGold.g,menuGold.b,.55f));
            }
            PirateHudStyle.Diamond(new Vector2(x+width*.5f,y),7,menuGold);
        }
        bool MenuAction(float x,float y,float width,string label,bool primary=false)
        {
            var rect=new Rect(x,y,width,54);
            string control="menu_"+x+"_"+y+"_"+label;
            bool hover=GUI.enabled && (rect.Contains(Event.current.mousePosition) || GUI.GetNameOfFocusedControl()==control);
            Color ink=hover?new Color(.1f,.23f,.25f,.97f):primary?new Color(.06f,.16f,.18f,.95f):new Color(.025f,.075f,.09f,.88f);
            PirateHudStyle.Fill(new Rect(x+8,y+4,width-16,46),ink);
            PirateHudStyle.Brush(new Rect(x-10,y-8,width+20,70),ink);
            Color accent=GUI.enabled?menuGold:PirateHudStyle.Muted;
            PirateHudStyle.Brush(new Rect(x,y+52,width,2),accent,true);
            if(primary || hover) PirateHudStyle.Brush(new Rect(x,y,width,2),accent,true);
            PirateHudStyle.Diamond(new Vector2(x+17,y+27),hover?9:6,accent);
            menuButton.fontSize=width<180?17:20;
            menuButton.normal.textColor=GUI.enabled?(primary || hover?PirateHudStyle.Paper:new Color(.83f,.86f,.81f)):PirateHudStyle.Muted;
            GUI.Label(new Rect(x+34,y,width-46,54),label,menuButton);
            GUI.SetNextControlName(control);
            bool clicked=GUI.Button(rect,GUIContent.none,GUIStyle.none);
            if(clicked) GameAudio.Play(SoundCue.Select,Vector3.zero,1f,true);
            return clicked;
        }
        void DrawSessionMenu()
        {
            if(DeveloperMenu.IsOpen || dedicated || Automated || !MenuOpen) return;
            PrepareMenu();
            var oldMatrix=GUI.matrix; var oldColor=GUI.color;
            GUI.color=Color.white; GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),menuShade);
            float scale=Mathf.Min(Screen.height/900f,Screen.width/1000f), width=Screen.width/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            float x=64, panelWidth=450;
            MenuText(new Rect(x,55,panelWidth,30),"М О Р Е   /   П О Р О Х   /   С В О Б О Д А",true);
            GUI.Label(new Rect(x-4,103,610,95),"PIRATE SLOP",menuTitle);
            MenuRule(x,213,panelWidth);
            MenuText(new Rect(x,232,panelWidth,45),playing ? "Ваше приключение продолжается" : "Подними паруса. Найди свою добычу.");
            float y=310;
            if(connecting)
            {
                MenuText(new Rect(x,y,panelWidth,70),status);
                float progress=ProceduralWorld.Instance!=null ? ProceduralWorld.Instance.Progress : 0f;
                GUI.color=new Color(.1f,.2f,.22f); GUI.DrawTexture(new Rect(x,y+95,panelWidth,5),Texture2D.whiteTexture);
                GUI.color=menuGold; GUI.DrawTexture(new Rect(x,y+95,panelWidth*Mathf.Clamp01(progress),5),Texture2D.whiteTexture); GUI.color=Color.white;
                MenuText(new Rect(x,y+115,panelWidth,30),"Подготовка моря и островов…",true);
                if(MenuAction(x,y+180,panelWidth,"Отменить подключение")) Disconnect();
            }
            else if(menuPage==5) { DrawSteamParty(x,y-42,panelWidth); }
            else if(menuPage==1 || menuPage==2)
            {
                MenuText(new Rect(x,y,panelWidth,35),menuPage==1?"СОЗДАТЬ ЭКСПЕДИЦИЮ":"ПРИСОЕДИНИТЬСЯ");
                MenuText(new Rect(x,y+48,panelWidth,25),"Адрес IPv4:порт",true);
                address=GUI.TextField(new Rect(x,y+78,panelWidth,52),address,64,menuInput);
                if(menuPage==1)
                {
                    MenuText(new Rect(x,y+145,panelWidth,25),"Номер карты · пусто — случайная",true);
                    seedInput=GUI.TextField(new Rect(x,y+175,panelWidth,52),seedInput,12,menuInput);
                    DrawBotToggle(x,y+238,panelWidth);
                }
                else MenuText(new Rect(x,y+150,panelWidth,70),"Введите адрес, который сообщил капитан вашей сессии.",true);
                if(MenuAction(x,y+285,panelWidth,menuPage==1?"Выйти в море":"Подключиться",true)) { PlayerPrefs.SetString("LastEndpoint",address); PlayerPrefs.Save(); Begin(menuPage==1,address); }
                if(MenuAction(x,y+350,panelWidth,"Назад")) menuPage=0;
            }
            else if(menuPage==3)
            {
                var bank=Resources.Load<GameAudioBank>("GameAudioBank");
                MenuText(new Rect(x,y,panelWidth,32),"ЗВУК");
                if(bank!=null)
                {
                    bank.Master=MenuVolume(x,y+60,panelWidth,"Общая громкость",bank.Master,"AudioMaster");
                    bank.Effects=MenuVolume(x,y+135,panelWidth,"Эффекты",bank.Effects,"AudioEffects");
                    bank.Ambience=MenuVolume(x,y+210,panelWidth,"Море и ветер",bank.Ambience,"AudioAmbience");
                    bank.Interface=MenuVolume(x,y+285,panelWidth,"Интерфейс",bank.Interface,"AudioInterface");
                }
                if(MenuAction(x,y+365,panelWidth,"Назад")) { PlayerPrefs.Save(); menuPage=0; }
            }
            else if(menuPage==4)
            {
                MenuText(new Rect(x,y,panelWidth,35),"УПРАВЛЕНИЕ");
                MenuText(new Rect(x,y+55,panelWidth,280),"WASD — движение · E — взаимодействие\nShift + E — снять пушку\nЛКМ — огонь / действие\nПКМ — отмена / еда\n1–6 — предметы · G — бросить\nF1 — вид от третьего лица\nТруба: колесо — зум, нажатие — метка\nПушка: мышь — наведение\nE / Esc — выйти из прицела");
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=0;
            }
            else
            {
                if(playing)
                {
                    if(MenuAction(x,y,panelWidth,"Вернуться на палубу",true)) AdvancedPlayerController.SetCursor(true);
                    if(MenuAction(x,y+68,panelWidth,"Покинуть сессию")) Disconnect();
                    if(steamSession && party.MatchLobby.m_SteamID != 0 && MenuLink(new Rect(x,y-35,panelWidth,32),"Копировать ID Steam-сессии: "+party.MatchLobby)) GUIUtility.systemCopyBuffer=party.MatchLobby.ToString();
                }
                else
                {
                    if(MenuAction(x,y,panelWidth,"Создать сессию",true)) menuPage=party != null && party.InLobby ? 5 : 1;
                    if(MenuAction(x,y+68,panelWidth,"Присоединиться")) menuPage=party != null && party.InLobby ? 5 : 2;
                }
                if(MenuAction(x,y+136,panelWidth,"Команда · Steam")) menuPage=5;
                if(MenuAction(x,y+204,panelWidth,"Настройки звука")) menuPage=3;
                if(MenuAction(x,y+272,panelWidth,"Управление")) menuPage=4;
                if(MenuAction(x,y+340,panelWidth,"Выйти из игры")) Application.Quit();
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
            fillWithBots = MenuToggle(x,y,width,fillWithBots,"Заполнить свободные места ботами");
        }
        bool MenuToggle(float x,float y,float width,bool value,string label)
        {
            bool next=GUI.Toggle(new Rect(x,y,width,35),value,GUIContent.none,GUIStyle.none);
            PirateHudStyle.Diamond(new Vector2(x+11,y+17),14,GUI.enabled?menuGold:PirateHudStyle.Muted);
            PirateHudStyle.Diamond(new Vector2(x+11,y+17),next?7:11,next?PirateHudStyle.Paper:new Color(.025f,.07f,.085f));
            MenuText(new Rect(x+30,y,width-30,35),label,true);
            return next;
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
