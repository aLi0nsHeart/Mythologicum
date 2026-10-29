using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    public sealed class NotificationPresentation : IDisposable
    {
        private readonly GameObject letter, badge, panel;
        private readonly RectTransform content;
        private readonly NotificationInbox inbox;
        private readonly Action<Vector2Int> focus;
        private int revision=-1;
        public bool IsOpen => panel.activeSelf;
        public NotificationPresentation(Canvas canvas,NotificationInbox inbox,Action<Vector2Int> focus)
        {
            this.inbox=inbox;this.focus=focus;
            var button=ManagementWidgets.Button(canvas.transform,"Letters",Vector2.zero,new Vector2(64,52),Toggle);letter=button.gameObject;
            var r=(RectTransform)letter.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,0);r.anchoredPosition=new Vector2(-18,86);
            letter.GetComponentInChildren<Text>().text="";
            var icon=ManagementWidgets.Rect(letter.transform,"Envelope",new Vector2(12,13),new Vector2(40,26)).gameObject.AddComponent<EnvelopeGraphic>();
            icon.color=new Color(.95f,.88f,.7f);icon.raycastTarget=false;
            badge=ManagementWidgets.Box(letter.transform,"Unread notifications",new Vector2(45,-7),new Vector2(24,25),new Color(.8f,.19f,.1f));
            var badgeText=ManagementWidgets.Label(badge.transform,"!",Vector2.zero,new Vector2(24,25),22);badgeText.alignment=TextAnchor.MiddleCenter;
            float width=460,height=Mathf.Min(430,((RectTransform)canvas.transform).rect.height-190);
            panel=ManagementWidgets.Box(canvas.transform,"Notification inbox",Vector2.zero,new Vector2(width,height));
            r=(RectTransform)panel.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(1,0);r.anchoredPosition=new Vector2(-18,150);
            ManagementWidgets.Label(panel.transform,"Letters",new Vector2(12,12),new Vector2(160,35),22);
            ManagementWidgets.Button(panel.transform,"Mark all read",new Vector2(174,8),new Vector2(180,35),()=>{inbox.MarkAllRead();Refresh();},16);
            ManagementWidgets.Button(panel.transform,"Close",new Vector2(364,8),new Vector2(84,35),Close,16);
            content=ManagementWidgets.Scroll(panel.transform,"Letters - scroll to read",new Vector2(10,53),new Vector2(width-20,height-63),0);
            panel.SetActive(false);Refresh();
        }
        public void Toggle() {if(IsOpen){Close();return;}panel.SetActive(true);panel.transform.SetAsLastSibling();inbox.MarkAllRead();revision=-1;Refresh();}
        public void Close()=>panel.SetActive(false);
        public void Refresh()
        {
            badge.SetActive(inbox.UnreadCount>0);
            if(!IsOpen||revision==inbox.Revision)return;revision=inbox.Revision;ManagementWidgets.Clear(content);
            float y=0;
            for(int i=inbox.Entries.Count-1;i>=0;i--)
            {
                var n=inbox.Entries[i];string title=(n.read?"":"NEW: ")+n.title+(n.count>1?" ("+n.count+")":"");
                var row=ManagementWidgets.Button(content,title,new Vector2(0,y),new Vector2(440,112),()=>{inbox.MarkRead(n.id);if(n.hasLocation){focus(n.location);Close();}Refresh();},17);
                var text=row.GetComponentInChildren<Text>();text.alignment=TextAnchor.UpperLeft;text.text=title+"\n"+n.body+(n.hasLocation?"\nClick to view location.":"");
                y+=120;
            }
            if(y==0){ManagementWidgets.Label(content,"No letters yet.",new Vector2(8,5),new Vector2(420,45));y=55;}
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,y);
        }
        public void Dispose(){UnityEngine.Object.Destroy(letter);UnityEngine.Object.Destroy(panel);}
    }
}
