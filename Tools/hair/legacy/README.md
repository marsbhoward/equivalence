# The face-on hairstyles

`BodyLook_faceon.cs` is `Assets/Scripts/Art/BodyLook.cs` as it stood just before the face-on hair
was removed for the two-view rework. The views in
`Assets/Scripts/Art/BodyLook.HairArt.cs` are built from these; `Tools/hair/compare.py` sets them side by side. Outside `Assets/`, so Unity never
compiles it. The styles are the `HairStyles` array: each a cap grid, an optional mane with its
`maneTop`, and for Shock and High Tail hand-drawn finer `menuGrid`/`menuMane`.

Every cap and mane except High Top's cap and Long's mane is an exact 2x block-double of the
original 16x14-era art (made when `PixelSprite.LayoutUnit` went 37.5 -> 75). Taking every other
row and column gives the pre-upgrade originals back, pixel for pixel.
