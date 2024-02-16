using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace TentBag.Extensions;

public static class BlockExtensions {
    public static void AddAreaWithoutEntities(this BlockSchematic bs, IWorldAccessor world, BlockPos start, BlockPos end) {
        // add 1 to end to make it's position inclusive
        for (int x = start.X; x < end.X + 1; ++x) {
            for (int y = start.Y; y < end.Y + 1; ++y) {
                for (int z = start.Z; z < end.Z + 1; ++z) {
                    BlockPos pos = TentBag.Instance.Compat!.NewBlockPos(x, y, z);

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
