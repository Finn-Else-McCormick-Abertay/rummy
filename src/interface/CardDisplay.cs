using Godot;
using Rummy.Game;

namespace Rummy.Interface;

[Tool]
public partial class CardDisplay : AspectRatioContainer
{
    [Export] public Rank Rank { get; set { field = value; UpdateTexture(); } }
    [Export] public Suit Suit { get; set { field = value; UpdateTexture(); } }

    public Card Card { get => new(Rank, Suit); set { Rank = value.Rank; Suit = value.Suit; } }

    [Export] public bool FaceDown { get; set { field = value; UpdateFacing(); } }

    private TextureRect frontTextureRect, backTextureRect;

    public override Variant _PropertyGetRevert(StringName property) {
        if (property == PropertyName.Rank) { return Variant.From(Rank.Ace); }
        return default;
    }

    public override void _Ready() { UpdateTexture(); UpdateFacing(); }

    private void UpdateTexture() {
        if (!IsNodeReady()) return;

        frontTextureRect ??= GetNode<TextureRect>("Front");

        var atlas = frontTextureRect.Texture as AtlasTexture;
        Vector2 size = new(atlas.Atlas.GetWidth() / 13f, atlas.Atlas.GetHeight() / 4f);
        atlas.Region = new Rect2(((int)Rank - 1) * size.X, (int)Suit * size.Y, size);
        frontTextureRect.Texture = atlas;
    }

    private void UpdateFacing() {
        if (!IsNodeReady()) return;

        frontTextureRect ??= GetNode<TextureRect>("Front");
        backTextureRect ??= GetNode<TextureRect>("Back");

        frontTextureRect.Visible = !FaceDown;
        backTextureRect.Visible = FaceDown;
    }
}
