using Godot;

namespace Rummy.Util;
#nullable enable

public static class ScriptExtensions
{
    public static Variant New(this Script script, params Variant[] args)
        => script switch {
            _ when !script.CanInstantiate() => default,
            GDScript gdScript => gdScript.New(args),
            CSharpScript cSharpScript => cSharpScript.New(args),
            _ => default
        };
    
    public static T? New<[MustBeVariant] T>(this Script script, params Variant[] args)
        => script switch {
            _ when !script.CanInstantiate() => default,
            GDScript gdScript => gdScript.New<T>(args),
            CSharpScript cSharpScript => cSharpScript.New<T>(args),
            _ => default
        };

    public static T? New<[MustBeVariant] T>(this GDScript script, params Variant[] args) => script.New(args).As<T>();
    public static T? New<[MustBeVariant] T>(this CSharpScript script, params Variant[] args) => script.New(args).As<T>();
}