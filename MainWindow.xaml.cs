using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DS3AppearanceTool.Game;
using DS3AppearanceTool.Model;
using Microsoft.Win32;

namespace DS3AppearanceTool;

public partial class MainWindow : Window
{
    /// <summary>An entry in the gender list; ToString is what the closed box shows.</summary>
    private sealed record NamedValue(byte Value, string Name)
    {
        public override string ToString() => Name;
    }

    /// <summary>How long the Alter Appearance menu gets to come up before the face is written.</summary>
    private const int MenuDelayMs = 2500;

    private readonly DispatcherTimer _timer;

    private Ds3Game? _game;
    private IntPtr _lastPlayerGameData;
    private bool _busy;
    private bool _suppressComboEvents;

    public MainWindow()
    {
        InitializeComponent();

        GenderBox.ItemsSource = Ds3Layout.GenderNames
            .Select((name, i) => new NamedValue((byte)i, name)).ToList();

        SetReady(false);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
        Poll();
    }

    // --- game state ---------------------------------------------------------

    /// <summary>Attaches when the game shows up and keeps the status line honest.</summary>
    private void Poll()
    {
        if (_busy) return;

        try
        {
            if (_game is not { IsRunning: true })
            {
                _game?.Dispose();
                _game = null;
                _lastPlayerGameData = IntPtr.Zero;
                _game = Ds3Game.Attach();

                if (_game is null)
                {
                    SetStatus(StatusKind.Off, "Dark Souls III is not running");
                    SetReady(false);
                    return;
                }
            }

            var playerGameData = _game.PlayerGameData();
            SetStatus(StatusKind.Ok, $"Attached to DarkSoulsIII.exe (pid {_game.ProcessId})");
            SetReady(true);

            if (playerGameData != _lastPlayerGameData)
            {
                _lastPlayerGameData = playerGameData;
                ReadGender();
            }
        }
        catch (CharacterNotLoadedException)
        {
            _lastPlayerGameData = IntPtr.Zero;
            SetStatus(StatusKind.Waiting, "Game running, no character loaded");
            SetReady(false);
        }
        catch (Exception ex)
        {
            _lastPlayerGameData = IntPtr.Zero;
            SetStatus(StatusKind.Off, ex.Message);
            SetReady(false);
        }
    }

    private void ReadGender()
    {
        if (_game is null) return;

        _suppressComboEvents = true;
        try
        {
            GenderBox.SelectedValue = _game.Gender;
        }
        finally
        {
            _suppressComboEvents = false;
        }
    }

    // --- export / import ----------------------------------------------------

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_game is not { } game) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export appearance",
            Filter = $"DS3 appearance preset (*{AppearancePreset.Extension})|*{AppearancePreset.Extension}",
            DefaultExt = AppearancePreset.Extension,
            FileName = "appearance" + AppearancePreset.Extension,
            AddExtension = true,
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            game.ReadPreset().Save(dialog.FileName);
            Message($"Exported to {System.IO.Path.GetFileName(dialog.FileName)}.");
        }
        catch (Exception ex)
        {
            Fail("Export", ex);
        }
    }

    /// <summary>
    /// Import opens Alter Appearance first: writing the face block with no creator open
    /// leaves the model as it was until the next load, while the menu shows the new look
    /// as a preview and commits it once the player confirms.
    /// </summary>
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_game is not { } game || _busy) return;

        var dialog = new OpenFileDialog
        {
            Title = "Import appearance",
            Filter = $"DS3 appearance preset (*{AppearancePreset.Extension})|*{AppearancePreset.Extension}|" +
                     "All files (*.*)|*.*",
            DefaultExt = AppearancePreset.Extension,
        };
        if (dialog.ShowDialog(this) != true) return;

        _busy = true;
        SetReady(false);
        try
        {
            var preset = AppearancePreset.Load(dialog.FileName);

            Message("Opening Alter Appearance…");
            game.OpenAlterAppearance();
            await Task.Delay(MenuDelayMs);

            game.WritePreset(preset, applyGender: true);
            ReadGender();
            Message($"Applied {System.IO.Path.GetFileName(dialog.FileName)}. " +
                    "Confirm in the menu to keep it.");
        }
        catch (Exception ex)
        {
            Fail("Import", ex);
        }
        finally
        {
            _busy = false;
            Poll();
        }
    }

    // --- gender -------------------------------------------------------------

    private void GenderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboEvents || _game is not { } game) return;
        if (GenderBox.SelectedValue is not byte value) return;

        try
        {
            game.Gender = value;
            Message($"Gender set to {Ds3Layout.GenderName(value)}.");
        }
        catch (Exception ex)
        {
            Fail("Gender", ex);
        }
    }

    // --- plumbing -----------------------------------------------------------

    private enum StatusKind { Off, Waiting, Ok }

    private void SetStatus(StatusKind kind, string text)
    {
        StatusDot.Fill = (Brush)FindResource(kind switch
        {
            StatusKind.Ok => "OkBrush",
            StatusKind.Waiting => "AccentBrush",
            _ => "BadBrush",
        });
        StatusText.Text = text;
    }

    private void SetReady(bool ready)
    {
        ExportButton.IsEnabled = ready;
        ImportButton.IsEnabled = ready;
        GenderBox.IsEnabled = ready;
    }

    private void Message(string text) => MessageText.Text = text;

    private void Fail(string what, Exception ex)
    {
        Message($"{what} failed: {ex.Message}");
        MessageBox.Show(this, ex.Message, $"{what} failed",
            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _game?.Dispose();
        base.OnClosed(e);
    }
}
