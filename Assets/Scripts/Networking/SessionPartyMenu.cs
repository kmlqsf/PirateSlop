using Steamworks;
using UnityEngine;

namespace PirateSlop.Networking
{
    public sealed partial class SessionController
    {
        Vector2 partyScroll;
        string lobbyInput = "";
        void DrawSteamParty(float x, float y, float width)
        {
            if (party == null) { MenuText(new Rect(x,y,width,50), "Steam не настроен"); return; }
            MenuText(new Rect(x,y,width,48), party.Status, true);
            if (!party.InLobby)
            {
                GUI.enabled = party.Available && !party.Busy;
                if (MenuAction(x,y+55,width,"Создать команду",true)) party.Create();
                MenuText(new Rect(x,y+120,width,40),"Примите приглашение через Steam или вставьте ID лобби:",true);
                lobbyInput=GUI.TextField(new Rect(x,y+165,width,45),lobbyInput,20,menuInput);
                if(MenuAction(x,y+225,width,"Войти по ID") && ulong.TryParse(lobbyInput,out var id)) party.Join(new CSteamID(id));
                GUI.enabled=true;
                if(MenuAction(x,y+300,width,"Назад")) menuPage=0;
                return;
            }
            MenuText(new Rect(x,y+42,width,30),"КОМАНДА • "+party.Count+" / "+MaxPlayers,true);
            partyScroll=GUI.BeginScrollView(new Rect(x,y+80,width,150),partyScroll,new Rect(0,0,width-24,party.Count*30));
            for(int i=0;i<party.Count;i++)
            {
                var member=party.Member(i);
                MenuText(new Rect(0,i*30,width-24,30),$"{party.Name(member)}  ·  Экипаж {party.Team(member)}  ·  {(party.IsReady(member)?"Готов":"Ждём")}",true);
            }
            GUI.EndScrollView();
            GUI.enabled=party.Waiting && !SessionBusy;
            int team=party.Team(SteamUser.GetSteamID());
            if(MenuAction(x,y+245,width/2-5,"Экипаж: "+team)) party.SetTeam(team%4+1);
            if(MenuAction(x+width/2+5,y+245,width/2-5,party.IsReady(SteamUser.GetSteamID())?"Не готов":"Готов",true)) party.Ready(!party.IsReady(SteamUser.GetSteamID()));
            if(MenuAction(x,y+310,width/2-5,"Пригласить")) party.Invite();
            if(MenuAction(x+width/2+5,y+310,width/2-5,"Копировать ID")) GUIUtility.systemCopyBuffer=party.Lobby.ToString();
            if(party.IsLeader && MenuAction(x,y+375,width,"Выйти в море",true)) party.Launch();
            GUI.enabled=true;
            if(GUI.Button(new Rect(x,y+440,width,30),"Покинуть лобби")) { party.Leave(); menuPage=0; }
        }
    }
}
