using UnityEngine;
using PirateSlop.World;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        int menuPage;
        GUIStyle menuTitle, menuLabel, menuSmall, menuButton, menuInput;
        Texture2D menuShade;
        readonly Color menuGold = new(.9f,.72f,.4f);
        void PrepareMenu()
        {
            if(menuTitle!=null) return;
            menuTitle=new GUIStyle(GUI.skin.label){fontSize=78,fontStyle=FontStyle.Bold,normal={textColor=new Color(.96f,.91f,.79f)}};
            menuLabel=new GUIStyle(GUI.skin.label){fontSize=21,wordWrap=true,normal={textColor=new Color(.92f,.91f,.85f)}};
            menuSmall=new GUIStyle(menuLabel){fontSize=15,normal={textColor=new Color(.6f,.72f,.73f)}};
            menuButton=new GUIStyle(GUI.skin.button){fontSize=20,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(22,12,0,0),normal={background=Texture2D.whiteTexture,textColor=Color.white},hover={background=Texture2D.whiteTexture,textColor=Color.white},active={background=Texture2D.whiteTexture,textColor=Color.white}};
            menuInput=new GUIStyle(GUI.skin.textField){fontSize=22,padding=new RectOffset(14,14,12,12)};
            menuShade=new Texture2D(128,1,TextureFormat.RGBA32,false); menuShade.wrapMode=TextureWrapMode.Clamp;
            for(int x=0;x<128;x++) menuShade.SetPixel(x,0,new Color(.015f,.045f,.055f,Mathf.Lerp(.95f,.08f,Mathf.SmoothStep(0,1,x/127f))));
            menuShade.Apply();
            if(!connecting && !playing) address=PlayerPrefs.GetString("LastEndpoint","127.0.0.1:"+Config.Port);
        }
        void MenuText(Rect rect,string text,bool small=false) => GUI.Label(rect,text,small?menuSmall:menuLabel);
        bool MenuAction(float x,float y,float width,string label,bool primary=false)
        {
            var rect=new Rect(x,y,width,54);
            var old=GUI.backgroundColor;
            GUI.backgroundColor=primary ? new Color(.55f,.36f,.14f) : new Color(.055f,.12f,.14f);
            bool clicked=GUI.Button(rect,label,menuButton); GUI.backgroundColor=old;
            var color=GUI.color; GUI.color=menuGold; GUI.DrawTexture(new Rect(x,y,3,54),Texture2D.whiteTexture); GUI.color=color;
            if(clicked) GameAudio.Play(SoundCue.Select,Vector3.zero,1f,true);
            return clicked;
        }
        void DrawSessionMenu()
        {
            if(DeveloperMenu.IsOpen || dedicated || Automated || !MenuOpen) return;
            PrepareMenu();
            var oldMatrix=GUI.matrix; var oldColor=GUI.color;
            float scale=Screen.height/900f, width=Screen.width/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
            GUI.color=Color.white; GUI.DrawTexture(new Rect(0,0,width,900),menuShade);
            float x=64, panelWidth=Mathf.Min(480,width-128);
            MenuText(new Rect(x,65,500,30),"МОРЕ • ПОРОХ • СВОБОДА",true);
            GUI.Label(new Rect(x,104,700,115),"PIRATE SLOP",menuTitle);
            MenuText(new Rect(x,222,450,60),playing ? "Ваше приключение продолжается" : "Подними паруса. Найди свою добычу.");
            float y=315;
            if(connecting)
            {
                MenuText(new Rect(x,y,panelWidth,70),status);
                float progress=ProceduralWorld.Instance!=null ? ProceduralWorld.Instance.Progress : 0f;
                GUI.color=new Color(.1f,.2f,.22f); GUI.DrawTexture(new Rect(x,y+95,panelWidth,5),Texture2D.whiteTexture);
                GUI.color=menuGold; GUI.DrawTexture(new Rect(x,y+95,panelWidth*Mathf.Clamp01(progress),5),Texture2D.whiteTexture); GUI.color=Color.white;
                MenuText(new Rect(x,y+115,panelWidth,30),"Подготовка моря и островов…",true);
                if(MenuAction(x,y+180,panelWidth,"Отменить подключение")) Disconnect();
            }
            else if(menuPage==5) { DrawSteamParty(x,y,panelWidth); }
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
                MenuText(new Rect(x,y+55,panelWidth,270),"WASD — движение\nE — взаимодействие / толкнуть корабль\nУдерживать E — снять пушку\nЛКМ — огонь / действие\nПКМ — отмена / еда\n1–6 — предметы · G — бросить\nF1 — вид от третьего лица\nТруба: колесо — зум, E — выход");
                if(MenuAction(x,y+345,panelWidth,"Назад")) menuPage=0;
            }
            else
            {
                if(playing)
                {
                    if(MenuAction(x,y,panelWidth,"Вернуться на палубу",true)) AdvancedPlayerController.SetCursor(true);
                    if(MenuAction(x,y+68,panelWidth,"Покинуть сессию")) Disconnect();
                    if(steamSession && party.MatchLobby.m_SteamID != 0 && GUI.Button(new Rect(x,y-50,panelWidth,32),"Копировать ID Steam-сессии: "+party.MatchLobby)) GUIUtility.systemCopyBuffer=party.MatchLobby.ToString();
                }
                else
                {
                    if(width >= 1120) DrawSteamParty(width-544,245,480);
                    else if(MenuAction(x,y-68,panelWidth,"Команда • Steam",true)) menuPage=5;
                    if(MenuAction(x,y,panelWidth,"Создать сессию",true)) { if(party != null && party.InLobby) party.Launch(); else menuPage=1; }
                    if(MenuAction(x,y+68,panelWidth,"Присоединиться")) menuPage=party != null && party.InLobby ? 5 : 2;
                }
                if(MenuAction(x,y+136,panelWidth,"Настройки звука")) menuPage=3;
                if(MenuAction(x,y+204,panelWidth,"Управление")) menuPage=4;
                if(MenuAction(x,y+272,panelWidth,"Выйти из игры")) Application.Quit();
            }
            if(!string.IsNullOrEmpty(error)) { GUI.color=new Color(1f,.58f,.4f); MenuText(new Rect(x,790,panelWidth,62),error,true); GUI.color=Color.white; }
            MenuText(new Rect(x,856,900,25),playing ? "Игроки: "+(population-botPopulation)+" · Боты: "+botPopulation+" · Команды: "+teamPopulation+" · Мест: "+population+" / "+MaxPlayers : "СОБЕРИ КОМАНДУ  /  ОТКРОЙ НЕИЗВЕСТНЫЕ БЕРЕГА",true);
            MenuText(new Rect(width-200,856,180,25),"PIRATE SLOP  •  01",true);
            GUI.matrix=oldMatrix; GUI.color=oldColor;
        }
        void DrawBotToggle(float x, float y, float width)
        {
            fillWithBots = GUI.Toggle(new Rect(x,y,24,30),fillWithBots,GUIContent.none);
            MenuText(new Rect(x+30,y,width-30,35),"Заполнить свободные места ботами",true);
        }
        float MenuVolume(float x,float y,float width,string label,float value,string key)
        {
            MenuText(new Rect(x,y,width,28),label+"  "+Mathf.RoundToInt(value*100)+"%",true);
            float next=GUI.HorizontalSlider(new Rect(x,y+35,width,22),value,0f,1f);
            if(!Mathf.Approximately(value,next)) PlayerPrefs.SetFloat(key,next);
            return next;
        }
    }
}
