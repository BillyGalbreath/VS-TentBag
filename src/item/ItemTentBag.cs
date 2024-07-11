using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace tentbag.item;

public class ItemTentBag : Item {
    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo) {
        if (withDebugInfo) {
            dsc.AppendLine($"<font color=\"#bbbbbb\">Id:{Id}</font>");
            dsc.AppendLine($"<font color=\"#bbbbbb\">Code: {Code}</font>");
        }
        foreach (CollectibleBehavior behavior in CollectibleBehaviors) {
            behavior.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
        }
        string itemDescText = GetItemDescText();
        if (itemDescText.Length > 0 && dsc.Length > 0) {
            dsc.Append('\n');
        }
        dsc.Append(itemDescText);
        if (Code != null && Code.Domain != "game") {
            dsc.AppendLine(Lang.Get("Mod: {0}", (api.ModLoader.GetMod(Code.Domain)?.Info.Name ?? Code.Domain)));
        }
    }
}
