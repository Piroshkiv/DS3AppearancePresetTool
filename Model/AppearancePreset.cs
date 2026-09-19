using System.IO;
using System.Text;
using DS3AppearanceTool.Game;

namespace DS3AppearanceTool.Model;

public sealed class PresetFormatException : Exception
{
    public PresetFormatException(string message) : base(message) { }
}

/// <summary>
/// A .ds3chr preset — the same 216-byte file the DS3 web save editor reads and
/// writes, so a look can travel between a save file and the running game.
///
///     0x00   4   magic "D3CH"
///     0x04   1   version = 1
///     0x05   1   gender      (PlayerGameData + 0xAA)
///     0x06   1   voice       (PlayerGameData + 0xAB)
///     0x07   1   reserved, 0
///     0x08 208   face block  (PlayerGameData + 0x6B8 .. +0x787)
///
/// The CT's own facedata format is deliberately not used: it cuts the face block
/// to 192 bytes and loses the last ten fields, and its body floats are a runtime
/// expansion of Build Detail that never reaches a save.
/// </summary>
public sealed class AppearancePreset
{
    public const string Extension = ".ds3chr";
    public const string Magic = "D3CH";
    public const byte Version = 1;
    public const int HeaderSize = 8;
    public const int FileSize = HeaderSize + Ds3Layout.FaceSize;

    /// <summary>A DS1 .dsrchr is exactly this long, and telling the user so beats "bad magic".</summary>
    private const int Ds1PresetSize = 130;

    public AppearancePreset(byte gender, byte voice, byte[] face)
    {
        if (face.Length != Ds3Layout.FaceSize)
            throw new ArgumentException($"Face block must be {Ds3Layout.FaceSize} bytes.", nameof(face));

        Gender = gender;
        Voice = voice;
        Face = face;
    }

    public byte Gender { get; }
    public byte Voice { get; }
    public byte[] Face { get; }

    public byte[] ToBytes()
    {
        var bytes = new byte[FileSize];
        Encoding.ASCII.GetBytes(Magic).CopyTo(bytes, 0);
        bytes[4] = Version;
        bytes[5] = Gender;
        bytes[6] = Voice;
        bytes[7] = 0;
        Face.CopyTo(bytes, HeaderSize);
        return bytes;
    }

    public static AppearancePreset FromBytes(byte[] bytes)
    {
        if (bytes.Length < HeaderSize)
            throw new PresetFormatException("File is too short to be an appearance preset.");

        if (Encoding.ASCII.GetString(bytes, 0, 4) != Magic)
            throw new PresetFormatException(bytes.Length == Ds1PresetSize
                ? "This looks like a DS1 .dsrchr preset, which does not fit DS3."
                : "Not a DS3 appearance preset (bad magic).");

        if (bytes[4] != Version)
            throw new PresetFormatException($"Preset version {bytes[4]} is not supported (expected {Version}).");

        if (bytes.Length < FileSize)
            throw new PresetFormatException("Preset file is truncated.");

        var face = new byte[Ds3Layout.FaceSize];
        Array.Copy(bytes, HeaderSize, face, 0, face.Length);
        return new AppearancePreset(bytes[5], bytes[6], face);
    }

    public static AppearancePreset Load(string path) => FromBytes(File.ReadAllBytes(path));

    public void Save(string path) => File.WriteAllBytes(path, ToBytes());
}
