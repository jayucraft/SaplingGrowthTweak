using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

[assembly: ModInfo(name: "SaplingGrowthTweak", modID: "saplinggrowthtweak", Side = "Universal", Version = "1.0.0", Authors = new string[] { "jayugg" },
    Description = "Edit sapling growth minimum temperature")]

namespace SaplingGrowthTweak
{
    [HarmonyPatch]
    public class SaplingGrowthTweakModSystem : ModSystem
    {
        public static ICoreAPI api;
        public Harmony harmony;
        public static TweakConfig config;
        public static ILogger Logger;

        public override void StartPre(ICoreAPI api)
        {
            SaplingGrowthTweakModSystem.api = api;
            Logger = api.Logger;
            try
            {
                config = api.LoadModConfig<TweakConfig>("SaplingGrowthTweak.json");
                if (config == null) {
                    config = new TweakConfig();
                    api.StoreModConfig(config, "SaplingGrowthTweak.json");
                }
            } catch (Exception e) {
                api.World.Logger.Error("Failed to load config, you probably made a typo: {0}", e);
                config = new TweakConfig();
            }
        }

        public override void StartServerSide(ICoreServerAPI api) {
            if (Harmony.HasAnyPatches(Mod.Info.ModID)) return;
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();
            Logger.Notification($"Correctly loaded temperature from config: {config?.Temperature}");
        }
        
        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }
        
        [HarmonyPatch(typeof(BlockEntitySapling), "CheckGrow", typeof(float))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CheckGrowTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            foreach (var instruction in codes)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && Math.Abs((float)instruction.operand - 5.0f) < 0.0001f)
                {
                    Logger.Notification($"Transpiling CheckGrow: {config.Temperature}");
                    instruction.operand = config.Temperature;
                    break;
                }
            }
            return codes;
        }
    }
    
    public class TweakConfig
    {
        public float Temperature { get; set; } = -100;
    }
}