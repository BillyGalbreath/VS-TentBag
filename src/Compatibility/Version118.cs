using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TentBag.Compatibility;

public class Version118 : Compat {
    public Version118() : base(
        typeof(BlockPos).GetConstructor(new[] {
            typeof(int), typeof(int), typeof(int)
        })!,
        typeof(BlockSchematic).GetMethod("PlaceEntitiesAndBlockEntities", new[] {
            typeof(IBlockAccessor),
            typeof(IWorldAccessor),
            typeof(BlockPos),
            typeof(Dictionary<int, AssetLocation>),
            typeof(Dictionary<int, AssetLocation>),
            typeof(bool)
        })!
    ) { }

    public override BlockPos NewBlockPos(int x, int y, int z) {
        return (BlockPos)BlockPosCtor.Invoke(new object?[] { x, y, z });
    }

    public override void InvokePlaceEntitiesAndBlockEntities(
        BlockSchematic blockSchematic,
        IBlockAccessor blockAccessor,
        IWorldAccessor worldForCollectibleResolve,
        BlockPos startPos,
        Dictionary<int, AssetLocation> blockCodes,
        Dictionary<int, AssetLocation> itemCodes,
        bool replaceBlockEntities = false
    ) {
        PlaceEntitiesAndBlockEntitiesMethod.Invoke(blockSchematic, new object?[] {
            blockAccessor, worldForCollectibleResolve, startPos, blockCodes, itemCodes, replaceBlockEntities
        });
    }
}
