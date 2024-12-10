using System.Globalization;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.Common.Collectible.Block;
using Vintagestory.GameContent;

namespace tentbag.util;

public static class Extensions {
    private static FieldInfo? _sprintCounter;

    public static int ToColor(this string value) {
        if (value.StartsWith('#')) {
            value = value[1..];
        } else if (value.StartsWith("0x", StringComparison.CurrentCultureIgnoreCase)) {
            value = value[2..];
        } else if (value.StartsWith("&h", StringComparison.CurrentCultureIgnoreCase)) {
            value = value[2..];
        }

        return int.Parse(value, NumberStyles.HexNumber);
    }

    public static int Reverse(this int color) {
        int a = color >> 24 & 0xFF;
        int r = color >> 16 & 0xFF;
        int g = color >> 8 & 0xFF;
        int b = color & 0xFF;
        return a << 24 | b << 16 | g << 8 | r;
    }

    public static void ReduceOnlySaturation(this EntityPlayer entity, float amount) {
        EntityBehaviorHunger? hunger = entity.GetBehavior<EntityBehaviorHunger>();
        if (hunger == null) {
            return;
        }

        hunger.Saturation = Math.Max(0, hunger.Saturation - amount);
        (_sprintCounter ??= hunger.GetType().GetField("sprintCounter", BindingFlags.Instance | BindingFlags.NonPublic))?.SetValue(hunger, 0);
    }

    // https://github.com/anegostudios/vsapi/blob/master/Common/Collectible/Block/BlockSchematic.cs
    public static void AddAreaWithoutEntities(this BlockSchematic bs, IWorldAccessor world, IBlockAccessor blockAccess, BlockPos start, BlockPos end) {
        BlockPos startPos = new(Math.Min(start.X, end.X), Math.Min(start.Y, end.Y), Math.Min(start.Z, end.Z), start.dimension);
        BlockPos finalPos = new(Math.Max(start.X, end.X), Math.Max(start.Y, end.Y), Math.Max(start.Z, end.Z), start.dimension);
        bs.OriginalPos = start;

        BlockPos readPos = new(start.dimension); // readPos has dimensionality, keyPos does not (because keyPos will be saved in the schematic)

        // add 1 to end to make its position inclusive
        for (int x = startPos.X; x < finalPos.X + 1; ++x) {
            for (int y = startPos.Y; y < finalPos.Y + 1; ++y) {
                for (int z = startPos.Z; z < finalPos.Z + 1; ++z) {
                    readPos.Set(x, y, z);

                    int blockId = blockAccess.GetBlock(readPos, 1).BlockId;
                    int fluidId = blockAccess.GetBlock(readPos, 2).BlockId;
                    if (fluidId == blockId) {
                        blockId = 0;
                    }

                    // don't skip liquids
                    /*if (bs.OmitLiquids) {
                        fluidId = 0;
                    }*/

                    // don't skip air (can cause unpack to be off centered)
                    /*if (blockId == 0 && fluidId == 0) {
                        continue;
                    }*/

                    BlockPos keyPos = new(x, y, z); // We create a new BlockPos object each time because it's going to be used as a key in the BlocksUnpacked dictionary; in future for performance a long might be a better key
                    bs.BlocksUnpacked[keyPos] = blockId;
                    bs.FluidsLayerUnpacked[keyPos] = fluidId;

                    BlockEntity blockEntity = blockAccess.GetBlockEntity(readPos);
                    if (blockEntity != null) {
                        if (blockEntity.Api == null) {
                            blockEntity.Initialize(world.Api);
                        }

                        bs.BlockEntitiesUnpacked[keyPos] = bs.EncodeBlockEntityData(blockEntity);
                        blockEntity.OnStoreCollectibleMappings(bs.BlockCodes, bs.ItemCodes);
                    }

                    Dictionary<int, Block> decors = blockAccess.GetSubDecors(readPos);
                    if (decors != null) {
                        bs.DecorsUnpacked[keyPos] = decors;
                    }
                }
            }
        }
    }

    // https://github.com/anegostudios/vsapi/blob/master/Common/Collectible/Block/BlockSchematic.cs
    public static bool PackIncludingAir(this BlockSchematic bs, IWorldAccessor world, BlockPos startPos) {
        bs.Indices.Clear();
        bs.BlockIds.Clear();
        bs.BlockEntities.Clear();
        bs.Entities.Clear();
        bs.DecorIndices.Clear();
        bs.DecorIds.Clear();
        bs.SizeX = 0;
        bs.SizeY = 0;
        bs.SizeZ = 0;

        int minX = int.MaxValue, minY = int.MaxValue, minZ = int.MaxValue;

        foreach (KeyValuePair<BlockPos, int> val in bs.BlocksUnpacked) {
            minX = Math.Min(minX, val.Key.X);
            minY = Math.Min(minY, val.Key.Y);
            minZ = Math.Min(minZ, val.Key.Z);

            // Store relative position and the block id
            int dx = val.Key.X - startPos.X;
            int dy = val.Key.Y - startPos.Y;
            int dz = val.Key.Z - startPos.Z;

            if (dx >= 1024 || dy >= 1024 || dz >= 1024) {
                world.Logger.Warning("Export format does not support areas larger than 1024 blocks in any direction. Will not pack.");
                bs.PackedOffset = new FastVec3i(0, 0, 0);
                return false;
            }
        }

        foreach ((BlockPos? pos, int blockid) in bs.BlocksUnpacked) {
            int fluidid = bs.FluidsLayerUnpacked.GetValueOrDefault(pos, 0);
            // do not skip air!
            //if (blockid == 0 && fluidid == 0) continue;

            // Store a block mapping (do not skip air!)
            /*if (blockid != 0)*/
            bs.BlockCodes[blockid] = world.BlockAccessor.GetBlock(blockid).Code;
            /*if (fluidid != 0)*/
            bs.BlockCodes[fluidid] = world.BlockAccessor.GetBlock(fluidid).Code;

            // Store relative position and the block id
            int dx = pos.X - minX;
            int dy = pos.Y - minY;
            int dz = pos.Z - minZ;

            bs.SizeX = Math.Max(dx, bs.SizeX);
            bs.SizeY = Math.Max(dy, bs.SizeY);
            bs.SizeZ = Math.Max(dz, bs.SizeZ);

            bs.Indices.Add((uint)((dy << 20) | (dz << 10) | dx));
            if (fluidid == 0) {
                bs.BlockIds.Add(blockid);
            } else if (blockid == 0) {
                bs.BlockIds.Add(fluidid);
            } else // if both block layer and liquid layer are present (non-zero), add this twice;  placing code will place the liquidid blocks in the liquids layer
            {
                bs.BlockIds.Add(blockid);
                bs.Indices.Add((uint)((dy << 20) | (dz << 10) | dx));
                bs.BlockIds.Add(fluidid);
            }
        }

        // also export fluid locks that do not have any other blocks in the same position
        foreach ((BlockPos? pos, int blockId) in bs.FluidsLayerUnpacked) {
            if (bs.BlocksUnpacked.ContainsKey(pos)) continue;

            // Store a block mapping (do not skip air!)
            /*if (blockId != 0)*/
            bs.BlockCodes[blockId] = world.BlockAccessor.GetBlock(blockId).Code;

            // Store relative position and the block id
            int dx = pos.X - minX;
            int dy = pos.Y - minY;
            int dz = pos.Z - minZ;

            bs.SizeX = Math.Max(dx, bs.SizeX);
            bs.SizeY = Math.Max(dy, bs.SizeY);
            bs.SizeZ = Math.Max(dz, bs.SizeZ);

            bs.Indices.Add((uint)((dy << 20) | (dz << 10) | dx));
            bs.BlockIds.Add(blockId);
        }

        foreach ((BlockPos? pos, Dictionary<int, Block>? decors) in bs.DecorsUnpacked) {
            // Store relative position and the block id
            int dx = pos.X - minX;
            int dy = pos.Y - minY;
            int dz = pos.Z - minZ;

            bs.SizeX = Math.Max(dx, bs.SizeX);
            bs.SizeY = Math.Max(dy, bs.SizeY);
            bs.SizeZ = Math.Max(dz, bs.SizeZ);


            foreach ((int faceAndSubposition, Block? decorBlock) in decors) {
                bs.BlockCodes[decorBlock.BlockId] = decorBlock.Code;
                bs.DecorIndices.Add((uint)((dy << 20) | (dz << 10) | dx));

                //UnpackDecorPosition(packedIndex, out var decorFaceIndex, out var subPosition);
                //decorFaceIndex += subPosition * 6;
                bs.DecorIds.Add(((long)faceAndSubposition << 24) + decorBlock.BlockId);
            }
        }

        // off-by-one problem as usual. A block at x=3 and x=4 means a sizex of 2
        bs.SizeX++;
        bs.SizeY++;
        bs.SizeZ++;

        foreach (KeyValuePair<BlockPos, string> val in bs.BlockEntitiesUnpacked) {
            int dx = val.Key.X - minX;
            int dy = val.Key.Y - minY;
            int dz = val.Key.Z - minZ;
            bs.BlockEntities[(uint)((dy << 20) | (dz << 10) | dx)] = val.Value;
        }

        BlockPos minPos = new(minX, minY, minZ);
        foreach (Entity? e in bs.EntitiesUnpacked) {
            using MemoryStream ms = new();
            BinaryWriter writer = new(ms);

            writer.Write(world.ClassRegistry.GetEntityClassName(e.GetType()));

            e.WillExport(minPos);
            e.ToBytes(writer, false);
            e.DidImportOrExport(minPos);

            bs.Entities.Add(Ascii85.Encode(ms.ToArray()));
        }

        if (bs.PathwayBlocksUnpacked != null) {
            foreach (BlockPosFacing path in bs.PathwayBlocksUnpacked) {
                path.Position.X -= minX;
                path.Position.Y -= minY;
                path.Position.Z -= minZ;
            }
        }

        bs.PackedOffset = new FastVec3i(minX - startPos.X, minY - startPos.Y, minZ - startPos.Z);
        bs.BlocksUnpacked.Clear();
        bs.FluidsLayerUnpacked.Clear();
        bs.DecorsUnpacked.Clear();
        bs.BlockEntitiesUnpacked.Clear();
        bs.EntitiesUnpacked.Clear();
        return true;
    }
}
