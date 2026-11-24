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
        private readonly RectTransform mapArea;
        private readonly ExpeditionMapGraphic mapGraphic;
        private readonly Vector2[] pinPositions;
        private readonly Rect[] pinPlacements;
        private Vector2 placedMapSize;
        private bool pinsPlaced;
        private static readonly Vector2 PinSize=new Vector2(40,32);
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
            float w=card.rect.width,h=card.rect.height,right=884f,rw=w-right-12;
            ManagementWidgets.Label(card,"park.progression_presentation.expeditions_world_map",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"park.progression_presentation.close",new Vector2(w-112,5),new Vector2(100,38),Close);
            float mapWidth=852f,mapHeight=424f,paperHeight=mapWidth*570f/794f;
            var surface=ManagementWidgets.Rect(card,"Expedition map viewport",new Vector2(12,0),new Vector2(mapWidth,mapHeight));
            surface.gameObject.AddComponent<RectMask2D>();
            // Crop the native-aspect parchment to the wide map surface. The drawing now
            // uses the panel's full height instead of a small inset inside another frame.
            var sea=ManagementWidgets.Box(surface,"World map",new Vector2(0,(mapHeight-paperHeight)/2),new Vector2(mapWidth,paperHeight),new Color(.055f,.14f,.19f));
            UIThemeElement.Mark(sea,UIThemeRole.MapBackground);
            mapArea=ManagementWidgets.Rect(surface,"Map content",new Vector2(12,12),new Vector2(mapWidth-24,mapHeight-24));
            mapArea.gameObject.AddComponent<RectMask2D>();
            mapGraphic=ManagementWidgets.Rect(mapArea,"Continents",Vector2.zero,mapArea.sizeDelta).gameObject.AddComponent<ExpeditionMapGraphic>();
            mapGraphic.color=new Color(.31f,.23f,.13f,.88f);mapGraphic.raycastTarget=false;
            mapGraphic.SetViewport(new Rect(0,0,1,1));
            pinPositions=new Vector2[progression.Rules.regions.Length];
            pinPlacements=new Rect[progression.Rules.regions.Length];
            foreach(var region in progression.Rules.regions)
            {
                var r=region;
                // The number is an unbound caption; only the hover name is localized.
                var button=ManagementWidgets.Button(mapArea,"Map pin "+r.id,Vector2.zero,PinSize,()=>SelectRegion(r),16);
                button.name=Loc.InternalName(region.name);
                button.GetComponentInChildren<Text>().text=(pins.Count+1).ToString();
                UIHoverTooltip.Attach(button,region.name);
                pins.Add(new KeyValuePair<ExpeditionRegion,Button>(region,button));
            }
            // Reflow after saved layout overrides change the map area in the designer.
            UILayoutElement.Attach(mapGraphic.rectTransform).RefreshLayout=LayoutMapPins;
            teamsText=ManagementWidgets.Label(card,"park.progression_presentation.expedition_teams",new Vector2(12,432),new Vector2(280,64),16);
            buyTeam=ManagementWidgets.Button(card,"park.progression_presentation.buy_team",new Vector2(304,454),new Vector2(190,38),()=>{progression.BuyTeam(home);Refresh();},16);
            ManagementWidgets.Label(card,"park.progression_presentation.active_expeditions",new Vector2(510,432),new Vector2(354,22),17);
            missionContent=ManagementWidgets.Scroll(card,"Active missions",new Vector2(510,456),new Vector2(354,42),0);
            missionLog=ManagementWidgets.Label(missionContent,"park.progression_presentation.no_active_expeditions",new Vector2(6,4),new Vector2(326,0),15);
            regionText=ManagementWidgets.Label(card,"park.progression_presentation.choose_a_region",new Vector2(right,0),new Vector2(rw,104),16);
            buyRegion=ManagementWidgets.Button(card,"park.progression_presentation.buy_location",new Vector2(right,110),new Vector2(rw,36),()=>{progression.BuyAccess(home,selectedRegion);Refresh();},16);
            ManagementWidgets.Label(card,"park.progression_presentation.mission_difficulty",new Vector2(right,154),new Vector2(rw,22),16);
            float bw=(rw-6*Math.Max(0,progression.Rules.difficulties.Length-1))/Math.Max(1,progression.Rules.difficulties.Length);
            for(int i=0;i<progression.Rules.difficulties.Length;i++)
            {
                var d=progression.Rules.difficulties[i];
                var button=ManagementWidgets.Button(card,d.name,new Vector2(right+i*(bw+6),180),new Vector2(bw,34),()=>{selectedDifficulty=d.id;Refresh();},14);
                difficulties.Add(new KeyValuePair<ExpeditionDifficulty,Button>(d,button));
            }
            ManagementWidgets.Button(card,"park.progression_presentation.team",new Vector2(right,224),new Vector2((rw-12)/2,32),()=>{teamCount=Math.Max(1,teamCount-1);Refresh();},15);
            ManagementWidgets.Button(card,"park.progression_presentation.team_2",new Vector2(right+(rw+12)/2,224),new Vector2((rw-12)/2,32),()=>{teamCount=Math.Min(Math.Max(1,progression.AvailableTeams),teamCount+1);Refresh();},15);
            missionPreview=ManagementWidgets.Label(card,"park.progression_presentation.mission_preview",new Vector2(right,266),new Vector2(rw,130),16);
            send=ManagementWidgets.Button(card,"park.progression_presentation.send_expedition",new Vector2(right,406),new Vector2(rw,38),()=>{
                result.text=progression.Launch(home,selectedRegion,selectedDifficulty,teamCount)?Loc.Text("park.progression_presentation.expedition_dispatched"):Loc.Text("park.progression_presentation.check_access_teams_funds_and_the_centre_s_path_connection");Refresh();});
            result=ManagementWidgets.Label(card,"",new Vector2(right,454),new Vector2(rw,44),15);
            selectedRegion=progression.Rules.regions.Length>0?progression.Rules.regions[0].id:null;
            selectedDifficulty=progression.Rules.difficulties.Length>0?progression.Rules.difficulties[0].id:null;

            research=ManagementWidgets.Modal(canvas,"Technology research");card=ManagementWidgets.Card(research,canvas);w=card.rect.width;h=card.rect.height;techWidth=w-24;
            ManagementWidgets.Label(card,"park.progression_presentation.technology_research",new Vector2(12,8),new Vector2(w-150,45),26);
            ManagementWidgets.Button(card,"park.progression_presentation.close",new Vector2(w-112,5),new Vector2(100,38),Close);
            researchStatus=ManagementWidgets.Label(card,"park.progression_presentation.research_projects",new Vector2(12,0),new Vector2(w-24,72),19);
            technologyContent=ManagementWidgets.Scroll(card,"Technology projects - scroll",new Vector2(12,88),new Vector2(w-24,h-90),0);
        }

        private static Vector2 PlacePin(Vector2 mapSize,Vector2 desired,Vector2 pinSize,List<Rect> occupied)
        {
            Vector2 origin=new Vector2(Mathf.Clamp(desired.x,0,mapSize.x-pinSize.x),Mathf.Clamp(desired.y,0,mapSize.y-pinSize.y));
            Vector2 best=origin;float bestDistance=float.PositiveInfinity;
            for(int x=-12;x<=12;x++)for(int y=-12;y<=12;y++)
            {
                Vector2 candidate=origin+new Vector2(x*6f,y*6f);
                if(candidate.x<0||candidate.y<0||candidate.x+pinSize.x>mapSize.x||candidate.y+pinSize.y>mapSize.y)continue;
                float distance=(candidate-origin).sqrMagnitude;if(distance>=bestDistance)continue;
                var bounds=new Rect(candidate-new Vector2(2,2),pinSize+new Vector2(4,4));bool blocked=false;
                foreach(Rect other in occupied)if(bounds.Overlaps(other)){blocked=true;break;}
                if(!blocked){best=candidate;bestDistance=distance;}
            }
            occupied.Add(new Rect(best,pinSize));return best;
        }
        private void SelectRegion(ExpeditionRegion region)
        {
            selectedRegion=region.id;Refresh();
        }
        private void LayoutMapPins()
        {
            Vector2 size=mapArea.rect.size;
            bool changed=!pinsPlaced||placedMapSize!=size;
            for(int i=0;i<pins.Count;i++)
            {
                var rect=(RectTransform)pins[i].Value.transform;
                if(pinPositions[i]!=pins[i].Key.mapPosition||pinPlacements[i].size!=rect.rect.size
                    ||pinPlacements[i].position!=rect.anchoredPosition){changed=true;break;}
            }
            if(!changed)return;
            pinsPlaced=true;placedMapSize=size;mapGraphic.rectTransform.sizeDelta=size;
            var occupied=new List<Rect>();
            for(int i=0;i<pins.Count;i++)
            {
                pinPositions[i]=pins[i].Key.mapPosition;
                Vector2 position=mapGraphic.Project(pinPositions[i]);
                var rect=(RectTransform)pins[i].Value.transform;Vector2 pinSize=rect.rect.size;
                Vector2 desired=new Vector2(position.x*size.x-pinSize.x/2,(1-position.y)*size.y-pinSize.y/2);
                rect.anchoredPosition=TopLeft(PlacePin(size,desired,pinSize,occupied));
                pinPlacements[i]=new Rect(rect.anchoredPosition,pinSize);
            }
        }
        private void RefreshRegionChoices()
        {
            LayoutMapPins();
            foreach(var pin in pins)
            {
                var region=pin.Key;
                pin.Value.GetComponent<Image>().color=region.id==selectedRegion?new Color(.45f,.37f,.17f)
                    :progression.HasAccess(region.id)?new Color(.16f,.4f,.29f):new Color(.13f,.21f,.25f);
            }
        }
        private static Vector2 TopLeft(Vector2 value)=>new Vector2(value.x,-value.y);
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
                researchStatus.text=Loc.Format("park.progression_presentation.funds_researchers_work_speed_x", economy.Balance, home.HiredCount, home.Footprint.Definition.staffCapacity, progression.StaffEfficiency, (p==null?Loc.Text("park.progression_presentation.choose_a_technology_below_creature_care_research_remains_a")
                    :Loc.Format("park.progression_presentation.researcher_seconds_left", p.Definition.DisplayName, 100*p.Work/p.Definition.workSeconds, Mathf.CeilToInt(p.Definition.workSeconds-p.Work))), (home.IsOpen?"":Loc.Text("park.progression_presentation.paused_reconnect_the_research_centre_s_entrance_path")));
                // Affordability and prerequisites can change while this window remains open.
                string signature=Loc.Revision+":"+home.RuntimeId+":"+progression.Revision+":"+home.IsOpen+":"+home.HiredCount;
                foreach(var t in progression.Rules.technologies)signature+=economy.CanAfford(t.cost)?"1":"0";
                if(signature!=techSignature){techSignature=signature;RebuildTechnology();}
            }
        }
        private string techSignature;
        private void RefreshMap()
        {
            RefreshRegionChoices();
            teamCount=Mathf.Clamp(teamCount,1,Math.Max(1,progression.AvailableTeams));
            bool open=progression.CanUse(home,BuildingKind.ExpeditionCentre);
            teamsText.text=Loc.Format("park.progression_presentation.teams_available_funds", progression.Teams, progression.Rules.maximumTeams, progression.AvailableTeams, economy.Balance, (open?Loc.Text("park.progression_presentation.more_teams_improve_success"):Loc.Text("park.progression_presentation.reconnect_the_centre_s_path")));
            buyTeam.GetComponentInChildren<Text>().text=Loc.Format("park.progression_presentation.buy_team_2", progression.NextTeamCost);
            buyTeam.interactable=open&&progression.Teams<progression.Rules.maximumTeams&&economy.CanAfford(progression.NextTeamCost);
            var region=progression.Region(selectedRegion);var d=progression.Difficulty(selectedDifficulty);bool available=progression.RegionHasCreatures(selectedRegion);
            if (region == null || d == null)
            {
                regionText.text = Loc.Text("park.progression_presentation.assign_at_least_one_location_and_mission_difficulty_in_the");
                missionPreview.text = Loc.Text("park.progression_presentation.no_mission_configured");
                buyRegion.interactable = false; send.interactable = false;
                return;
            }
            bool access=progression.HasAccess(selectedRegion);
            regionText.text=Loc.Format("park.progression_presentation.format", region.DisplayName, (available?Loc.Format("park.progression_presentation.possible_creatures", (access?Loc.Text("park.progression_presentation.expedition_permit_owned"):Loc.Format("park.progression_presentation.expedition_permit", region.accessCost)), Names(region.species)):Loc.Text("park.progression_presentation.no_creature_species_assigned_yet_this_location_cannot_be_p")));
            buyRegion.GetComponentInChildren<Text>().text=access?Loc.Text("park.progression_presentation.location_unlocked"):Loc.Format("park.progression_presentation.buy_location_2", region.accessCost.ToString("0"));
            buyRegion.interactable=open&&available&&!access&&economy.CanAfford(region.accessCost);
            foreach(var choice in difficulties)choice.Value.GetComponent<Image>().color=choice.Key.id==selectedDifficulty?new Color(.45f,.37f,.17f):new Color(.18f,.28f,.31f);
            var eligible=progression.Eligible(selectedRegion,selectedDifficulty);
            missionPreview.text=Loc.Format("park.progression_presentation.teams_assigned_success_time_s_cost_rating_5", teamCount, 100*progression.Chance(selectedDifficulty,teamCount), progression.MissionSeconds(d), teamCount*d.costPerTeam, d.minimumRating, d.maximumRating, (eligible.Count>0?Loc.Format("park.progression_presentation.on_success_of_one_species_candidates", d.creaturesFound, Names(eligible)):Loc.Text("park.progression_presentation.no_eligible_creatures_for_this_difficulty")));
            send.interactable=open&&access&&eligible.Count>0&&progression.AvailableTeams>=teamCount&&economy.CanAfford(teamCount*d.costPerTeam);
            var text=new StringBuilder();foreach(var m in progression.Missions)
                text.Append(progression.Region(m.region).DisplayName).Append(" - ").Append(progression.Difficulty(m.difficulty).DisplayName)
                    .Append("\n").Append(m.teams).Append(Loc.Text("park.progression_presentation.team_s")).Append(Mathf.CeilToInt(m.remaining)).Append(Loc.Text("park.progression_presentation.s_left")).Append((100*m.chance).ToString("0")).Append(Loc.Text("park.progression_presentation.success"));
            missionLog.text=text.Length==0?Loc.Text("park.progression_presentation.no_active_expeditions_teams_return_after_every_mission"):text.ToString();
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
                var details=ManagementWidgets.Label(row.transform,Loc.Format("park.progression_presentation.cost_work_researcher_seconds", t.Description, t.cost, t.workSeconds, (reason==""?"":Loc.Format("park.progression_presentation.format_2", reason))),
                    new Vector2(10,39),new Vector2(techWidth-225,0),17);
                float detailsHeight=Mathf.Ceil(details.preferredHeight),rowHeight=Mathf.Max(86,47+detailsHeight);
                details.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,detailsHeight);
                ((RectTransform)row.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,rowHeight);
                var button=ManagementWidgets.Button(row.transform,progression.Completed(t.id)?"park.progression_presentation.completed":p?.Definition.id==t.id?"park.progression_presentation.researching":"park.progression_presentation.start_research",
                    new Vector2(techWidth-195,(rowHeight-42)/2),new Vector2(180,42),()=>{staff.StartTechnology(home,t.id);techSignature=null;Refresh();},17);
                button.interactable=reason=="";y+=rowHeight+12;
            }
            technologyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(0,y-12));
            UIOverflowScroll.Attach(technologyContent.GetComponentInParent<ScrollRect>()).Refresh();
        }
        public void Dispose(){UnityEngine.Object.Destroy(map);UnityEngine.Object.Destroy(research);}
    }
}
