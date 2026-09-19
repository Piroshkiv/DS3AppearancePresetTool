# How it works inside

## Where the look lives

```
PlayerGameData = [[GameDataMan] + 0x10]

  +0x0AA   1 byte     gender  (0 female, 1 male)
  +0x0AB   1 byte     voice   (0..2) — read on export only, never written
  +0x6B8 208 bytes    everything else: hair / brow / beard / pupil / tattoo
                      model ids, nine RGBA colours, the face shape sliders,
                      cosmetics and Build Detail (body proportions)
```

208 bytes, not the 192 the Cheat Engine table knows: the tail `0x778..0x787` is
appearance data too, and the game writes it into the save along with the rest.
The float proportions at `PGD+0x3B0` are not part of a preset — they are a
runtime expansion of the Build Detail bytes and never reach a save.

## Why Alter Appearance is opened on import

Writing 208 bytes to `PlayerGameData+0x6B8` is not enough on its own: the game
does not rebuild the character model every frame, so an edit made mid-game
reaches the screen after a level reload at best. The appearance menu (Rosaria's
service) is the place where the game reads those bytes, previews them and
commits the result on confirmation. So an import goes:

1. open Alter Appearance;
2. wait 2500 ms for the menu to come up (`MenuDelayMs` in `MainWindow.xaml.cs`);
3. write the face block and the gender;
4. the player then confirms the change in the menu.

The menu is opened the way the **"Alter Appearance"** script in
`DS3_TGA_v3.4.0.CT` does it (Rosaria group, by Igromanru): an AOB scan finds the
call site, its `rel32` gives the address of the menu function, and the call is
made on a thread of our own inside the game (`VirtualAllocEx` +
`CreateRemoteThread`). The call site is told apart by the menu id at the end of
the pattern: `0x10` is alter appearance, `0x12` Ludleth's transposition, `0x13`
stat reallocation. On the installed version of the game both patterns (this one
and `GameDataMan`) occur in `DarkSoulsIII.exe` exactly once.

## The `.ds3chr` format

```
0x00   4   magic "D3CH"
0x04   1   version = 1
0x05   1   gender
0x06   1   voice (read on export, never written back)
0x07   1   reserved, 0
0x08 208   the face block, verbatim
```

The CT's own `<facedata>` format is deliberately not supported — it cuts the
block to 192 bytes and loses ten fields.

## Model ids

Hair, beard, brows, pupils and tattoos are model indices, and an index the game
has no model for crashes it. The tool never lets you type one — it carries over
exactly the values the source preset held.

## On the original tool

None of the original DSAppearancePresetTool's code is here: this is a separate
implementation for DS3, written from scratch. What was reused from the Cheat
Engine table are offsets and structure layouts — facts about the game, not
authored text.
