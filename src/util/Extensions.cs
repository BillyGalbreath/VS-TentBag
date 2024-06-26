using System;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace TentBag.util;

public static class Extensions {
    private static FieldInfo? _sprintCounter;

    public static void ReduceOnlySaturation(this EntityPlayer entity, float amount) {
        EntityBehaviorHunger? hunger = entity.GetBehavior<EntityBehaviorHunger>();
        if (hunger == null) {
            return;
        }
        hunger.Saturation = Math.Max(0, hunger.Saturation - amount);
        (_sprintCounter ??= hunger.GetType().GetField("sprintCounter", BindingFlags.Instance | BindingFlags.NonPublic))?.SetValue(hunger, 0);
    }

    public static void AddAreaWithoutEntities(this BlockSchematic bs, IWorldAccessor world, BlockPos start, BlockPos end) {
        // add 1 to end to make its position inclusive
        for (int x = start.X; x < end.X + 1; ++x) {
            for (int y = start.Y; y < end.Y + 1; ++y) {
                for (int z = start.Z; z < end.Z + 1; ++z) {
                    BlockPos pos = new(x, y, z, 0);

                    int blockId = world.BulkBlockAccessor.GetBlock(pos, 1).BlockId;
                    int fluidId = world.BulkBlockAccessor.GetBlock(pos, 2).BlockId;
                    if (fluidId == blockId) {
                        blockId = 0;
                    }

                    // don't skip air (can cause unpack to be off centered)
                    /*if (blockId == 0 && fluidId == 0) {
                        continue;
                    }*/

                    bs.BlocksUnpacked[pos] = blockId;
                    bs.FluidsLayerUnpacked[pos] = fluidId;

                    BlockEntity blockEntity = world.BulkBlockAccessor.GetBlockEntity(pos);
                    if (blockEntity != null) {
                        bs.BlockEntitiesUnpacked[pos] = bs.EncodeBlockEntityData(blockEntity);
                        blockEntity.OnStoreCollectibleMappings(bs.BlockCodes, bs.ItemCodes);
                    }

                    Block[] decors = world.BulkBlockAccessor.GetDecors(pos);
                    if (decors != null) {
                        bs.DecorsUnpacked[pos] = decors;
                    }
                }
            }
        }
    }
}
