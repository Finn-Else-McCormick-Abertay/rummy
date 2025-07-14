using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ConfigMenu : Control
{
    public GameManager GameManager {
        get; set {
            field = value;
            TryInitialise();
            this.OnReady(OnGameManagerChanged);
            this.OnReady(Rebuild);
        }
    }

    public TitleLine TitleLine { get; private set; }

    [Signal] public delegate void CloseRequestedEventHandler();

    [Export] public BaseButton SidebarButton { get; set; }
    [Export] public Shortcut Shortcut { get; set; }

    protected virtual void OnGameManagerChanged() { }
    protected virtual void Rebuild() { }

    // This has to be here because _Ready gets overwritten by base class
    private bool _configMenuInitialised = false;
    private void TryInitialise() {
        if (_configMenuInitialised) return;
        this.OnReady(() => {
            TitleLine = this.FindChildOfType<TitleLine>();
            TitleLine.IfValid(() => {
                TitleLine.CloseButton.Pressed += () => EmitSignal(SignalName.CloseRequested);
                if (string.IsNullOrEmpty(TitleLine.Title)) TitleLine.Title = GetType().Name.TrimSuffix("Menu").Capitalize();
            });
        });
        _configMenuInitialised = true;
    }
}