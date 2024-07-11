namespace tentbag.configuration;

public abstract class Lang {
    public static string UnpackError(params object[] _) => Vintagestory.API.Config.Lang.Get("tentbag:unpack-error");
    public static string IllegalItemError(params object[] args) => Vintagestory.API.Config.Lang.Get("tentbag:illegal-item-error", args);
    public static string SolidGroundError(params object[] _) => Vintagestory.API.Config.Lang.Get("tentbag:solid-ground-error");
    public static string ClearAreaError(params object[] _) => Vintagestory.API.Config.Lang.Get("tentbag:clear-area-error");
}
