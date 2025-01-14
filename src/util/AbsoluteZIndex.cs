
using Godot;

namespace Rummy.Util;

static class AbsoluteZIndexExtensions
{
    public static int FindAbsoluteZIndex(this CanvasItem self) {
        var zIndex = 0;
        CanvasItem canvasItem = self;
        while (canvasItem is not null) {
            zIndex += canvasItem.ZIndex;
            if (!canvasItem.ZAsRelative) { break; }
            canvasItem = canvasItem.GetParent() as CanvasItem;
        }
        return zIndex;
    }
}