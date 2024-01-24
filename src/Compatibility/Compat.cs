using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TentBag.Compatibility;

public abstract class Compat {
    protected readonly ConstructorInfo BlockPosCtor;
    protected readonly MethodInfo PlaceEntitiesAndBlockEntitiesMethod;

    protected Compat(ConstructorInfo blockPosCtor, MethodInfo placeEntitiesAndBlockEntitiesMethod) {
        BlockPosCtor = blockPosCtor;
        PlaceEntitiesAndBlockEntitiesMethod = placeEntitiesAndBlockEntitiesMethod;
    }

    public abstract BlockPos NewBlockPos(int x, int y, int z);

    public abstract void InvokePlaceEntitiesAndBlockEntities(
        BlockSchematic blockSchematic,
        IBlockAccessor blockAccessor,
        IWorldAccessor worldForCollectibleResolve,
        BlockPos startPos,
        Dictionary<int, AssetLocation> blockCodes,
        Dictionary<int, AssetLocation> itemCodes,
        bool replaceBlockEntities = false
    );

    public static Compat GetVersion() {
        try {
            if (Version119.GetPlaceEntitiesAndBlockEntitiesMethodInfo() != null) {
                return new Version119();
            }
        } catch (Exception) {
            // ignored
        }

        return new Version118();
    }
}
