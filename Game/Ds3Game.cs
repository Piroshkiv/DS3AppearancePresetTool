using DS3AppearanceTool.Model;

namespace DS3AppearanceTool.Game;

/// <summary>Raised when the game is running but a character is not loaded yet.</summary>
public sealed class CharacterNotLoadedException : Exception
{
    public CharacterNotLoadedException(string message) : base(message) { }
}

/// <summary>
/// The running game: resolves PlayerGameData, reads and writes the appearance
/// blocks, and opens the Alter Appearance menu.
/// </summary>
public sealed class Ds3Game : IDisposable
{
    private readonly ProcessMemory _memory;
    private IntPtr _gameDataManVar;
    private IntPtr _alterAppearanceFunc;

    private Ds3Game(ProcessMemory memory) => _memory = memory;

    public int ProcessId => _memory.Process.Id;

    public bool IsRunning => _memory.IsRunning;

    /// <summary>Attaches to a running DarkSoulsIII.exe, or returns null if none is running.</summary>
    public static Ds3Game? Attach()
    {
        var memory = ProcessMemory.Open(Ds3Layout.ProcessName);
        return memory is null ? null : new Ds3Game(memory);
    }

    /// <summary>Address of the GameDataMan pointer variable, resolved once per attach.</summary>
    private IntPtr GameDataManVar()
    {
        if (_gameDataManVar != IntPtr.Zero) return _gameDataManVar;

        var match = _memory.Scan(ProcessMemory.ParsePattern(Ds3Layout.GameDataManAob));
        if (match == IntPtr.Zero)
            throw new InvalidOperationException(
                "GameDataMan not found. The game version may be newer than this tool's pattern.");

        var address = match + 7 + _memory.ReadInt32(match + 3);
        if (address.ToInt64() < _memory.ModuleBase.ToInt64() ||
            address.ToInt64() >= _memory.ModuleBase.ToInt64() + _memory.ModuleSize)
            throw new InvalidOperationException("Resolved GameDataMan lies outside the module image.");

        _gameDataManVar = address;
        return address;
    }

    /// <summary>PlayerGameData = [[GameDataMan] + 0x10]. Null until a character is loaded.</summary>
    public IntPtr PlayerGameData()
    {
        var gameDataMan = (IntPtr)_memory.ReadInt64(GameDataManVar());
        if (gameDataMan == IntPtr.Zero)
            throw new CharacterNotLoadedException("No character loaded (the game is at the main menu).");

        var pgd = (IntPtr)_memory.ReadInt64(gameDataMan + Ds3Layout.PlayerGameDataOffset);
        if (pgd == IntPtr.Zero)
            throw new CharacterNotLoadedException("No character loaded (the game is at the main menu).");

        return pgd;
    }

    // --- appearance ---------------------------------------------------------

    public byte[] ReadFace() => _memory.Read(PlayerGameData() + Ds3Layout.FaceOffset, Ds3Layout.FaceSize);

    public void WriteFace(byte[] face)
    {
        if (face.Length != Ds3Layout.FaceSize)
            throw new ArgumentException($"Face block must be {Ds3Layout.FaceSize} bytes.", nameof(face));

        _memory.Write(PlayerGameData() + Ds3Layout.FaceOffset, face);
    }

    public byte Gender
    {
        get => _memory.ReadByte(PlayerGameData() + Ds3Layout.GenderOffset);
        set => _memory.WriteByte(PlayerGameData() + Ds3Layout.GenderOffset, value);
    }

    /// <summary>Read-only: a preset carries the voice, but nothing here writes it.</summary>
    public byte Voice => _memory.ReadByte(PlayerGameData() + Ds3Layout.VoiceOffset);

    public AppearancePreset ReadPreset() => new(Gender, Voice, ReadFace());

    /// <summary>
    /// Writes a preset into the live character: the face block, and the gender when asked.
    /// The preset's voice is carried by the file but never written — the tool does not
    /// offer voice, so importing one cannot change how the character sounds.
    /// </summary>
    public void WritePreset(AppearancePreset preset, bool applyGender)
    {
        WriteFace(preset.Face);
        if (applyGender) Gender = preset.Gender;
    }

    // --- Alter Appearance ---------------------------------------------------

    /// <summary>
    /// Opens Rosaria's Alter Appearance menu, the same way the CT script does: find the
    /// call site, follow its rel32 to the menu function, and call it from a thread of
    /// our own. This is what makes an imported face actually take hold — the creator
    /// applies PlayerGameData to the model and commits it when you confirm.
    /// </summary>
    public void OpenAlterAppearance()
    {
        if (_alterAppearanceFunc == IntPtr.Zero)
        {
            var match = _memory.Scan(ProcessMemory.ParsePattern(Ds3Layout.AlterAppearanceAob));
            if (match == IntPtr.Zero)
                throw new InvalidOperationException(
                    "The Alter Appearance call site was not found. The game version may be " +
                    "newer than this tool's pattern.");

            _alterAppearanceFunc = match + 4 + _memory.ReadInt32(match);
        }

        _memory.RunShellcode(BuildCallShellcode(_alterAppearanceFunc));
    }

    /// <summary>
    /// sub rsp,0x48 / mov rax,func / lea rcx,[rsp+0x28] / call rax / add rsp,0x48 / ret.
    /// The frame matches the CT script's: the callee gets a scratch struct on our stack
    /// and rsp stays aligned the way the x64 ABI expects at a call.
    /// </summary>
    private static byte[] BuildCallShellcode(IntPtr function)
    {
        var code = new List<byte>
        {
            0x48, 0x83, 0xEC, 0x48,             // sub rsp,0x48
            0x48, 0xB8,                         // mov rax, imm64
        };
        code.AddRange(BitConverter.GetBytes(function.ToInt64()));
        code.AddRange(new byte[]
        {
            0x48, 0x8D, 0x4C, 0x24, 0x28,       // lea rcx,[rsp+0x28]
            0xFF, 0xD0,                         // call rax
            0x48, 0x83, 0xC4, 0x48,             // add rsp,0x48
            0xC3,                               // ret
        });

        return code.ToArray();
    }

    public void Dispose() => _memory.Dispose();
}
