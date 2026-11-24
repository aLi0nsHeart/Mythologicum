# Project decisions

- Bigfoot, Yeti and the Loch Ness Monster are reserved for DLC. Do not add them
  to the base game's expedition pools.
- Keep the 23 expedition regions in `Assets/Mythologicum/Scripts/Progression/WorldRegionCatalog.cs`.
  Assign future creatures to those regions instead of adding country-specific
  locations. See `Assets/Mythologicum/Documentation/World-Regions-Guide.md` for
  the designer's map conventions. Species origin and expedition region are
  separate: Mongolia is an origin within East Asia, for example.
- Preserve legacy region save migration and the progression region schema
  marker when changing expedition data. Old merged or split permits must not
  duplicate rewards or lose paid access.
- Keep generated Unity `.asset` YAML contiguous, without blank separator lines.
  Unity's importer discarded later creature fields when these were present.
  Verify actual imported values in Unity as well as source-file contents.
- Existing scene and art edits belong to the designer. Keep programming changes
  small and data-driven; do not overwrite scenes or assets without authorization.
