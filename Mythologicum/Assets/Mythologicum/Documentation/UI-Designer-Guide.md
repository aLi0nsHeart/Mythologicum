# UI designer guide

Use **Mythologicum > UI > Designer Tools** in Unity's top menu. This window links
to the active appearance and layout assets and lets you save exceptions for a
selected UI control. No C# changes are needed for these adjustments.

## Where to change things

| What you want to change | Where |
| --- | --- |
| Fonts, general text size, headings, button captions | **Select Active Theme > Typography** |
| Text colours, disabled/warning colours, backgrounds, opacity, stars | **Theme > Colours** |
| Panel artwork, buttons, input fields, sliders and scrollbars | **Theme > Artwork** |
| Icon artwork, icon colour/spacing and the cursor | **Theme > Icons** |
| The appearance of one particular label | **Designer Tools**, or **Theme > Overrides** |
| Panel widths, screen positions, anchors, margins and frame safe areas | **Select Layout Settings > Panels** |
| Bottom dock positions/sizes, money/stars, construction flyout rows and gaps | **Layout > HUD & menus** |
| Scroll speed, scrollbar width, hover delay and tooltip dimensions | **Layout > Scroll & hover** |
| The position or size of one particular control | **Designer Tools**, or **Layout > Individual controls** |
| Which theme the game uses | **Layout > Active Theme** |

The default assets are:

- `Assets/Mythologicum/UI/Resources/SteampunkTheme.asset`
- `Assets/Mythologicum/UI/Resources/ExhibitionLayout.asset`

**Select Active Theme** follows the Active Theme reference, so it also works if
you have switched to a duplicate theme. The existing theme's saved fonts, colours
and artwork are preserved.

## Change fonts, sizes and colours

1. Choose **Mythologicum > UI > Select Active Theme**.
2. Open **Typography**. The default fonts cover body text, headings and titles.
3. Expand a text category: **Body Text**, **Heading Text**, **Title Text**,
   **Button Text**, **Input Text**, **Small Text**, or **Hud Text**.
4. Enter a positive **Font Size**, such as `20`, to give that category a fixed
   base size. `0` keeps each label's original designed size. **Size Scale** changes
   a category proportionally; **Global Text Scale** changes all categories.
5. For a category-specific colour, enable **Override Color** in that category.
   To change the shared palette instead, open **Colours > Text palette**.
6. Save the asset with the theme's **Save theme asset** button or save the project.

An empty font in a category inherits the default font. Category settings can also
control line spacing, font style, alignment and text fitting. **Fixed Size** turns
off automatic shrinking; **Shrink To Fit** allows captions to shrink within their
controls. A larger font may need a larger control or may cause a report to scroll.

With **Preserve Status Colors** enabled, warning, positive and disabled states use
the shared danger/accent/muted colours. Disable it when your custom text colours
should take precedence over those states.

Shadows and outlines have their own toggles, colours and offsets. HUD outlines
are separate from ordinary text outlines. Background colour fields include alpha:
lowering alpha makes a surface more transparent.

## Adjust one label or control

1. Start Play mode and open the screen you want to adjust.
2. Select the UI object in the **Hierarchy**. Select the object containing a
   **Text** component to edit its typography. You can pause the Unity Editor to
   keep the game still while editing.
3. Open **Mythologicum > UI > Designer Tools**. Its matching path identifies the
   selected object.
4. Edit the **Saved text exception** fields. For example, enable **Custom colour**
   and choose a colour, or set **Size** to `22`. Changes save to the theme asset
   and preview immediately, including while the Editor is paused.
5. For geometry, enable **Override position** or **Override size** and enter the
   saved values in the same window. These save to the layout asset. Editing these
   saved fields works even when a generated screen normally controls its layout.
6. Stop Play mode and start it again to confirm the saved result.

Illustrated panels expose **Logical panel width** and an optional position rather
than an independent width/height pair. Their source image proportions stay intact.
To adjust the artwork, lining or body safe area, use the layout asset instead of
editing the generated `Panel artwork` child.

**Save current visible text appearance** records the selected label's currently
resolved font, colour and other text settings. **Save selected position and size**
records the current RectTransform; this is useful after directly moving a control
while the Editor is paused. Use the removal buttons to return that control to its
shared settings. These actions support Unity Undo.

Generated Hierarchy objects are recreated when Play mode ends. Direct changes to
their Text or RectTransform components alone do not persist and may be replaced
by the running layout. Save them through Designer Tools, or edit the assets.
For authored scene/prefab labels, `UIThemeElement` also offers a local appearance
override; save that scene/prefab normally. A local override takes precedence over
theme exceptions.

## Matching and precedence

Individual exceptions can match an exact object name, such as `Park funds`, or a
hierarchy suffix, such as `Building information/Panel content/Building name`.
Designer Tools captures a specific path automatically. **Screen Name** can limit
a rule to one ancestor panel or Unity scene. Later matching rules take precedence.
Use stable names when making rules for dynamically created rows.

Text resolves in this order: screen's original design, shared category, matching
theme exception, then an authored local override. Global text scale is applied
last; preserved status colours still apply if enabled.

Geometry resolves from the screen/HUD layout, then an individual control override.
Native frame parts use the panel and safe-area settings to retain their proportions.

## Panels and screen sizes

Expand a named entry in **Layout > Panels**. Built-in entries include buildings,
creatures, visitors, enclosures, mail, terrain tools, Pause, confirmations, the main
menu, Settings, map setup, park management, encyclopedia, expeditions and research.

- **Preferred Width** is the logical width before fitting to the screen. `0` uses
  the screen's original width. **Width Scale** multiplies that width.
- Turn on **Override Position** to use the saved position.
- Turn on **Override Anchor** to change which screen point anchors the panel.
  `(0,0)` is bottom left, `(1,1)` is top right, and `(0.5,0.5)` is the centre.
- Safe-area fields are fractions: `0.1` reserves ten percent of that edge. Keep
  body text clear of the artwork's ornament.
- Panels automatically shrink to remain inside the screen margin. At a small
  resolution, increasing logical width may leave the visible width unchanged
  because the panel has already reached its maximum fitting height.

Bottom dock positions use positive Y upwards. Funds Position is relative to the
top-left corner, with negative Y downwards. Stars Position is an inset inside that
display, with positive Y downwards. Individual RectTransform positions follow the
selected object's existing anchors.

Scrolling remains disabled while content fits. Scroll settings change genuine
overflow behaviour; they do not force short reports to scroll.

The dark panel backing automatically follows the frame artwork's enclosed inner
outline, including curved corners and the gaps beside ornaments. Its colour and
transparency remain under **Theme > Colours > Panel Lining Color**. In
**Layout > Panels**, leave **Match Frame Backing To Artwork** enabled. **Frame
Backing Overlap Pixels** extends the backing slightly underneath the frame to hide
seams; the default is two source-image pixels. These controls do not move the text.
The vertical/horizontal **Lining Insets** apply only when automatic matching is
disabled or replacement artwork has no enclosed interior.

## Variants, saving and repairs

Duplicate the theme asset in the Project window, rename the duplicate, then assign
it to **ExhibitionLayout > Active Theme** to make a new visual variant. Leave Active
Theme empty to fall back to `Resources/SteampunkTheme`. The layout itself is loaded
from `Resources/ExhibitionLayout`; keep this active layout asset's name and location.

Asset edits made in Play mode persist when saved. Designer Tools saves immediately;
for ordinary Inspector edits, save the project before closing Unity. Scene-object
edits made during Play mode are temporary.

**Mythologicum > UI > Repair Missing Steampunk References** supplies missing default
art references. It preserves existing fonts, colours, text settings and overrides;
it does not reset your design. Scenes are not rebuilt by these tools.

## Existing gameplay data

The new controls above cover interface appearance and layout. Existing gameplay
ScriptableObjects remain separate, including creature profiles in
`Resources/Creatures`, `Visitors/DefaultVisitors`, `Staff/DefaultStaff`,
`Progression/DefaultProgression`, and `Terrain/DefaultTerrain`. Changing UI text
sizes or colours does not change simulation rules or saved park values.

Open **Mythologicum > Gameplay > Balancing Tools** to edit gameplay values. The
[gameplay balancing guide](Gameplay-Balancing-Guide.md) lists park/economy,
building, fence, creature, visitor, staff, research, expedition and terrain assets,
including units and when changes take effect.
