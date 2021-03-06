﻿using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Entities;
using BlockLimiter.Utility;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using BlockLimiter.Settings;
using VRage.Network;
using Sandbox.Definitions;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRage.Game;
using VRageMath;


namespace BlockLimiter.Patch
{
    [PatchShim]
    public static class BuildBlockPatch
    {
     
        public static void Patch(PatchContext ctx)
        {
            var t = typeof(MyCubeGrid);
            var aMethod = t.GetMethod("BuildBlocksRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(aMethod).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlocksRequest),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
            var bMethod = t.GetMethod("BuildBlocksAreaRequest", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            ctx.GetPattern(bMethod).Prefixes.Add(typeof(BuildBlockPatch).GetMethod(nameof(BuildBlocksArea),BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static));
        }
     

        /// <summary>
        /// Checks blocks being built in creative with multiblock placement.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="area"></param>
        /// <returns></returns>
        private static bool BuildBlocksArea(MyCubeGrid __instance, MyCubeGrid.MyBlockBuildArea area)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(area.DefinitionId);
            var grid = __instance;

            int blocksToBuild = (int) area.BuildAreaSize.X * (int) area.BuildAreaSize.Y * (int) area.BuildAreaSize.Z;


            if (grid == null)
            {
                BlockLimiter.Instance.Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }

            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);
            if (Block.IsWithinLimits(def, playerId, grid.EntityId, blocksToBuild, out var limitName)) return true;
            BlockLimiter.Instance.Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing {def.ToString().Substring(16)} due to limits");

            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string>(){def.ToString().Substring(16)},limitName);

            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();

            return false;


        }


        /// <summary>
        /// Checks blocks being placed on grids.
        /// </summary>
        /// <param name="__instance"></param>
        /// <param name="locations"></param>
        /// <returns></returns>
        private static bool BuildBlocksRequest(MyCubeGrid __instance, HashSet<MyCubeGrid.MyBlockLocation> locations)
        {
            
            if (!BlockLimiterConfig.Instance.EnableLimits) return true;

            var grid = __instance;
            if (grid == null)
            {
                BlockLimiter.Instance.Log.Debug("Null grid in BuildBlockHandler");
                return true;
            }


            var def = MyDefinitionManager.Static.GetCubeBlockDefinition(locations.FirstOrDefault().BlockDefinition);

            if (def == null) return true;
            
           
            var remoteUserId = MyEventContext.Current.Sender.Value;
            var playerId = Utilities.GetPlayerIdFromSteamId(remoteUserId);

            if (Block.IsWithinLimits(def, playerId, grid.EntityId,1, out var limitName)) return true;
            BlockLimiter.Instance.Log.Info($"Blocked {Utilities.GetPlayerNameFromSteamId(remoteUserId)} from placing {def.ToString().Substring(16)} due to limits");
            var msg = Utilities.GetMessage(BlockLimiterConfig.Instance.DenyMessage,new List<string> {def.ToString().Substring(16)},limitName);
            if (remoteUserId != 0 && MySession.Static.Players.IsPlayerOnline(playerId))
                BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>()?
                    .SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, msg, Color.Red, remoteUserId);
            Utilities.SendFailSound(remoteUserId);
            Utilities.ValidationFailed();
            return false;

        }
            

    }
}
