using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    /// <summary>World map and technology projects. Time continues while these management windows are open.</summary>
    public sealed class ProgressionPresentation : IDisposable
    {
        private readonly ParkProgression progression;
        private readonly StaffWorld staff;
        private readonly ParkEconomy economy;
        private readonly Func<string,string> speciesName;
        private readonly GameObject map, research;
        private readonly Text teamsText, regionText, missionPreview, missionLog, researchStatus, result;
        private readonly Button buyTeam, buyRegion, send;
        private readonly RectTransform missionContent, technologyContent;
        private readonly List<KeyValuePair<ExpeditionRegion,Button>> pins=new List<KeyValuePair<ExpeditionRegion,Button>>();
        private readonly List<KeyValuePair<ExpeditionDifficulty,Button>> difficulties=new List<KeyValuePair<ExpeditionDifficulty,Button>>();
        private ParkBuildingRecord home;
        private string selectedRegion, selectedDifficulty;
        private int teamCount=1;
        private readonly float techWidth;
        public bool IsOpen => map.activeSelf||research.activeSelf;
        public ProgressionPresentation(Canvas canvas,ParkProgression progression,StaffWorld staff,ParkEconomy economy,Func<string,string> speciesName)
        {
            this.progression=progression;this.staff=staff;this.economy=economy;this.speciesName=speciesName;
            map=ManagementWidgets.Modal(canvas,"Expedition world map");var card=ManagementWidgets.Card(map,canvas);
            float w=card.rect.width,h=card.rect.height,left=w*.6f,right=w*.64f,rw=w-right-12;
            ManagementWidgets.Label(card,"Expeditions - world map",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"Close",new Vector2(w-112,5),new Vector2(100,38),Close);
            var sea=ManagementWidgets.Box(card,"World map",new Vector2(12,65),new Vector2(left-12,h*.43f),new Color(.055f,.14f,.19f));
            var land=ManagementWidgets.Rect(sea.transform,"Continents",Vector2.zero,new Vector2(left-12,h*.43f)).gameObject.AddComponent<ExpeditionMapGraphic>();
            land.color=new Color(.2f,.35f,.29f);land.raycastTarget=false;
            foreach(var region in progression.Rules.regions)
            {
                var r=region;
                var button=ManagementWidgets.Button(sea.transform,region.name,
                    new Vector2(r.mapPosition.x*(left-12)-47,(1-r.mapPosition.y)*h*.43f-16),new Vector2(94,32),()=>{selectedRegion=r.id;Refresh();},12);
                pins.Add(new KeyValuePair<ExpeditionRegion,Button>(region,button));
            }
            float row=80+h*.43f;
            teamsText=ManagementWidgets.Label(card,"Expedition teams",new Vector2(12,row),new Vector2(left-12,52),18);
            buyTeam=ManagementWidgets.Button(card,"Buy team",new Vector2(12,row+54),new Vector2(Mathf.Min(290,left-12),42),()=>{progression.BuyTeam(home);Refresh();});
            ManagementWidgets.Label(card,"Active expeditions (scroll)",new Vector2(12,row+108),new Vector2(left-12,30),19);
            missionContent=ManagementWidgets.Scroll(card,"Active missions",new Vector2(12,row+142),new Vector2(left-12,Mathf.Max(65,h-row-154)),100);
            missionLog=ManagementWidgets.Label(missionContent,"No active expeditions.",new Vector2(8,6),new Vector2(left-28,100),17);
            regionText=ManagementWidgets.Label(card,"Choose a region",new Vector2(right,65),new Vector2(rw,112),20);
            buyRegion=ManagementWidgets.Button(card,"Buy location",new Vector2(right,181),new Vector2(rw,42),()=>{progression.BuyAccess(home,selectedRegion);Refresh();});
            ManagementWidgets.Label(card,"Mission difficulty",new Vector2(right,238),new Vector2(rw,30));
            float bw=(rw-12)/3;
            for(int i=0;i<progression.Rules.difficulties.Length;i++)
            {
                var d=progression.Rules.difficulties[i];
                var button=ManagementWidgets.Button(card,d.name,new Vector2(right+i*(bw+6),272),new Vector2(bw,40),()=>{selectedDifficulty=d.id;Refresh();},15);
                difficulties.Add(new KeyValuePair<ExpeditionDifficulty,Button>(d,button));
            }
            ManagementWidgets.Button(card,"- team",new Vector2(right,326),new Vector2((rw-12)/2,36),()=>{teamCount=Math.Max(1,teamCount-1);Refresh();},16);
            ManagementWidgets.Button(card,"+ team",new Vector2(right+(rw+12)/2,326),new Vector2((rw-12)/2,36),()=>{teamCount=Math.Min(Math.Max(1,progression.AvailableTeams),teamCount+1);Refresh();},16);
            missionPreview=ManagementWidgets.Label(card,"Mission preview",new Vector2(right,377),new Vector2(rw,158),18);
            send=ManagementWidgets.Button(card,"Send expedition",new Vector2(right,Mathf.Min(543,h-105)),new Vector2(rw,42),()=>{
                result.text=progression.Launch(home,selectedRegion,selectedDifficulty,teamCount)?"Expedition dispatched.":"Check access, teams, funds and the centre's path connection.";Refresh();});
            result=ManagementWidgets.Label(card,"",new Vector2(right,Mathf.Min(590,h-52)),new Vector2(rw,50),16);
            selectedRegion=progression.Rules.regions[0].id;selectedDifficulty=progression.Rules.difficulties[0].id;

            research=ManagementWidgets.Modal(canvas,"Technology research");card=ManagementWidgets.Card(research,canvas);w=card.rect.width;h=card.rect.height;techWidth=w-24;
            ManagementWidgets.Label(card,"Technology research",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"Close",new Vector2(w-112,5),new Vector2(100,38),Close);
            researchStatus=ManagementWidgets.Label(card,"Research projects",new Vector2(12,60),new Vector2(w-24,80),19);
            technologyContent=ManagementWidgets.Scroll(card,"Technology projects - scroll",new Vector2(12,147),new Vector2(w-24,h-160),0);
        }
        public void OpenExpeditions(ParkBuildingRecord building){home=building;research.SetActive(false);map.SetActive(true);map.transform.SetAsLastSibling();result.text="";Refresh();}
        public void OpenResearch(ParkBuildingRecord building){home=building;map.SetActive(false);research.SetActive(true);research.transform.SetAsLastSibling();techSignature=null;Refresh();}
        public void Close(){map.SetActive(false);research.SetActive(false);}
        public void Refresh()
        {
            if(!IsOpen)return;
            if(map.activeSelf) RefreshMap();
            if(research.activeSelf)
            {
                var p=progression.ActiveProject(home);
                researchStatus.text=$"Funds: {economy.Balance:0} | Researchers: {home.HiredCount}/5 | Work speed: {progression.StaffEfficiency:0.##}x\n"
                    +(p==null?"Choose a technology below. Creature care research remains a separate assignment."
                    :$"{p.Definition.name}: {100*p.Work/p.Definition.workSeconds:0}% | {Mathf.CeilToInt(p.Definition.workSeconds-p.Work)} researcher-seconds left")
                    +(home.IsOpen?"":"\nPaused: reconnect the research centre's entrance path.");
                // Affordability and prerequisites can change while this window remains open.
                string signature=home.RuntimeId+":"+progression.Revision+":"+home.IsOpen+":"+home.HiredCount;
                foreach(var t in progression.Rules.technologies)signature+=economy.CanAfford(t.cost)?"1":"0";
                if(signature!=techSignature){techSignature=signature;RebuildTechnology();}
            }
        }
        private string techSignature;
        private void RefreshMap()
        {
            teamCount=Mathf.Clamp(teamCount,1,Math.Max(1,progression.AvailableTeams));
            bool open=progression.CanUse(home,BuildingKind.ExpeditionCentre);
            teamsText.text=$"Teams: {progression.Teams}/{progression.Rules.maximumTeams} owned | {progression.AvailableTeams} available | Funds: {economy.Balance:0}\n"
                +(open?"Several teams on one mission increase its success chance.":"Reconnect the expedition centre's entrance path to buy or launch.");
            buyTeam.GetComponentInChildren<Text>().text=$"Buy team ({progression.NextTeamCost:0})";
            buyTeam.interactable=open&&progression.Teams<progression.Rules.maximumTeams&&economy.CanAfford(progression.NextTeamCost);
            var region=progression.Region(selectedRegion);var d=progression.Difficulty(selectedDifficulty);bool available=progression.RegionHasCreatures(selectedRegion);
            bool access=progression.HasAccess(selectedRegion);
            regionText.text=region.name+"\n"+(available?(access?"Expedition permit owned.":$"Expedition permit: {region.accessCost:0}")+"\nPossible creatures: "+Names(region.species):"No creature species assigned yet. This location cannot be purchased.");
            buyRegion.GetComponentInChildren<Text>().text=access?"Location unlocked":"Buy location ("+region.accessCost.ToString("0")+")";
            buyRegion.interactable=open&&available&&!access&&economy.CanAfford(region.accessCost);
            foreach(var pin in pins)pin.Value.GetComponent<Image>().color=pin.Key.id==selectedRegion?new Color(.45f,.37f,.17f)
                :progression.HasAccess(pin.Key.id)?new Color(.16f,.4f,.29f):new Color(.13f,.21f,.25f);
            foreach(var choice in difficulties)choice.Value.GetComponent<Image>().color=choice.Key.id==selectedDifficulty?new Color(.45f,.37f,.17f):new Color(.18f,.28f,.31f);
            var eligible=progression.Eligible(selectedRegion,selectedDifficulty);
            missionPreview.text=$"Teams assigned: {teamCount}\nSuccess: {100*progression.Chance(selectedDifficulty,teamCount):0}% | Time: {d.seconds*progression.DurationMultiplier:0}s\n"
                +$"Cost: {teamCount*d.costPerTeam:0} | Rating: {d.minimumRating}-{d.maximumRating}/5\n"
                +(eligible.Count>0?$"On success: {d.creaturesFound} of one species\nCandidates: "+Names(eligible):"No eligible creatures for this difficulty.");
            send.interactable=open&&access&&eligible.Count>0&&progression.AvailableTeams>=teamCount&&economy.CanAfford(teamCount*d.costPerTeam);
            var text=new StringBuilder();foreach(var m in progression.Missions)
                text.Append(progression.Region(m.region).name).Append(" - ").Append(progression.Difficulty(m.difficulty).name)
                    .Append("\n").Append(m.teams).Append(" team(s) | ").Append(Mathf.CeilToInt(m.remaining)).Append("s left | ").Append((100*m.chance).ToString("0")).Append("% success\n\n");
            missionLog.text=text.Length==0?"No active expeditions.\nTeams return on both success and failure.":text.ToString();
            float height=Mathf.Max(100,missionLog.preferredHeight+12);missionLog.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            missionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height+12);
        }
        private string Names(IEnumerable<string> ids)
        {var names=new List<string>();foreach(string id in ids)names.Add(speciesName(id));return string.Join(", ",names);}
        private void RebuildTechnology()
        {
            ManagementWidgets.Clear(technologyContent);float y=0;
            foreach(var definition in progression.Rules.technologies)
            {
                var t=definition;
                var row=ManagementWidgets.Box(technologyContent,t.name,new Vector2(0,y),new Vector2(techWidth,118),new Color(.07f,.12f,.14f));
                var p=progression.ActiveProject(home);string reason=progression.ResearchBlock(home,t.id);
                ManagementWidgets.Label(row.transform,t.name,new Vector2(10,8),new Vector2(techWidth-225,28),21);
                ManagementWidgets.Label(row.transform,t.description+"\n"+$"Cost: {t.cost:0} | Work: {t.workSeconds:0} researcher-seconds"+(reason==""?"":"\n"+reason),
                    new Vector2(10,39),new Vector2(techWidth-225,75),17);
                var button=ManagementWidgets.Button(row.transform,progression.Completed(t.id)?"Completed":p?.Definition.id==t.id?"Researching":"Start research",
                    new Vector2(techWidth-195,36),new Vector2(180,42),()=>{staff.StartTechnology(home,t.id);techSignature=null;Refresh();},17);
                button.interactable=reason=="";y+=126;
            }
            technologyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,y);
        }
        public void Dispose(){UnityEngine.Object.Destroy(map);UnityEngine.Object.Destroy(research);}
    }
}
