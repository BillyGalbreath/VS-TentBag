using System;
using TentBag.Configuration;
using TentBag.Extensions;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace TentBag.Items;

public class ItemTentBag : Item {
    private static readonly AssetLocation EmptyBag = new("tentbag:tentbag-empty");
    private static readonly AssetLocation PackedBag = new("tentbag:tentbag-packed");

    private static readonly AssetLocation[] BannedBlocks = {
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

    private static bool IsAirOrNull(Block? block) => block is not { Replaceable: < 9505 };
    private bool IsPlantOrRock(Block? block) => _config.ReplacePlantsAndRocks && block?.Replaceable is >= 5500 and <= 6500;
    private bool IsReplaceable(Block? block) => IsAirOrNull(block) || IsPlantOrRock(block);

    private static void SendClientError(EntityPlayer entity, string error) => TentBag.Instance.SendClientError(entity.Player, error);

    private Config _config = null!;

    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection? blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling) {
        if (blockSel == null || byEntity is not EntityPlayer entity) {
            return;
        }

        handling = EnumHandHandling.PreventDefaultAction;

        if (api.Side != EnumAppSide.Server) {
            return;
        }

        _config = Config.GetConfig();

        string contents = slot.Itemstack.Attributes.GetString("tent-contents");
        if (contents == null) {
            PackTent(entity, blockSel, slot);
        } else {
            UnpackTent(entity, blockSel, slot, contents);
        }
    }

    private void PackTent(EntityPlayer entity, BlockSelection blockSel, ItemSlot slot) {
        int y = IsPlantOrRock(entity.World.BulkBlockAccessor.GetBlock(blockSel.Position)) ? 1 : 0;

        BlockPos start = blockSel.Position.AddCopy(-_config.Radius, 1 - y, -_config.Radius);
        BlockPos end = blockSel.Position.AddCopy(_config.Radius, Math.Max(_config.Height, 3), _config.Radius);

        if (!CanPack(entity, start, end)) {
            return;
        }

        // create schematic of area
        BlockSchematic bs = new();
        bs.AddAreaWithoutEntities(entity.World, start, end);
        bs.Pack(entity.World, start);

        // clear area in world
        entity.World.BulkBlockAccessor.WalkBlocks(start, end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = TentBag.Instance.Compat!.NewBlockPos(posX, posY, posZ);
            entity.World.BulkBlockAccessor.SetBlock(0, pos);
            entity.World.BulkBlockAccessor.MarkBlockModified(pos);
        });
        entity.World.BulkBlockAccessor.Commit();


        // drop packed tentbag on the ground and remove empty from inventory
        ItemStack packed = new(entity.World.GetItem(PackedBag), slot.StackSize);
        packed.Attributes.SetString("tent-contents", bs.ToJson());
        if (_config.PutTentInInventoryOnUse) {
            ItemStack sinkStack = slot.Itemstack.Clone();
            slot.Itemstack.StackSize = 0;
            slot.Itemstack = packed;
            slot.OnItemSlotModified(sinkStack);
        } else {
            entity.World.SpawnItemEntity(packed, blockSel.Position.ToVec3d().Add(0, 1 - y, 0));
            slot.TakeOutWhole();
        }

        // consume player saturation
        entity.ReduceOnlySaturation(_config.BuildEffort);
    }

    private void UnpackTent(EntityPlayer entity, BlockSelection blockSel, ItemSlot slot, string contents) {
        int y = IsPlantOrRock(entity.World.BulkBlockAccessor.GetBlock(blockSel.Position)) ? 1 : 0;

        BlockPos start = blockSel.Position.AddCopy(-_config.Radius, 0 - y, -_config.Radius);
        BlockPos end = blockSel.Position.AddCopy(_config.Radius, Math.Max(_config.Height, 3), _config.Radius);

        if (!CanUnpack(entity, start, end)) {
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
        entity.World.BulkBlockAccessor.WalkBlocks(start.AddCopy(0, 1, 0), end, (block, posX, posY, posZ) => {
            if (block.BlockId == 0) {
                return;
            }

            BlockPos pos = TentBag.Instance.Compat!.NewBlockPos(posX, posY, posZ);
            entity.World.BulkBlockAccessor.SetBlock(0, pos);
            entity.World.BulkBlockAccessor.MarkBlockModified(pos);
        });
        entity.World.BulkBlockAccessor.Commit();

        // paste the schematic into the world
        BlockPos adjustedStart = bs.AdjustStartPos(start.Add(_config.Radius, 1, _config.Radius), EnumOrigin.BottomCenter);
        bs.ReplaceMode = EnumReplaceMode.ReplaceAll;
        bs.Place(entity.World.BulkBlockAccessor, entity.World, adjustedStart);
        entity.World.BulkBlockAccessor.Commit();
        TentBag.Instance.Compat!.InvokePlaceEntitiesAndBlockEntities(bs, entity.World.BulkBlockAccessor, entity.World, adjustedStart, bs.BlockCodes, bs.ItemCodes);

        // drop empty tentbag on the ground and remove empty from inventory
        ItemStack empty = new(entity.World.GetItem(EmptyBag), slot.StackSize);
        if (_config.PutTentInInventoryOnUse) {
            ItemStack sinkStack = slot.Itemstack.Clone();
            slot.Itemstack.StackSize = 0;
            slot.Itemstack = empty;
            slot.OnItemSlotModified(sinkStack);
        } else {
            entity.World.SpawnItemEntity(empty, blockSel.Position.ToVec3d().Add(0, 1 - y, 0));
            slot.TakeOutWhole();
        }

        // consume player saturation
        entity.ReduceOnlySaturation(_config.BuildEffort);
    }

    private static bool CanPack(EntityPlayer entity, BlockPos start, BlockPos end) {
        bool allowed = true;

        entity.World.BulkBlockAccessor.SearchBlocks(start, end, (block, pos) => {
            if (!entity.World.Claims.TryAccess(entity.Player, pos, EnumBlockAccessFlags.BuildOrBreak)) {
                return allowed = false;
            }

            // ReSharper disable once InvertIf
            if (IsBannedBlock(block.Code)) {
                SendClientError(entity, Lang.Get("tentbag:tentbag-illegal-item", block.Code));
                return allowed = false;
            }

            return true;
        });

        return allowed;
    }

    private bool CanUnpack(EntityPlayer entity, BlockPos start, BlockPos end) {
        bool allowed = true;

        entity.World.BulkBlockAccessor.SearchBlocks(start, end, (block, pos) => {
            if (!entity.World.Claims.TryAccess(entity.Player, pos, EnumBlockAccessFlags.BuildOrBreak)) {
                return allowed = false;
            }

            if (pos.Y == start.Y) {
                if (!_config.RequireFloor || block.SideSolid[BlockFacing.indexUP]) {
                    return allowed = true;
                }

                SendClientError(entity, Lang.Get("tentbag:tentbag-solid-ground"));
                return allowed = false;
            }

            if (IsReplaceable(block)) {
                return allowed = true;
            }

            SendClientError(entity, Lang.Get("tentbag:tentbag-clear-area"));
            return allowed = false;
        });

        return allowed;
    }

    private static bool IsBannedBlock(AssetLocation? needle) {
        if (needle == null) {
            return false;
        }

        foreach (AssetLocation hay in BannedBlocks) {
            if (hay.Equals(needle)) {
                return true;
            }

            if (hay.IsWildCard && WildcardUtil.GetWildcardValue(hay, needle) != null) {
                return true;
            }
        }

        return false;
    }
}
