using Steamworks;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        string lobbyInput = "";
        string matchInput = "";
        bool joinOtherSession;
        GUIStyle crewNameStyle, crewHeadingStyle;
        void DrawSteamParty(float x, float y, float width)
        {
            if (party == null) { MenuText(new Rect(x,y,width,50), "Steam не настроен"); if(MenuAction(x,y+70,width,"Назад")) menuPage=0; return; }
            MenuText(new Rect(x,y,width,48), party.Status, true);
            GUI.enabled = party.Available && !party.Busy;
            if (MenuAction(x,y+55,width,"Создать лобби",true)) party.Create();
            MenuText(new Rect(x,y+120,width,40),"Примите приглашение через Steam или вставьте ID лобби:",true);
            RegisterMenuControl(new Rect(x,y+165,width,45),"menu_lobby");
            GUI.SetNextControlName("menu_lobby");
            lobbyInput=GUI.TextField(new Rect(x,y+165,width,45),lobbyInput,20,menuInput);
            if(MenuAction(x,y+225,width,"Войти по ID") && ulong.TryParse(lobbyInput,out var id)) party.Join(new CSteamID(id));
            GUI.enabled=true;
            if(menuPage==5 && MenuAction(x,y+300,width,"Назад")) menuPage=0;
        }
        void DrawCrewLobby(float screenWidth)
        {
            float x=(screenWidth-1180)*.5f;
            if(crewNameStyle==null)
            {
                crewNameStyle=new GUIStyle(menuSmall){fontSize=17,wordWrap=false,clipping=TextClipping.Clip};
                crewHeadingStyle=new GUIStyle(menuLabel){fontSize=22,fontStyle=FontStyle.Bold,wordWrap=false};
            }
            PirateHudStyle.Fill(new Rect(x-24,36,1228,828),new Color(.014f,.037f,.048f,.95f));
            MenuText(new Rect(x,62,800,44),"СБОР ЭКИПАЖЕЙ");
            MenuText(new Rect(x,110,1000,30),"10 кораблей · до 3 пиратов в экипаже · переходите между командами до старта",true);
            MenuText(new Rect(x+920,70,260,35),party.Count+" / "+SteamParty.LobbyCapacity+" игроков",true);
            if(MenuLink(new Rect(x+900,116,280,28),"Копировать ID лобби")) GUIUtility.systemCopyBuffer=party.Lobby.ToString();
            if(MenuLink(new Rect(x+610,116,250,28),"Сессия другого капитана")) joinOtherSession=!joinOtherSession;
            MenuRule(x,164,1180);
            var me=SteamUser.GetSteamID();
            int myTeam=party.Team(me);
            bool unlocked=party.Waiting && !party.Busy && !SessionBusy;
            for(int index=0;index<SteamParty.TeamCount;index++)
            {
                int team=index+1, count=party.TeamMembers(team);
                float cx=x+(index%5)*240, cy=190+(index/5)*228;
                bool mine=myTeam==team;
                Color color=Color.HSVToRGB(index*.097f,.42f,.78f);
                PirateHudStyle.Fill(new Rect(cx,cy,220,208),mine?new Color(.08f,.14f,.16f,.98f):new Color(.035f,.066f,.079f,.96f));
                PirateHudStyle.Fill(new Rect(cx,cy,220,mine?4:2),color);
                crewHeadingStyle.normal.textColor=mine?menuGold:menuIvory;
                GUI.Label(new Rect(cx+12,cy+12,154,30),"Экипаж "+team.ToString("00"),crewHeadingStyle);
                MenuText(new Rect(cx+171,cy+16,42,25),count+" / 3",true);
                int slot=0;
                for(int memberIndex=0;memberIndex<party.Count;memberIndex++)
                {
                    var member=party.Member(memberIndex);
                    if(party.Team(member)!=team) continue;
                    float sy=cy+53+slot*34;
                    bool ready=party.IsReady(member);
                    MenuDiamond(new Vector2(cx+16,sy+13),5,ready?new Color(.45f,.77f,.59f):menuMuted);
                    crewNameStyle.normal.textColor=member==me?menuGold:menuIvory;
                    string suffix=(member==me?" · вы":"")+(SteamMatchmaking.GetLobbyOwner(party.Lobby)==member?" · хост":"");
                    string name=party.Name(member)+suffix;
                    GUI.Label(new Rect(cx+29,sy,151,27),new GUIContent(name,name),crewNameStyle);
                    if(ready) MenuText(new Rect(cx+183,sy,26,27),"✓",true);
                    slot++;
                }
                for(;slot<CrewSize;slot++)
                {
                    float sy=cy+53+slot*34;
                    crewNameStyle.normal.textColor=menuMuted;
                    GUI.Label(new Rect(cx+29,sy,175,27),"Свободное место",crewNameStyle);
                }
                GUI.enabled=unlocked && !party.TeamPending(me) && !mine && count<CrewSize;
                if(MenuChoice(cx+4,cy+158,212,mine?"Ваш экипаж":count>=CrewSize?"Нет мест":"Присоединиться",mine)) party.SelectTeam(team);
                GUI.enabled=true;
            }
            MenuText(new Rect(x,642,1180,30),party.TeamPending(me)?"Хост подтверждает выбор экипажа…":"Ваш экипаж: "+myTeam+" · один экипаж — один корабль. Золотом отмечено ваше имя; зелёным — готовые игроки.",true);
            if(party.IsLeader)
            {
                GUI.enabled=unlocked;
                if(MenuChoice(x,688,220,"Обычная карта",!useTestMap)) useTestMap=false;
                if(MenuDeveloperTools && MenuChoice(x+230,688,220,"Тестовая карта",useTestMap)) useTestMap=true;
                if(!useTestMap && !joinOtherSession) fillWithBots=MenuToggle(x+475,695,430,fillWithBots,"Заполнить свободные места ботами");
                if(joinOtherSession)
                {
                    RegisterMenuControl(new Rect(x+480,689,400,45),"menu_match");
                    GUI.SetNextControlName("menu_match");
                    matchInput=GUI.TextField(new Rect(x+480,689,400,45),matchInput,20,menuInput);
                    if(MenuAction(x+900,687,275,"Войти по ID") && ulong.TryParse(matchInput,out var match)) party.JoinSession(match);
                }
                if(fillWithBots) observerSelected=false;
            }
            else MenuText(new Rect(x,688,900,46),"Выберите экипаж и нажмите «Я готов». Карту и начало экспедиции выбирает хост.",true);
            GUI.enabled=unlocked;
            if(MenuAction(x,746,245,"Пригласить друзей")) party.Invite();
            GUI.enabled=unlocked && !party.TeamPending(me);
            if(MenuAction(x+265,746,235,party.IsReady(me)?"Я не готов":"Я готов",true)) party.Ready(!party.IsReady(me));
            GUI.enabled=unlocked && party.IsLeader;
            if(MenuAction(x+525,746,295,"Начать экспедицию",true)) party.Launch(useTestMap);
            GUI.enabled=true;
            MenuText(new Rect(x,809,720,36),party.Status,true);
            if(MenuLink(new Rect(x+750,812,130,30),"В меню")) menuPage=0;
            if(!SessionBusy && MenuLink(new Rect(x+920,812,260,30),"Покинуть лобби")) { party.Leave(); menuPage=0; }
            if(SessionBusy && MenuLink(new Rect(x+920,812,260,30),"Назад")) menuPage=0;
        }
    }
}
