using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

[assembly: ModInfo(name: "SaplingGrowthTweak", modID: "saplinggrowthtweak", Side = "Universal", Version = "1.0.1", Authors = new string[] { "jayugg" },
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
        }
        
        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
        }
        
        [HarmonyPatch(typeof(BlockEntitySapling), "CheckGrow", typeof(float))]
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> CheckGrowTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);
            MethodInfo helperMethod = typeof(SaplingGrowthTweakModSystem).GetMethod(nameof(AdjustTemperatureBasedOnConfig), BindingFlags.Static | BindingFlags.Public);

            // Define a local variable to store the float return value from the helper method
            LocalBuilder tempVar = il.DeclareLocal(typeof(float));

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && Math.Abs((float)codes[i].operand - 5.0f) < 0.0001f)
                {
                    Logger.Notification($"Transpiling CheckGrow");
                    
                    // Insert instructions to call the helper method and store its return value
                    codes.Insert(i, new CodeInstruction(OpCodes.Ldarg_0)); // Load `this` onto the evaluation stack
                    codes.Insert(i + 1, new CodeInstruction(OpCodes.Call, helperMethod)); // Call the helper method
                    codes.Insert(i + 2, new CodeInstruction(OpCodes.Stloc, tempVar.LocalIndex)); // Store the return value in the local variable

                    // Replace the Ldc_R4 instruction with one that loads the local variable
                    codes[i + 3] = new CodeInstruction(OpCodes.Ldloc, tempVar.LocalIndex);

                    break;
                }
            }
            return codes;
        }
        
        public static float AdjustTemperatureBasedOnConfig(BlockEntitySapling instance)
        {
            var code = instance.Block.Code;
            var mostSpecificMatch = config.TemperaturePerSaplingType
                .Where(keyVal => WildcardUtil.Match(keyVal.Key, code.ToString()))
                .OrderByDescending(keyVal => keyVal.Key.Length)
                .FirstOrDefault()
                .Value;
            Logger.Warning($"Adjusting temperature for {code} growth to: {mostSpecificMatch}");
            return mostSpecificMatch;
        }
    }
    
    public class TweakConfig
    {
        public Dictionary<string, float> TemperaturePerSaplingType { get; set; } = new Dictionary<string, float>()
        {
            { "@(.*)", -100 },
            { "@(.*)pine(.*)", -101 },
            { "@(.*)birch(.*)", -102 },
        };
    }
}