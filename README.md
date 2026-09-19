# DS3 Appearance Preset Tool

*[Русская версия](README.ru.md)*

A Dark Souls III analogue of
**[DSAppearancePresetTool](https://github.com/BobDoleOwndU/DSAppearancePresetTool)**
by BobDoleOwndU, which does the same job for Dark Souls Remastered / PTDE.
WPF, Windows only.

Like the original, it works on the **memory of the running game**: two buttons,
one to save the character's look into a file and one to load it back. Save files
are not touched — for `.sl2` there is a web editor
([DSRSave](https://github.com/Piroshkiv/DSRSave)), and the two share one preset
format.

![the window](docs/window.png)

## How it differs from the original

| | DS1 tool | this one |
|---|---|---|
| preset file | `.dsrchr`, 130 bytes | `.ds3chr`, 216 bytes — the same file the DS3 editor's Appearance tab exports |
| gender | inside the preset | **a list of its own**, written into the game the moment it is picked |
| applying | writes memory | opens **Alter Appearance** before importing |

The whole window is three controls: export, import, gender. The line on top says
whether the tool is attached to the game, the line at the bottom says what
happened last.

## Why Alter Appearance

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

The gender list writes into the game as soon as a value is picked. Gender is what
Rosaria charges for, so change it with the appearance menu open — for example
right after an import, while the menu is still on screen.

## What gets written

```
PlayerGameData = [[GameDataMan] + 0x10]

  +0x0AA   1 byte     gender  (0 female, 1 male)
  +0x0AB   1 byte     voice   (0..2) — read on export only, never written
  +0x6B8 208 bytes    everything else: hair / brow / beard / pupil / tattoo
                      model ids, nine RGBA colours, the face shape sliders,
                      cosmetics and Build Detail (body proportions)
```

208 bytes, not the 192 the CT knows: the tail `0x778..0x787` is appearance data
too, and the game writes it into the save along with the rest. The float
proportions at `PGD+0x3B0` are not part of a preset — they are a runtime
expansion of the Build Detail bytes and never reach a save.

The full breakdown of the block lives in
[DSRSave](https://github.com/Piroshkiv/DSRSave) —
`ds1-save-editor/docs/ds3-appearance.md`, and the research tool that found it is
`tools/ds3-appearance-sweeper/` in the same repository.

## The `.ds3chr` format

```
0x00   4   magic "D3CH"
0x04   1   version = 1
0x05   1   gender
0x06   1   voice (never written to the game, but kept in the file — the format
           is shared with the editor)
0x07   1   reserved, 0
0x08 208   the face block, verbatim
```

Byte for byte the same file `DS3Character.exportAppearancePreset()` produces in
the web editor. So a look can be pulled out of any save with the editor and put
on a live character with this tool, and the other way round. The CT's own
`<facedata>` format is deliberately not supported — it cuts the block to 192
bytes and loses ten fields.

## Building and running

```powershell
dotnet build DS3AppearanceTool.csproj -c Release
dotnet run  --project DS3AppearanceTool.csproj
```

Needs .NET 8 (Windows Desktop). The solution is `DSAppearancePresetTool.sln`.

Order of work: start the game, load a character (at the main menu
`PlayerGameData` is still null — the tool says "no character loaded" and comes
alive on its own once a character is in), then export or import. If OpenProcess
is refused, run the tool as administrator.

## Warning

This tool writes into the game's memory. **Do not play online with it** — it is
the same as playing with Cheat Engine attached: go offline first, and back up
`%AppData%\Roaming\DarkSoulsIII\<steamid>\DS30000.sl2` before changing anything.

About the id fields: hair, beard, brows, pupils and tattoos are model indices,
and an index the game has no model for crashes it. The tool never lets you type
one — it carries over exactly the values the source preset held.

## Credits

- appearance offsets and the Alter Appearance script — `DS3_TGA_v3.4.0.CT`, by
  Igromanru;
- the idea and the two-button shape — DSAppearancePresetTool, by BobDoleOwndU;
- offsets checked against real saves and the face block decoded with
  `tools/ds3-appearance-sweeper` in [DSRSave](https://github.com/Piroshkiv/DSRSave).

None of the original DSAppearancePresetTool's code is here: this is a separate
implementation for DS3, written from scratch. What was reused from the CT are
offsets and structure layouts — facts about the game, not authored text.

## License

MIT, see `LICENSE`.
