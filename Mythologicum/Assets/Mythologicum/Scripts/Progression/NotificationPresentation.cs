using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    public sealed class NotificationPresentation : IDisposable
    {
        private readonly GameObject letter, panel;
        private readonly RectTransform content, body;
        private readonly UIFramedPanel frame;
        private readonly Button markAll, unreadFilter, close;
        private readonly NotificationInbox inbox;
        private readonly Action<Vector2Int> focus;
        private readonly Text mailLabel;
        private readonly Action opened;
        private int lastUnread=-1;
        private int revision=-1;
        private bool onlyUnread;
        private float bodyWidth=-1;
        public bool IsOpen => panel.activeSelf;
        public NotificationPresentation(Canvas canvas,NotificationInbox inbox,Action<Vector2Int> focus,Action opened=null)
        {
            this.inbox=inbox;this.focus=focus;this.opened=opened;
            var button=ManagementWidgets.Button(canvas.transform,"Letters",Vector2.zero,new Vector2(64,52),Toggle);letter=button.gameObject;
            var r=(RectTransform)letter.transform;r.anchorMin=r.anchorMax=new Vector2(0,0);r.pivot=new Vector2(.5f,.5f);
            r.anchoredPosition=new Vector2(266,90);r.sizeDelta=new Vector2(64,64);
            mailLabel=letter.GetComponentInChildren<Text>();mailLabel.text="Mail";
            UIIcon.AttachStandalone(button,UIIconKind.Mail);
            UILayoutElement.Bind(r,UILayoutBinding.MailIcon);
            panel=ManagementWidgets.Box(canvas.transform,"Notification inbox",Vector2.zero,Vector2.one);
            r=(RectTransform)panel.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,0);r.anchoredPosition=new Vector2(24,210);
            frame=UIFramedPanel.Attach(panel,false,720);body=frame.Content;
            var heading=ManagementWidgets.Label(frame.Header,"Letters",Vector2.zero,frame.Header.rect.size,24);
            heading.alignment=TextAnchor.MiddleCenter;
            float width=body.rect.width,height=body.rect.height;
            markAll=ManagementWidgets.Button(body,"Mark all read",Vector2.zero,new Vector2(180,36),()=>{inbox.MarkAllRead();Refresh();},16);
            unreadFilter=ManagementWidgets.Button(body,"Filter unread letters",new Vector2(188,0),new Vector2(140,36),()=>
            {onlyUnread=!onlyUnread;revision=-1;Refresh();},16);
            close=ManagementWidgets.Button(body,"Close",new Vector2(width-84,0),new Vector2(84,36),Close,16);
            content=ManagementWidgets.Scroll(body,"Letters - scroll to read",new Vector2(0,46),new Vector2(width,height-46),0);
            panel.SetActive(false);Refresh();
        }
        public void Toggle() {if(IsOpen){Close();return;}opened?.Invoke();panel.SetActive(true);panel.transform.SetAsLastSibling();revision=-1;Refresh();UIThemeApplicator.ApplyAll(panel.GetComponentInParent<Canvas>());}
        public void Close()=>panel.SetActive(false);
        public void Refresh()
        {
            if(lastUnread!=inbox.UnreadCount)
            {
                lastUnread=inbox.UnreadCount;
                mailLabel.text=lastUnread>0?$"Mail ({lastUnread} unread)":"Mail";
                UIIcon.AttachStandalone(letter.GetComponent<Button>(),lastUnread>0?UIIconKind.MailUnread:UIIconKind.Mail);
            }
            if(!IsOpen)return;
            frame.Refresh();
            float width=body.rect.width;
            bool resized=Mathf.Abs(bodyWidth-width)>.1f;bodyWidth=width;
            Place(markAll,0,Mathf.Min(180,width*.38f));
            Place(unreadFilter,Mathf.Min(180,width*.38f)+8,Mathf.Min(140,width*.28f));
            Place(close,width-Mathf.Min(84,width*.2f),Mathf.Min(84,width*.2f));
            var scroll=(RectTransform)content.parent.parent;
            scroll.sizeDelta=new Vector2(width,Mathf.Max(0,body.rect.height-46));
            markAll.interactable=inbox.UnreadCount>0;
            unreadFilter.GetComponentInChildren<Text>().text=onlyUnread?"Unread only":"All letters";
            if(!resized&&revision==inbox.Revision)return;revision=inbox.Revision;ManagementWidgets.Clear(content);
            float y=0;
            for(int i=inbox.Entries.Count-1;i>=0;i--)
            {
                var n=inbox.Entries[i];if(onlyUnread&&n.read)continue;
                string severity=n.severity==ParkNoticeSeverity.Critical?"CRITICAL: ":n.severity==ParkNoticeSeverity.Warning?"WARNING: ":"";
                string title=(n.read?"":"NEW: ")+severity+n.title+(n.count>1?" ("+n.count+")":"");
                var row=ManagementWidgets.Button(content,title,new Vector2(0,y),new Vector2(content.rect.width-14,80),()=>{inbox.MarkRead(n.id);if(n.hasLocation){focus(n.location);Close();}Refresh();},17);
                var text=row.GetComponentInChildren<Text>();text.alignment=TextAnchor.UpperLeft;text.text=title+"\n"+n.body+(n.hasLocation?"\nClick to view location.":"");
                float rowHeight=Mathf.Max(76,text.preferredHeight+16);
                ((RectTransform)row.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,rowHeight);
                y+=rowHeight+8;
            }
            if(y==0){ManagementWidgets.Label(content,onlyUnread?"No unread letters.":"No letters yet.",new Vector2(8,5),new Vector2(content.rect.width-30,35));y=44;}
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,y);
            UIOverflowScroll.Attach(content.GetComponentInParent<ScrollRect>()).Refresh();
        }
        private static void Place(Button button,float x,float width)
        {
            var rect=(RectTransform)button.transform;rect.anchoredPosition=new Vector2(x,0);rect.sizeDelta=new Vector2(width,36);
        }
        public void Dispose(){UnityEngine.Object.Destroy(letter);UnityEngine.Object.Destroy(panel);}
    }
}
