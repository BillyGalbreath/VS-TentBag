using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TentBag.Compatibility;

public class Version119 : Compat {
    public Version119() : base(
        typeof(BlockPos).GetConstructor(new[] {
            typeof(int), typeof(int), typeof(int), typeof(int)
        })!,
        GetPlaceEntitiesAndBlockEntitiesMethodInfo()!
    ) { }

    public override BlockPos NewBlockPos(int x, int y, int z) {
        return (BlockPos)BlockPosCtor.Invoke(new object?[] { x, y, z, 0 });
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
            blockAccessor, worldForCollectibleResolve, startPos, blockCodes, itemCodes, replaceBlockEntities, null, 0, null, true
        });
    }

    protected internal static MethodInfo? GetPlaceEntitiesAndBlockEntitiesMethodInfo() {
        return typeof(BlockSchematic).GetMethod("PlaceEntitiesAndBlockEntities", new[] {
            typeof(IBlockAccessor),
            typeof(IWorldAccessor),
            typeof(BlockPos),
            typeof(Dictionary<int, AssetLocation>),
            typeof(Dictionary<int, AssetLocation>),
            typeof(bool),
            typeof(Dictionary<int, Dictionary<int, int>>),
            typeof(int),
            typeof(Dictionary<BlockPos, Block>),
            typeof(bool)
        });
    }
}
