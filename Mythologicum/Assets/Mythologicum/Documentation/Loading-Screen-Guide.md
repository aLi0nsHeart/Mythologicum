# Loading screen settings

Select `Assets/Mythologicum/Resources/Interface/DefaultLoadingScreen.asset` in
Unity's Project window to adjust the loading screen. The screen is created by the
scene-transition coordinator, so no scene or prefab editing is required.
The same asset is available under **Mythologicum > Gameplay > Balancing Tools >
Loading screen**.

New games, loading saved parks and returning to the menu share this screen. It
also protects park preparation when starting Play mode directly in GameScene.
The incoming park remains paused and input stays blocked until its systems are
ready. Loaded saves then retain their existing Pause menu; Resume starts the
simulation at the selected speed.

**Transition timing** uses real seconds, including while the park is paused.
The defaults fade in over 0.15 seconds, fade out over 0.2 seconds and keep the
screen visible for at least 0.4 seconds. Preparation has a 180-second timeout.
These controls do not accelerate loading or change the reported progress.

The background uses the active UI theme's texture over an opaque dark cover.
You can choose another texture, adjust its opacity, or disable it. The content
automatically scales down to fit the screen. Its size, margins, label positions,
text sizes and individual typography overrides are editable in the same asset.
Fonts and colours follow the current UI theme unless a typography override is
enabled; localization font fallbacks apply too.

The bronze loading frame and fill come from the existing
`UI/Art/Steampunk_UI_Loading_Bars.png` sprites. The frame keeps its source aspect
ratio. **Fill bounds** position the coloured interior as fractions of its frame.
If you choose different artwork, adjust the bar aspect ratio and fill bounds to
match. Its fill represents progress reported by the coordinator. The optional
percentage is hidden by default. The rotating gear shows activity and uses real
time; it does not advance the progress value.

Loading titles, preparation stages, the error message and the return button use
`ui.loading.*` IDs in `Resources/Localization/localization.csv`. Translate their
cells just like the rest of the interface. The error state replaces the progress
bar with a return-to-menu button. The loading screen retains input blocking until
the coordinator finishes or the player chooses recovery.
