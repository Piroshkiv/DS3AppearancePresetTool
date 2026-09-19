namespace DS3AppearanceTool.Game;

/// <summary>
/// Where DS3 keeps a character's look while the game is running.
///
/// Everything here is a fact about the game, gathered from DS3_TGA_v3.4.0.CT
/// (group "Appearance", FaceData scripts by Igromanru) and verified against
/// the save file by tools/ds3-appearance-sweeper — see
/// ds1-save-editor/docs/ds3-appearance.md for the full write-up.
/// </summary>
public static class Ds3Layout
{
    public const string ProcessName = "DarkSoulsIII";

    /// <summary>
    /// mov rax,[GameDataMan] / test rax,rax / jz / mov rax,[rax+xx] / ret.
    /// The pointer variable is RIP-relative: var = match + 7 + int32 at match+3.
    /// </summary>
    public const string GameDataManAob = "48 8B 05 ?? ?? ?? ?? 48 85 C0 ?? ?? 48 8B 40 ?? C3";

    /// <summary>
    /// The call site that opens Rosaria's "Alter Appearance" menu, the same one the
    /// CT script of that name scans for. The four leading wildcards are the rel32
    /// operand of the call, so the target is match + 4 + int32 at match. The menu id
    /// (0x10) is in the trailing <c>mov [rbx+18],10</c> and is what tells this call
    /// site apart from Ludleth's transpose (0x12) and stat reallocation (0x13).
    /// </summary>
    public const string AlterAppearanceAob =
        "?? ?? ?? ?? 90 48 8B 50 08 48 89 53 08 48 8B 40 10 48 89 43 10 " +
        "48 8D ?? ?? ?? ?? ?? 48 89 44 24 28 C7 43 18 10 00 00 00";

    /// <summary>PlayerGameData = [[GameDataMan] + 0x10].</summary>
    public const int PlayerGameDataOffset = 0x10;

    /// <summary>The whole look except gender and voice, copied verbatim into the save.</summary>
    public const int FaceOffset = 0x6B8;

    /// <summary>
    /// 208, not the 192 the CT knows: the tail 0x778..0x787 is real appearance data
    /// and the game writes it into the save along with the rest.
    /// </summary>
    public const int FaceSize = 0xD0;

    public const int GenderOffset = 0xAA;

    /// <summary>Read on export so a preset keeps the voice; the tool never writes it.</summary>
    public const int VoiceOffset = 0xAB;

    public static readonly string[] GenderNames = { "Female", "Male" };

    public static string GenderName(byte value) =>
        value < GenderNames.Length ? GenderNames[value] : $"Unknown ({value})";
}
