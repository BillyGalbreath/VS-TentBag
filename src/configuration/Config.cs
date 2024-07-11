using System.Diagnostics.CodeAnalysis;

namespace tentbag.configuration;

[SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Global")]
public class Config {
    public int Radius { get; set; } = 3;
    public int Height { get; set; } = 7;
    public float BuildEffort { get; set; } = 100F;
    public bool RequireFloor { get; set; } = false;
    public bool ReplacePlantsAndRocks { get; set; } = true;
    public bool PutTentInInventoryOnUse { get; set; } = true;
    public string HighlightErrorColor { get; set; } = "#2FFF0000";

    public string[] BannedBlocks { get; set; } = {
        "game:log-grown-*",
        "game:log-resin-*",
        "game:log-resinharvested-*",
        "game:statictranslocator-*",
        "game:teleporterbase",
        "game:crop-*",
        "game:herb-*",
        "game:mushroom-*",
        "game:smallberrybush-*",
        "game:bigberrybush-*",
        "game:water-*",
        "game:lava-*",
        "game:farmland-*",
        "game:rawclay-*",
        "game:peat-*",
        "game:rock-*",
        "game:ore-*",
        "game:crock-burned-*",
        "game:bowl-meal",
        "game:claypot-cooked",
        "game:anvil-*",
        "game:forge"
    };
}
