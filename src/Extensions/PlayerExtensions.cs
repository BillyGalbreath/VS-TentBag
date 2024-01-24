using System;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace TentBag.Extensions;

public static class PlayerExtensions {
    public static void ReduceOnlySaturation(this EntityPlayer entity, float amount) {
        EntityBehaviorHunger? hunger = entity.GetBehavior<EntityBehaviorHunger>();
        if (hunger == null) {
            return;
        }

        hunger.Saturation = Math.Max(0, hunger.Saturation - amount);
        hunger.SetField("sprintCounter", 0);
    }
}
