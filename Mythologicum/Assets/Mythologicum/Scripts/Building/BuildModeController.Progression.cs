using System.Collections.Generic;
using UnityEngine;
namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        public ParkProgression Progression { get; private set; }
        private ProgressionPresentation management;
        private NotificationPresentation notifications;
        public bool ManagementOpen => management?.IsOpen ?? false;
        private void InitializeProgression()
        {
            var definition=Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
            var ratings=new Dictionary<string,int>();foreach(var creature in creatureCatalog)ratings[creature.name]=creature.rating;
            Progression=new ParkProgression(definition!=null?definition.rules:new ProgressionRules(),economy,buildingConstruction.Network,ratings,
                ()=>Random.value,id=>Species(id)?.displayName??id);
            staffWorld.Progression=Progression;
            fences.Layout.Opened+=edge=>{
                if(!creatureWorld.RestoringSave)Progression.Inbox.Post("opened-fence",ParkNoticeKind.FenceOpened,"Fence opened",
                    "A fence segment was removed or destroyed. Check containment and rebuild the gap if necessary.",edge.CellA);
            };
            staffWorld.Research.Finished+=species=>{
                if(!creatureWorld.RestoringSave)Progression.Inbox.Post("creature-study:"+species.name,ParkNoticeKind.ResearchFinished,
                    "Creature research finished: "+species.displayName,"All care notes for this species have been discovered.");
            };
        }
        private void CreateManagementUI()
        {
            Canvas.ForceUpdateCanvases();var canvas=FindFirstObjectByType<Canvas>();
            management=new ProgressionPresentation(canvas,Progression,staffWorld,economy,id=>Species(id)?.displayName??id);
            notifications=new NotificationPresentation(canvas,Progression.Inbox,cell=>{
                management.Close();SelectTool(Tool.None);GetComponent<IsometricCameraController>()?.FocusOn(grid.CellCenter(cell));
            });
            palette.ConfigureProgression(Progression);
            buildingInspection.ConfigureManagement(OpenExpeditions,OpenTechnologies);
        }
        private void OpenExpeditions(ParkBuildingRecord home)
        {SelectTool(Tool.None);notifications.Close();management.OpenExpeditions(home);}
        private void OpenTechnologies(ParkBuildingRecord home)
        {SelectTool(Tool.None);notifications.Close();management.OpenResearch(home);}
        public bool CloseManagementWindow()
        {
            if(ManagementOpen){management.Close();return true;}
            if(notifications?.IsOpen==true){notifications.Close();return true;}
            return false;
        }
    }
}
