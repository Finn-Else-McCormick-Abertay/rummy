
using Godot;

namespace Rummy.Interface;

public static class ActionName
{
    public static readonly StringName Select = "select";
    public static readonly StringName Skip = "skip";
    public static readonly StringName Meld = "meld";
    public static readonly StringName Discard = "discard";

    public static class UI
    {
        public static readonly StringName Left = "ui_left";
        public static readonly StringName Right = "ui_right";
        public static readonly StringName Up = "ui_up";
        public static readonly StringName Down = "ui_down";
    }
}