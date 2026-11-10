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
            float w=card.rect.width,h=card.rect.height,right=740f,rw=w-right-12;
            ManagementWidgets.Label(card,"Expeditions - world map",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"Close",new Vector2(w-112,5),new Vector2(100,38),Close);
            float mapWidth=540f,mapHeight=mapWidth*570f/794f;
            var sea=ManagementWidgets.Box(card,"World map",new Vector2(12,0),new Vector2(mapWidth,mapHeight),new Color(.055f,.14f,.19f));
            UIThemeElement.Mark(sea,UIThemeRole.MapBackground);
            var mapArea=ManagementWidgets.Rect(sea.transform,"Map content",new Vector2(mapWidth*.07f,mapHeight*.28f),new Vector2(mapWidth*.86f,mapHeight*.55f));
            var land=ManagementWidgets.Rect(mapArea,"Continents",Vector2.zero,mapArea.sizeDelta).gameObject.AddComponent<ExpeditionMapGraphic>();
            land.color=new Color(.31f,.23f,.13f,.88f);land.raycastTarget=false;
            var occupiedPins=new List<Rect>();
            foreach(var region in progression.Rules.regions)
            {
                var r=region;
                Vector2 pin=PlacePin(mapArea.sizeDelta,new Vector2(r.mapPosition.x*mapArea.rect.width-47,
                    (1-r.mapPosition.y)*mapArea.rect.height-16),occupiedPins);
                var button=ManagementWidgets.Button(mapArea,region.name,pin,
                    new Vector2(94,32),()=>{selectedRegion=r.id;Refresh();},12);
                pins.Add(new KeyValuePair<ExpeditionRegion,Button>(region,button));
            }
            teamsText=ManagementWidgets.Label(card,"Expedition teams",new Vector2(12,397),new Vector2(280,58),16);
            buyTeam=ManagementWidgets.Button(card,"Buy team",new Vector2(12,460),new Vector2(280,38),()=>{progression.BuyTeam(home);Refresh();});
            ManagementWidgets.Label(card,"Active expeditions",new Vector2(315,397),new Vector2(350,24),18);
            missionContent=ManagementWidgets.Scroll(card,"Active missions",new Vector2(315,425),new Vector2(350,73),0);
            missionLog=ManagementWidgets.Label(missionContent,"No active expeditions.",new Vector2(6,4),new Vector2(324,0),17);
            regionText=ManagementWidgets.Label(card,"Choose a region",new Vector2(right,0),new Vector2(rw,90),18);
            buyRegion=ManagementWidgets.Button(card,"Buy location",new Vector2(right,98),new Vector2(rw,38),()=>{progression.BuyAccess(home,selectedRegion);Refresh();});
            ManagementWidgets.Label(card,"Mission difficulty",new Vector2(right,146),new Vector2(rw,24));
            float bw=(rw-6*Math.Max(0,progression.Rules.difficulties.Length-1))/Math.Max(1,progression.Rules.difficulties.Length);
            for(int i=0;i<progression.Rules.difficulties.Length;i++)
            {
                var d=progression.Rules.difficulties[i];
                var button=ManagementWidgets.Button(card,d.name,new Vector2(right+i*(bw+6),178),new Vector2(bw,38),()=>{selectedDifficulty=d.id;Refresh();},15);
                difficulties.Add(new KeyValuePair<ExpeditionDifficulty,Button>(d,button));
            }
            ManagementWidgets.Button(card,"- team",new Vector2(right,228),new Vector2((rw-12)/2,36),()=>{teamCount=Math.Max(1,teamCount-1);Refresh();},16);
            ManagementWidgets.Button(card,"+ team",new Vector2(right+(rw+12)/2,228),new Vector2((rw-12)/2,36),()=>{teamCount=Math.Min(Math.Max(1,progression.AvailableTeams),teamCount+1);Refresh();},16);
            missionPreview=ManagementWidgets.Label(card,"Mission preview",new Vector2(right,278),new Vector2(rw,126),17);
            send=ManagementWidgets.Button(card,"Send expedition",new Vector2(right,414),new Vector2(rw,38),()=>{
                result.text=progression.Launch(home,selectedRegion,selectedDifficulty,teamCount)?"Expedition dispatched.":"Check access, teams, funds and the centre's path connection.";Refresh();});
            result=ManagementWidgets.Label(card,"",new Vector2(right,460),new Vector2(rw,38),16);
            selectedRegion=progression.Rules.regions.Length>0?progression.Rules.regions[0].id:null;
            selectedDifficulty=progression.Rules.difficulties.Length>0?progression.Rules.difficulties[0].id:null;

            research=ManagementWidgets.Modal(canvas,"Technology research");card=ManagementWidgets.Card(research,canvas);w=card.rect.width;h=card.rect.height;techWidth=w-24;
            ManagementWidgets.Label(card,"Technology research",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"Close",new Vector2(w-112,5),new Vector2(100,38),Close);
            researchStatus=ManagementWidgets.Label(card,"Research projects",new Vector2(12,0),new Vector2(w-24,72),19);
            technologyContent=ManagementWidgets.Scroll(card,"Technology projects - scroll",new Vector2(12,88),new Vector2(w-24,h-90),0);
        }

        private static Vector2 PlacePin(Vector2 mapSize,Vector2 desired,List<Rect> occupied)
        {
            Vector2 origin=new Vector2(Mathf.Clamp(desired.x,0,mapSize.x-94),Mathf.Clamp(desired.y,0,mapSize.y-32));
            Vector2 best=origin;float bestDistance=float.PositiveInfinity;
            for(int x=-12;x<=12;x++)for(int y=-12;y<=12;y++)
            {
                Vector2 candidate=origin+new Vector2(x*8f,y*8f);
                if(candidate.x<0||candidate.y<0||candidate.x+94>mapSize.x||candidate.y+32>mapSize.y)continue;
                float distance=(candidate-origin).sqrMagnitude;if(distance>=bestDistance)continue;
                var bounds=new Rect(candidate-new Vector2(2,2),new Vector2(98,36));bool blocked=false;
                foreach(Rect other in occupied)if(bounds.Overlaps(other)){blocked=true;break;}
                if(!blocked){best=candidate;bestDistance=distance;}
            }
            occupied.Add(new Rect(best,new Vector2(94,32)));return best;
        }
        public void OpenExpeditions(ParkBuildingRecord building){home=building;research.SetActive(false);map.SetActive(true);map.transform.SetAsLastSibling();result.text="";Refresh();UIThemeApplicator.ApplyAll(map.GetComponentInParent<Canvas>());}
        public void OpenResearch(ParkBuildingRecord building){home=building;map.SetActive(false);research.SetActive(true);research.transform.SetAsLastSibling();techSignature=null;Refresh();UIThemeApplicator.ApplyAll(research.GetComponentInParent<Canvas>());}
        public void Close(){map.SetActive(false);research.SetActive(false);}
        public void Refresh()
        {
            if(!IsOpen)return;
            if(map.activeSelf) RefreshMap();
            if(research.activeSelf)
            {
                var p=progression.ActiveProject(home);
                researchStatus.text=$"Funds: {economy.Balance:0} | Researchers: {home.HiredCount}/{home.Footprint.Definition.staffCapacity} | Work speed: {progression.StaffEfficiency:0.##}x\n"
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
            teamsText.text=$"Teams: {progression.Teams}/{progression.Rules.maximumTeams} | Available: {progression.AvailableTeams}\nFunds: {economy.Balance:0}\n"
                +(open?"More teams improve success.":"Reconnect the centre's path.");
            buyTeam.GetComponentInChildren<Text>().text=$"Buy team ({progression.NextTeamCost:0})";
            buyTeam.interactable=open&&progression.Teams<progression.Rules.maximumTeams&&economy.CanAfford(progression.NextTeamCost);
            var region=progression.Region(selectedRegion);var d=progression.Difficulty(selectedDifficulty);bool available=progression.RegionHasCreatures(selectedRegion);
            if (region == null || d == null)
            {
                regionText.text = "Assign at least one location and mission difficulty in the progression settings.";
                missionPreview.text = "No mission configured.";
                buyRegion.interactable = false; send.interactable = false;
                return;
            }
            bool access=progression.HasAccess(selectedRegion);
            regionText.text=region.name+"\n"+(available?(access?"Expedition permit owned.":$"Expedition permit: {region.accessCost:0}")+"\nPossible creatures: "+Names(region.species):"No creature species assigned yet. This location cannot be purchased.");
            buyRegion.GetComponentInChildren<Text>().text=access?"Location unlocked":"Buy location ("+region.accessCost.ToString("0")+")";
            buyRegion.interactable=open&&available&&!access&&economy.CanAfford(region.accessCost);
            foreach(var pin in pins)pin.Value.GetComponent<Image>().color=pin.Key.id==selectedRegion?new Color(.45f,.37f,.17f)
                :progression.HasAccess(pin.Key.id)?new Color(.16f,.4f,.29f):new Color(.13f,.21f,.25f);
            foreach(var choice in difficulties)choice.Value.GetComponent<Image>().color=choice.Key.id==selectedDifficulty?new Color(.45f,.37f,.17f):new Color(.18f,.28f,.31f);
            var eligible=progression.Eligible(selectedRegion,selectedDifficulty);
            missionPreview.text=$"Teams assigned: {teamCount}\nSuccess: {100*progression.Chance(selectedDifficulty,teamCount):0}% | Time: {progression.MissionSeconds(d):0}s\n"
                +$"Cost: {teamCount*d.costPerTeam:0} | Rating: {d.minimumRating}-{d.maximumRating}/5\n"
                +(eligible.Count>0?$"On success: {d.creaturesFound} of one species\nCandidates: "+Names(eligible):"No eligible creatures for this difficulty.");
            send.interactable=open&&access&&eligible.Count>0&&progression.AvailableTeams>=teamCount&&economy.CanAfford(teamCount*d.costPerTeam);
            var text=new StringBuilder();foreach(var m in progression.Missions)
                text.Append(progression.Region(m.region).name).Append(" - ").Append(progression.Difficulty(m.difficulty).name)
                    .Append("\n").Append(m.teams).Append(" team(s) | ").Append(Mathf.CeilToInt(m.remaining)).Append("s left | ").Append((100*m.chance).ToString("0")).Append("% success\n\n");
            missionLog.text=text.Length==0?"No active expeditions.\nTeams return after every mission.":text.ToString();
            float height=Mathf.Ceil(missionLog.preferredHeight);missionLog.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
            missionContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height+8);
            UIOverflowScroll.Attach(missionContent.GetComponentInParent<ScrollRect>()).Refresh();
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
                var details=ManagementWidgets.Label(row.transform,t.description+"\n"+$"Cost: {t.cost:0} | Work: {t.workSeconds:0} researcher-seconds"+(reason==""?"":"\n"+reason),
                    new Vector2(10,39),new Vector2(techWidth-225,0),17);
                float detailsHeight=Mathf.Ceil(details.preferredHeight),rowHeight=Mathf.Max(86,47+detailsHeight);
                details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,detailsHeight);
                ((RectTransform)row.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,rowHeight);
                var button=ManagementWidgets.Button(row.transform,progression.Completed(t.id)?"Completed":p?.Definition.id==t.id?"Researching":"Start research",
                    new Vector2(techWidth-195,(rowHeight-42)/2),new Vector2(180,42),()=>{staff.StartTechnology(home,t.id);techSignature=null;Refresh();},17);
                button.interactable=reason=="";y+=rowHeight+12;
            }
            technologyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(0,y-12));
            UIOverflowScroll.Attach(technologyContent.GetComponentInParent<ScrollRect>()).Refresh();
        }
        public void Dispose(){UnityEngine.Object.Destroy(map);UnityEngine.Object.Destroy(research);}
    }
}
