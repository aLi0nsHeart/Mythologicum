using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    internal static class ManagementWidgets
    {
        internal static Font Font => UITheme.BodyFont;
        internal static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(Loc.InternalName(name),typeof(RectTransform));go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);
            rect.anchoredPosition=new Vector2(position.x,-position.y);rect.sizeDelta=size;return rect;
        }
        internal static GameObject Box(Transform parent,string name,Vector2 position,Vector2 size,Color? color=null)
        {
            var rect=Rect(parent,name,position,size);var image=rect.gameObject.AddComponent<Image>();
            image.color=color??new Color(.04f,.075f,.09f,.99f);return rect.gameObject;
        }
        internal static Text Label(Transform parent,string name,Vector2 position,Vector2 size,int fontSize=18)
        {
            var rect=Rect(parent,name,position,size);var text=rect.gameObject.AddComponent<Text>();
            if (Loc.Has(name)) Loc.Set(text,name); else text.text=name;
            text.font=Font;text.fontSize=fontSize;text.color=new Color(.94f,.88f,.72f);
            text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;
            if(parent.name=="Window content"&&position.y<30&&fontSize>=24)
            {
                var frame=parent.GetComponentInParent<UIFramedPanel>(true);
                rect.SetParent(frame.Header,false);rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
                rect.offsetMin=new Vector2(8,4);rect.offsetMax=new Vector2(-8,-4);
                text.alignment=TextAnchor.MiddleCenter;
            }
            return text;
        }
        internal static Button Button(Transform parent,string name,Vector2 position,Vector2 size,Action click,int fontSize=18)
        {
            var go=Box(parent,name,position,size,new Color(.18f,.28f,.31f));var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
            button.navigation=new Navigation {mode=Navigation.Mode.None};button.onClick.AddListener(()=>click());
            var label=Label(go.transform,name,new Vector2(5,3),size-new Vector2(10,6),fontSize);label.alignment=TextAnchor.MiddleCenter;
            if(parent.name=="Window content"&&Loc.InternalName(name).StartsWith("Close",StringComparison.Ordinal))
            {
                var frame=parent.GetComponentInParent<UIFramedPanel>(true);
                var rect=(RectTransform)go.transform;rect.SetParent(frame.transform,false);
                rect.anchorMin=rect.anchorMax=new Vector2(.91f,.86f);rect.pivot=new Vector2(1,.5f);
                rect.anchoredPosition=Vector2.zero;
            }
            return button;
        }
        internal static RectTransform Scroll(Transform parent,string name,Vector2 position,Vector2 size,float contentHeight)
        {
            var go=Box(parent,name,position,size,new Color(.055f,.095f,.11f));var scroll=go.AddComponent<ScrollRect>();
            var view=Box(go.transform,"Viewport",Vector2.zero,size,new Color(0,0,0,.001f));
            var vr=(RectTransform)view.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=vr.offsetMax=Vector2.zero;
            view.AddComponent<RectMask2D>();
            var content=Rect(view.transform,"Content",Vector2.zero,new Vector2(size.x,contentHeight));
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.sizeDelta=new Vector2(0,contentHeight);
            scroll.viewport=vr;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=25;
            scroll.movementType=ScrollRect.MovementType.Clamped;UIOverflowScroll.Attach(scroll);return content;
        }
        internal static GameObject Modal(Canvas canvas,string name)
        {
            var root=Box(canvas.transform,name,Vector2.zero,Vector2.zero,new Color(.015f,.03f,.04f,.95f));
            var rect=(RectTransform)root.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            root.SetActive(false);return root;
        }
        internal static RectTransform Card(GameObject root,Canvas canvas)
        {
            var card=Rect(root.transform,"Window",Vector2.zero,Vector2.one);
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);
            var image=card.gameObject.AddComponent<Image>();image.color=new Color(.07f,.045f,.025f,1f);
            var frame=UIFramedPanel.Attach(card.gameObject,false,1400f);
            var content=Rect(frame.Content,"Window content",Vector2.zero,new Vector2(1160,500));
            frame.SetLogicalContent(content,new Vector2(1160,500));frame.Refresh();
            return content;
        }
        internal static void Clear(Transform root)
        {for(int i=root.childCount-1;i>=0;i--){var go=root.GetChild(i).gameObject;go.SetActive(false);UnityEngine.Object.Destroy(go);}}
    }
}
