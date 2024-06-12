using System;
using System.Collections.Generic;
using System.Linq;
using TentBag.Configuration;
using TentBag.util;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TentBag.Items;

public class ItemTentBag : Item {
    private static readonly AssetLocation _emptyBag = new("tentbag:tentbag-empty");
    private static readonly AssetLocation _packedBag = new("tentbag:tentbag-packed");

    private static readonly AssetLocation[] _bannedBlocks = {
        new("game:log-grown-*"),
        new("game:log-resin-*"),
        new("game:log-resinharvested-*"),
        new("game:statictranslocator-*"),
        new("game:teleporterbase"),
        new("game:crop-*"),
        new("game:herb-*"),
        new("game:mushroom-*"),
        new("game:smallberrybush-*"),
        new("game:bigberrybush-*"),
        new("game:water-*"),
        new("game:lava-*"),
        new("game:farmland-*"),
        new("game:rawclay-*"),
        new("game:peat-*"),
        new("game:rock-*"),
        new("game:ore-*"),
        new("game:crock-burned-*"),
        new("game:bowl-meal"),
        new("game:claypot-cooked"),
        new("game:anvil-*"),
        new("game:forge")
    };

    private static readonly List<BlockPos> _emptyBlockPosList = Array.Empty<BlockPos>().ToList();
    private const int _highlightColor = 0xFF | (0x2F << 24);

    private static Config Config => TentBag.Instance.Config;
    private static bool IsAirOrNull(Block? block) => block is not { Replaceable: < 9505 };
    private static bool IsPlantOrRock(Block? block) => Config.ReplacePlantsAndRocks && block?.Replaceable is >= 5500 and <= 6500;
    private static bool IsReplaceable(Block? block) => IsAirOrNull(block) || IsPlantOrRock(block);

    private static void SendClientError(EntityPlayer entity, string error) => TentBag.Instance.SendClientError(entity.Player, error);

    private long _highlightId;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
        if (blockSel == null || byEntity is not EntityPlayer entity) {
            return;
        }

        handling = EnumHandHandling.PreventDefaultAction;

        if (api.Side != EnumAppSide.Server) {
            return;
        }

        string contents = slot.Itemstack.Attributes.GetString("tent-contents");
        IBlockAccessor blockAccessor = entity.World.BlockAccessor;
        if (contents == null) {
            PackTent(entity, blockAccessor, blockSel, slot);
        } else {
            UnpackTent(entity, blockAccessor, blockSel, slot, contents);
        }
    }

    private void PackTent(EntityPlayer entity, IBlockAccessor blockAccessor, BlockSelection blockSel, ItemSlot slot) {
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

        // clear area in world
        blockAccessor.WalkBlocks(start, end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = new(posX, posY, posZ, 0);
            blockAccessor.SetBlock(0, pos);
            blockAccessor.MarkBlockModified(pos);
        });
        blockAccessor.Commit();


        // drop packed tentbag on the ground and remove empty from inventory
        ItemStack packed = new(entity.World.GetItem(_packedBag), slot.StackSize);
        packed.Attributes.SetString("tent-contents", bs.ToJson());
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

    private void UnpackTent(EntityPlayer entity, IBlockAccessor blockAccessor, BlockSelection blockSel, ItemSlot slot, string contents) {
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
            SendClientError(entity, Lang.Get("tentbag:tentbag-unpack-error"));
            return;
        }

        // clear area in world
        blockAccessor.WalkBlocks(start.AddCopy(0, 1, 0), end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = new(posX, posY, posZ, 0);
            blockAccessor.SetBlock(0, pos);
            blockAccessor.MarkBlockModified(pos);
        });
        blockAccessor.Commit();

        // paste the schematic into the world
        BlockPos adjustedStart = bs.AdjustStartPos(start.Add(Config.Radius, 1, Config.Radius), EnumOrigin.BottomCenter);
        bs.ReplaceMode = EnumReplaceMode.ReplaceAll;
        bs.Place(blockAccessor, entity.World, adjustedStart);
        blockAccessor.Commit();
        bs.PlaceEntitiesAndBlockEntities(blockAccessor, entity.World, adjustedStart, bs.BlockCodes, bs.ItemCodes);

        // drop empty tentbag on the ground and remove empty from inventory
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
                    SendClientError(entity, Lang.Get("tentbag:tentbag-illegal-item", block.GetPlacedBlockName(entity.World, pos)));
                    notified = true;
                }

                blocks.Add(pos);
            }
        });

        return !HighlightBlocks(entity, blocks);
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
                        SendClientError(entity, Lang.Get("tentbag:tentbag-solid-ground"));
                        notified = true;
                    }

                    blocks.Add(pos);
                }
            } else if (!IsReplaceable(block)) {
                if (!notified) {
                    SendClientError(entity, Lang.Get("tentbag:tentbag-clear-area"));
                    notified = true;
                }

                blocks.Add(pos);
            }
        });

        return !HighlightBlocks(entity, blocks);
    }

    private static bool IsBannedBlock(AssetLocation? needle) {
        if (needle == null) {
            return false;
        }

        foreach (AssetLocation hay in _bannedBlocks) {
            if (hay.Equals(needle)) {
                return true;
            }

            if (hay.IsWildCard && WildcardUtil.GetWildcardValue(hay, needle) != null) {
                return true;
            }
        }

        return false;
    }

    private bool HighlightBlocks(EntityPlayer entity, List<BlockPos> blocks) {
        if (_highlightId > 0) {
            api.Event.UnregisterCallback(_highlightId);
        }

        if (blocks.Count <= 0) {
            return false;
        }

        entity.World.HighlightBlocks(entity.Player, 1337, blocks, Enumerable.Repeat(_highlightColor, blocks.Count).ToList());

        _highlightId = api.Event.RegisterCallback(_ => entity.World.HighlightBlocks(entity.Player, 1337, _emptyBlockPosList), 2500);

        return true;
    }
}
