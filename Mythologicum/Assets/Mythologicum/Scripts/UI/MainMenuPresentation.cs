using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mythologicum
{
    /// <summary>The exhibition's front door. Add actions to CreateActions without changing the layout.</summary>
    public sealed class MainMenuPresentation : MonoBehaviour
    {
        private sealed class MenuAction
        {
            public readonly string Name; public readonly Action Execute; public readonly Func<bool> Enabled;
            public MenuAction(string name, Action execute, Func<bool> enabled = null) { Name = name; Execute = execute; Enabled = enabled; }
        }
        private readonly List<KeyValuePair<MenuAction, Button>> actions = new List<KeyValuePair<MenuAction, Button>>();
        private float refreshAt;
        public Text Message { get; private set; }
        public IReadOnlyList<Button> MenuButtons { get { var result = new List<Button>(); foreach (var item in actions) result.Add(item.Value); return result; } }

        public static MainMenuPresentation Create(Canvas canvas, SceneNavigation navigation, Action load)
        {
            if (canvas == null || navigation == null) throw new ArgumentNullException("The main menu requires a canvas and scene navigation.");
            foreach (Transform child in canvas.transform)
            {
                // Scene navigation can be authored beneath the canvas; keep its behaviour alive.
                if (child.GetComponentInChildren<SceneNavigation>(true) != null)
                {
                    foreach (var graphic in child.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
                    foreach (var selectable in child.GetComponentsInChildren<Selectable>(true)) selectable.interactable = false;
                }
                else child.gameObject.SetActive(false);
            }
            Canvas.ForceUpdateCanvases();
            var root = ManagementWidgets.Modal(canvas, "Main menu");
            UIThemeElement.Mark(root, UIThemeRole.Overlay);
            var result = root.AddComponent<MainMenuPresentation>(); result.Build(canvas, navigation, load);
            root.SetActive(true); root.transform.SetAsFirstSibling(); result.RefreshSaveAvailability(); UIThemeApplicator.ApplyAll(canvas); return result;
        }

        private static List<MenuAction> CreateActions(SceneNavigation navigation, Action load) => new List<MenuAction>
        {
            new MenuAction("New Game", navigation.StartGame),
            new MenuAction("Load Game", load, () => ParkSaveStore.Exists),
            new MenuAction("Settings", navigation.OpenSettings),
            new MenuAction("Quit", navigation.QuitGame)
        };

        private void Build(Canvas canvas, SceneNavigation navigation, Action load)
        {
            var backdrop = ManagementWidgets.Rect(transform, "Exhibition background", Vector2.zero, Vector2.zero);
            backdrop.anchorMin = Vector2.zero; backdrop.anchorMax = Vector2.one; backdrop.offsetMin = backdrop.offsetMax = Vector2.zero;
            var background = backdrop.gameObject.AddComponent<RawImage>();
            background.texture = UITheme.Current != null ? UITheme.Current.background : null;
            background.color = background.texture != null ? Color.white : new Color(.11f, .075f, .04f);
            background.raycastTarget = false;
            var canvasRect = (RectTransform)canvas.transform;
            background.uvRect = new Rect(0, 0, canvasRect.rect.width / Mathf.Max(1f, canvasRect.rect.height), 1);
            UIThemeElement.Mark(backdrop.gameObject, UIThemeRole.Ignore);
            var card = ManagementWidgets.Card(gameObject, canvas);
            UIThemeElement.Mark(card.gameObject, UIThemeRole.Window);
            var title = ManagementWidgets.Label(card, "Main menu title", new Vector2(30, 8), new Vector2(1100, 62), 53);
            title.text = "MYTHOLOGICUM"; title.alignment = TextAnchor.MiddleCenter;
            UIThemeElement.Mark(title.gameObject, UIThemeRole.Title);
            var subtitle = ManagementWidgets.Label(card, "Main menu subtitle", new Vector2(30, 70), new Vector2(1100, 42), 29);
            subtitle.text = "The Grand Exhibition"; subtitle.alignment = TextAnchor.MiddleCenter;
            UIThemeElement.Mark(subtitle.gameObject, UIThemeRole.Heading);
            var content = ManagementWidgets.Scroll(card, "Main menu actions", new Vector2(313, 126), new Vector2(534, 282), 0);
            var actionBackground = content.GetComponentInParent<ScrollRect>(true).GetComponent<Image>();
            actionBackground.color = Color.clear;
            UIThemeElement.Mark(actionBackground.gameObject, UIThemeRole.Ignore);
            var entries = CreateActions(navigation, load);
            float y = 6;
            foreach (var action in entries)
            {
                var item = action;
                var button = ManagementWidgets.Button(content, item.Name, new Vector2(10, y), new Vector2(500, 56), () =>
                {
                    if (SettingsPresentation.IsOpen || NewGameSetup.IsOpen || SaveBrowserPresentation.IsOpen || !(item.Enabled?.Invoke() ?? true)) return;
                    item.Execute();
                }, 24);
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                actions.Add(new KeyValuePair<MenuAction, Button>(item, button)); y += 68;
            }
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, y);
            Message = ManagementWidgets.Label(card, "Main menu message", new Vector2(130, 430), new Vector2(900, 56), 19);
            Message.text = ""; Message.alignment = TextAnchor.MiddleCenter;
        }

        public void RefreshSaveAvailability()
        {
            foreach (var item in actions) item.Value.interactable = item.Key.Enabled?.Invoke() ?? true;
        }
        private void Update()
        {
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + .5f; RefreshSaveAvailability();
        }
        private void OnApplicationFocus(bool focused) { if (focused) RefreshSaveAvailability(); }
    }
}
