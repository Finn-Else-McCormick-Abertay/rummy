using System.Collections.Generic;
using System.Linq;
using Godot;
using Rummy.AI;

namespace Rummy.Interface;

[Tool, GlobalClass]
public partial class PlayerIconResource : Resource
{
    [Export] private Godot.Collections.Dictionary<string, Texture2D> _iconTextures = [];

    public bool HasKey(string key) => _iconTextures.ContainsKey(key);
    public Texture2D IconFor(string key) => _iconTextures.GetValueOrDefault(key);

    public Texture2D IconFor<T>(T player) => player switch {
        null => IconFor("invalid"),
        UserPlayer => IconFor("user"),
        RandomPlayer when HasKey("random") => IconFor("random"),
        IntelligentPlayer when HasKey("intelligent") => IconFor("intelligent"),
        _ => IconFor("computer")
    } ?? IconFor("fallback");
}