using Godot;
using Rummy.Util;

namespace Rummy.Interface;

public partial class ConfigMenu : Control
{
    public GameManager GameManager { get; set { field = value; this.OnReady(Rebuild); } }
    
    [Signal] public delegate void CloseRequestedEventHandler();

    [Export] public BaseButton SidebarButton { get; set; }
    [Export] public Shortcut Shortcut { get; set; }

    protected virtual void Rebuild() { }
}