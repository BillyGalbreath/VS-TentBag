using tentbag.configuration;
using tentbag.util;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace tentbag.behaviors;

public class PackableBehavior : CollectibleBehavior {
    private static Config Config => TentBag.Instance.Config;
    private static bool IsAirOrNull(Block? block) => block is not { Replaceable: < 9505 };
    private static bool IsPlantOrRock(Block? block) => Config.ReplacePlantsAndRocks && block?.Replaceable is >= 5500 and <= 6500;
    private static bool IsReplaceable(Block? block) => IsAirOrNull(block) || IsPlantOrRock(block);

    private static void SendClientError(EntityPlayer entity, string error) => TentBag.Instance.SendClientError(entity.Player, error);

    private readonly AssetLocation? _emptyBag;
    private readonly AssetLocation? _packedBag;

    private long _highlightId;

    public PackableBehavior(CollectibleObject obj) : base(obj) {
        string domain = obj.Code.Domain;
        string path = obj.CodeWithoutParts(1);
        _emptyBag = new AssetLocation(domain, $"{path}-empty");
        _packedBag = new AssetLocation(domain, $"{path}-packed");
    }

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling, ref EnumHandling handling) {
        if (blockSel == null || byEntity is not EntityPlayer entity) {
            return;
        }

        handHandling = EnumHandHandling.PreventDefaultAction;

        if (entity.Api.Side != EnumAppSide.Server) {
            return;
        }

        string contents = slot.Itemstack.Attributes.GetString("tent-contents") ?? slot.Itemstack.Attributes.GetString("packed-contents");
        if (contents == null) {
            PackContents(entity, blockSel, slot);
        } else {
            UnpackContents(entity, blockSel, slot, contents);
        }
    }

    private void PackContents(EntityPlayer entity, BlockSelection blockSel, ItemSlot slot) {
        IBlockAccessor blockAccessor = entity.World.BlockAccessor;

        int y = IsPlantOrRock(blockAccessor.GetBlock(blockSel.Position)) ? 1 : 0;

        BlockPos start = blockSel.Position.AddCopy(-Config.Radius, 1 - y, -Config.Radius);
        BlockPos end = blockSel.Position.AddCopy(Config.Radius, Math.Max(Config.Height, 3), Config.Radius);

        if (!CanPack(entity, blockAccessor, start, end)) {
            return;
        }

        // create schematic of area
        BlockSchematic bs = new();
        bs.AddAreaWithoutEntities(entity.World, start, end);
        bs.Pack(entity.World, start);

        // clear area in world (requires bulk block accessor to prevent decor dupes)
        IBulkBlockAccessor bulkBlockAccessor = entity.World.BulkBlockAccessor;
        bulkBlockAccessor.WalkBlocks(start, end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = new(posX, posY, posZ, 0);
            bulkBlockAccessor.SetBlock(0, pos);
            bulkBlockAccessor.MarkBlockModified(pos);
            bulkBlockAccessor.MarkBlockDirty(pos);
            bulkBlockAccessor.MarkBlockEntityDirty(pos);
            bulkBlockAccessor.MarkChunkDecorsModified(pos);
        });
        bulkBlockAccessor.Commit();

        // drop packed item on the ground and remove empty from inventory
        ItemStack packed = new(entity.World.GetItem(_packedBag), slot.StackSize);
        packed.Attributes.SetString("packed-contents", bs.ToJson());
        if (Config.PutTentInInventoryOnUse) {
            ItemStack sinkStack = slot.Itemstack.Clone();
            slot.Itemstack.StackSize = 0;
            slot.Itemstack = packed;
            slot.OnItemSlotModified(sinkStack);
        } else {
            entity.World.SpawnItemEntity(packed, blockSel.Position.ToVec3d().Add(0, 1 - y, 0));
            slot.TakeOutWhole();
        }

        // consume player saturation
        entity.ReduceOnlySaturation(Config.BuildEffort);
    }

    private void UnpackContents(EntityPlayer entity, BlockSelection blockSel, ItemSlot slot, string contents) {
        IBlockAccessor blockAccessor = entity.World.BlockAccessor;

        int y = IsPlantOrRock(blockAccessor.GetBlock(blockSel.Position)) ? 1 : 0;

        BlockPos start = blockSel.Position.AddCopy(-Config.Radius, 0 - y, -Config.Radius);
        BlockPos end = blockSel.Position.AddCopy(Config.Radius, Math.Max(Config.Height, 3), Config.Radius);

        if (!CanUnpack(entity, blockAccessor, start, end)) {
            return;
        }

        // try load schematic data from json contents
        string? error = null;
        BlockSchematic bs = BlockSchematic.LoadFromString(contents, ref error);
        if (!string.IsNullOrEmpty(error)) {
            SendClientError(entity, Lang.UnpackError());
            return;
        }

        // clear area in world (requires bulk block accessor to prevent decor dupes)
        IBulkBlockAccessor bulkBlockAccessor = entity.World.BulkBlockAccessor;
        bulkBlockAccessor.WalkBlocks(start.AddCopy(0, 1, 0), end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = new(posX, posY, posZ, 0);
            bulkBlockAccessor.SetBlock(0, pos);
            bulkBlockAccessor.MarkBlockModified(pos);
            bulkBlockAccessor.MarkBlockDirty(pos);
            bulkBlockAccessor.MarkBlockEntityDirty(pos);
            bulkBlockAccessor.MarkChunkDecorsModified(pos);
        });
        bulkBlockAccessor.Commit();

        // paste the schematic into the world (requires regular block accessor to prevent lighting/room issues)
        BlockPos adjustedStart = bs.AdjustStartPos(start.Add(Config.Radius, 1, Config.Radius), EnumOrigin.BottomCenter);
        bs.ReplaceMode = EnumReplaceMode.ReplaceAll;
        bs.Place(blockAccessor, entity.World, adjustedStart);
        blockAccessor.Commit();
        bs.PlaceEntitiesAndBlockEntities(blockAccessor, entity.World, adjustedStart, bs.BlockCodes, bs.ItemCodes);

        // drop empty item on the ground and remove empty from inventory
        ItemStack empty = new(entity.World.GetItem(_emptyBag), slot.StackSize);
        if (Config.PutTentInInventoryOnUse) {
            ItemStack sinkStack = slot.Itemstack.Clone();
            slot.Itemstack.StackSize = 0;
            slot.Itemstack = empty;
            slot.OnItemSlotModified(sinkStack);
        } else {
            entity.World.SpawnItemEntity(empty, blockSel.Position.ToVec3d().Add(0, 1 - y, 0));
            slot.TakeOutWhole();
        }

        // consume player saturation
        entity.ReduceOnlySaturation(Config.BuildEffort);
    }

    private bool CanPack(EntityPlayer entity, IBlockAccessor blockAccessor, BlockPos start, BlockPos end) {
        List<BlockPos> blocks = new();
        bool notified = false;

        blockAccessor.WalkBlocks(start, end, (block, posX, posY, posZ) => {
            BlockPos pos = new(posX, posY, posZ, 0);
            if (!entity.World.Claims.TryAccess(entity.Player, pos, EnumBlockAccessFlags.BuildOrBreak)) {
                notified = true;
                blocks.Add(pos);
            } else if (IsBannedBlock(block.Code)) {
                if (!notified) {
                    SendClientError(entity, Lang.IllegalItemError(block.GetPlacedBlockName(entity.World, pos)));
                    notified = true;
                }

                blocks.Add(pos);
            }
        });

        return !ShouldHighlightBlocks(entity, blocks);
    }

    private bool CanUnpack(EntityPlayer entity, IBlockAccessor blockAccessor, BlockPos start, BlockPos end) {
        List<BlockPos> blocks = new();
        bool notified = false;

        blockAccessor.WalkBlocks(start, end, (block, posX, posY, posZ) => {
            BlockPos pos = new(posX, posY, posZ, 0);
            if (!entity.World.Claims.TryAccess(entity.Player, pos, EnumBlockAccessFlags.BuildOrBreak)) {
                notified = true;
                blocks.Add(pos);
            } else if (pos.Y == start.Y) {
                // ReSharper disable once InvertIf
                if (Config.RequireFloor && !block.SideSolid[BlockFacing.indexUP]) {
                    if (!notified) {
                        SendClientError(entity, Lang.SolidGroundError());
                        notified = true;
                    }

                    blocks.Add(pos);
                }
            } else if (!IsReplaceable(block)) {
                if (!notified) {
                    SendClientError(entity, Lang.ClearAreaError());
                    notified = true;
                }

                blocks.Add(pos);
            }
        });

        return !ShouldHighlightBlocks(entity, blocks);
    }

    private static bool IsBannedBlock(AssetLocation? block) {
        if (block == null) {
            return false;
        }

        foreach (string banned in Config.BannedBlocks) {
            AssetLocation code = new(banned);
            if (code.Equals(block)) {
                return true;
            }

            if (code.IsWildCard && WildcardUtil.GetWildcardValue(code, block) != null) {
                return true;
            }
        }

        return false;
    }

    private bool ShouldHighlightBlocks(EntityPlayer entity, List<BlockPos> blocks) {
        if (blocks.Count <= 0) {
            return false;
        }

        if (_highlightId > 0) {
            entity.Api.Event.UnregisterCallback(_highlightId);
        }

        int color = Config.HighlightErrorColor.ToColor().Reverse();
        List<int> colors = Enumerable.Repeat(color, blocks.Count).ToList();
        entity.World.HighlightBlocks(entity.Player, 1337, blocks, colors);

        _highlightId = entity.Api.Event.RegisterCallback(_ => {
            List<BlockPos> empty = Array.Empty<BlockPos>().ToList();
            entity.World.HighlightBlocks(entity.Player, 1337, empty);
        }, 2500);

        return true;
    }
}
