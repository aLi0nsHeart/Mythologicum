using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    internal static class ManagementWidgets
    {
        internal static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        internal static RectTransform Rect(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
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
            text.text=name;text.font=Font;text.fontSize=fontSize;text.color=new Color(.94f,.88f,.72f);
            text.alignment=TextAnchor.UpperLeft;text.raycastTarget=false;return text;
        }
        internal static Button Button(Transform parent,string name,Vector2 position,Vector2 size,Action click,int fontSize=18)
        {
            var go=Box(parent,name,position,size,new Color(.18f,.28f,.31f));var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();
            button.navigation=new Navigation {mode=Navigation.Mode.None};button.onClick.AddListener(()=>click());
            var label=Label(go.transform,name,new Vector2(5,3),size-new Vector2(10,6),fontSize);label.alignment=TextAnchor.MiddleCenter;
            return button;
        }
        internal static RectTransform Scroll(Transform parent,string name,Vector2 position,Vector2 size,float contentHeight)
        {
            var go=Box(parent,name,position,size,new Color(.055f,.095f,.11f));var scroll=go.AddComponent<ScrollRect>();
            var view=Box(go.transform,"Viewport",Vector2.zero,size);
            var vr=(RectTransform)view.transform;vr.anchorMin=Vector2.zero;vr.anchorMax=Vector2.one;vr.offsetMin=vr.offsetMax=Vector2.zero;
            view.AddComponent<RectMask2D>();
            var content=Rect(view.transform,"Content",Vector2.zero,new Vector2(size.x,contentHeight));
            content.anchorMin=new Vector2(0,1);content.anchorMax=new Vector2(1,1);content.sizeDelta=new Vector2(0,contentHeight);
            scroll.viewport=vr;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.scrollSensitivity=25;
            scroll.movementType=ScrollRect.MovementType.Clamped;return content;
        }
        internal static GameObject Modal(Canvas canvas,string name)
        {
            var root=Box(canvas.transform,name,Vector2.zero,Vector2.zero,new Color(.015f,.03f,.04f,.95f));
            var rect=(RectTransform)root.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            root.SetActive(false);return root;
        }
        internal static RectTransform Card(GameObject root,Canvas canvas)
        {
            var parent=(RectTransform)canvas.transform;
            var card=Rect(root.transform,"Window",Vector2.zero,new Vector2(1160,660));
            card.localScale=Vector3.one*Mathf.Min(1f,(parent.rect.width-30)/1160,(parent.rect.height-30)/660);
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);return card;
        }
        internal static void Clear(Transform root)
        {for(int i=root.childCount-1;i>=0;i--){var go=root.GetChild(i).gameObject;go.SetActive(false);UnityEngine.Object.Destroy(go);}}
    }
}
