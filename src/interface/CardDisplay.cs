using Godot;
using Rummy.Game;
using System;

namespace Rummy.Interface;

[Tool]
public partial class CardDisplay : Control
{
    private Rank _rank;
    [Export] public Rank Rank {
        get => _rank;
        set { _rank = value; UpdateTexture(); }
    }
    private Suit _suit;
    [Export] public Suit Suit {
        get => _suit;
        set { _suit = value; UpdateTexture(); } 
    }

    private bool _faceDown = false;
    [Export] public bool FaceDown {
        get => _faceDown;
        set { _faceDown = value; UpdateFacing(); }
    }

    private TextureRect frontTextureRect, backTextureRect;

    public override void _Ready() {
        UpdateTexture();
        UpdateFacing();
    }

    private void UpdateTexture() {
        if (!IsNodeReady()) { return; }

        frontTextureRect ??= GetNode<TextureRect>("Front");

        var atlasTexture = (AtlasTexture)frontTextureRect.Texture;
        atlasTexture.Region = atlasTexture.Region with {
            Size = Size with {
                X = atlasTexture.Atlas.GetWidth() / 13f,
                Y = atlasTexture.Atlas.GetHeight() / 4f
            }
        };
        atlasTexture.Region = atlasTexture.Region with {
            Position = Position with {
                X = ((int)Rank - 1) * atlasTexture.Region.Size.X,
                Y = (int)Suit * atlasTexture.Region.Size.Y
            }
        };
        frontTextureRect.Texture = atlasTexture;
    }

    private void UpdateFacing() {
        if (!IsNodeReady()) { return; }

        frontTextureRect ??= GetNode<TextureRect>("Front");
        backTextureRect ??= GetNode<TextureRect>("Back");

        frontTextureRect.Visible = !FaceDown;
        backTextureRect.Visible = FaceDown;
    }
}
