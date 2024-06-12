using System.Diagnostics.CodeAnalysis;

namespace TentBag.Configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Config {
    public int Radius { get; set; } = 3;
    public int Height { get; set; } = 7;
    public float BuildEffort { get; set; } = 100F;
    public bool RequireFloor { get; set; } = false;
    public bool ReplacePlantsAndRocks { get; set; } = true;
    public bool PutTentInInventoryOnUse { get; set; } = true;
}
