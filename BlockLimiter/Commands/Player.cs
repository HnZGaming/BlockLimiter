﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using BlockLimiter.ProcessHandlers;
using BlockLimiter.Settings;
using BlockLimiter.Utility;
using NLog;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch;
using Torch.Commands;
using Torch.Commands.Permissions;
using Torch.Managers;
using Torch.Mod;
using Torch.Mod.Messages;
using Torch.Utils;
using VRage.Dedicated.RemoteAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Network;
using VRage.Utils;
using VRageMath;

namespace BlockLimiter.Commands
{
    [Category("blocklimit")]
    public partial class Player:CommandModule
    {
        private static Dictionary<ulong, DateTime> _updateCommandTimeout = new Dictionary<ulong, DateTime>();

        [Command("update mylimit")]
        [Permission(MyPromoteLevel.None)]
        public void UpdateMyLimit()
        {
            if (Context.Player == null || Context.Player.SteamUserId == 0)
            {
                Context.Respond("This command can only be run by a player in game");
                return;
            }

            var steamId = Context.Player.SteamUserId;

            if (!_updateCommandTimeout.TryGetValue(steamId, out var lastRun))
            {
                _updateCommandTimeout[steamId] = DateTime.Now;

                Utility.UpdateLimits.PlayerLimit(Context.Player.IdentityId);
                Context.Respond("Limits Updated");
                return;

            }

            var diff = DateTime.Now - lastRun;
            if (diff.TotalMinutes < 5)
            {
                var totalRemaining = TimeSpan.FromMinutes(5) - diff;
                Context.Respond($"Cooldown in effect.  Try again in {totalRemaining.TotalSeconds:N0} seconds");
                return;
            }

            _updateCommandTimeout[steamId] = DateTime.Now;

            Utility.UpdateLimits.PlayerLimit(Context.Player.IdentityId);
            Context.Respond("Limits Updated");



        }
        [Command("mylimit", "list current player status")]
        [Permission(MyPromoteLevel.None)]
        public void MyLimit()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }
            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond("Command can only be run in-game by players");
                return;
            }

            var playerId = Context.Player.IdentityId;

            var newList = BlockLimiterConfig.Instance.AllLimits;

            if (!newList.Any())
            {
                Context.Respond("No limit item found");
                return;
            }
            

            var sb = Utilities.GetLimit(playerId);

            if (sb.Length == 0)
            {
                Context.Respond("You have no block within set limit");
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"PlayerLimit",sb.ToString()),Context.Player.SteamUserId);

        }

        [Command("limits", "gets list of limits and there settings")]
        [Permission(MyPromoteLevel.None)]
        public void GetLimits()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                Context.Respond("Plugin disabled");
                return;
            }

            var sb = new StringBuilder();
            var limiterLimits = BlockLimiterConfig.Instance.AllLimits.ToList();
            if (!limiterLimits.Any())
            {
                Context.Respond("No limit item found");
                return;
            }

            sb.AppendLine($"Found {limiterLimits.Where(x=>x.BlockList.Any()).ToList().Count()} items");
            foreach (var item in limiterLimits.Where(x=>x.BlockList.Any()))
            {
                var name = string.IsNullOrEmpty(item.Name) ? "No Name" : item.Name;
                sb.AppendLine();
                sb.AppendLine(name);
                item.BlockList.ForEach(x=>sb.Append($"[{x}] "));
                sb.AppendLine();
                sb.AppendLine($"GridType: {item.GridTypeBlock}");
                sb.AppendLine($"Limits:       {item.Limit}");
                sb.AppendLine($"PlayerLimit:  {item.LimitPlayers}");
                sb.AppendLine($"FactionLimit: {item.LimitFaction}");
                sb.AppendLine($"GridLimit:    {item.LimitGrids}");
            }

            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }

            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of Limits",sb.ToString()),Context.Player.SteamUserId);
        }


        
        [Command("pairnames", "gets the list of all pair names possible")]
        [Permission(MyPromoteLevel.None)]
        public void ListPairNames(string blockType=null)
        {

            var sb = new StringBuilder();

            var allDef = MyDefinitionManager.Static.GetAllDefinitions();

            var def = new List<MyDefinitionBase>();

            if (!string.IsNullOrEmpty(blockType))
            {
                foreach (var defBase in allDef)
                {
                    if (!defBase.Id.TypeId.ToString().Substring(16).Equals(blockType,StringComparison.OrdinalIgnoreCase))
                        continue;
                    def.Add(defBase);
                }

                if (!def.Any())
                {
                    Context.Respond($"Can't find any definition for {blockType}");
                    return;
                }

            }

            else
            {
                def.AddRange(allDef);
            }

            if (!def.Any())
            {
                Context.Respond("Na Bruh!");
                return;
            }

            sb.AppendLine($"Total of {def.Count} definitions found on server");
            var definitionDictionary = new Dictionary<MyModContext, List<string>>();
            foreach (var myDefinitionId in def)
            {
                if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(myDefinitionId.Id, out var x)) continue;
                if (myDefinitionId.Context == null) continue;
                if (!definitionDictionary.ContainsKey(myDefinitionId.Context))
                {
                    definitionDictionary[myDefinitionId.Context] = new List<string> {x.BlockPairName};
                    continue;
                }

                if (definitionDictionary[myDefinitionId.Context].Contains(x.BlockPairName)) continue;
                definitionDictionary[myDefinitionId.Context].Add(x.BlockPairName);
            }

            foreach (var (context,thisList) in definitionDictionary)
            {
                sb.AppendLine(context.IsBaseGame ? $"[{thisList.Count} Vanilla blocks]" : $"[{thisList.Count} blocks --- {context.ModName} - {context.ModId}]");
                
                thisList.ForEach(x=>sb.AppendLine(x));
                sb.AppendLine();
            }

            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }


            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of pair names",sb.ToString()),Context.Player.SteamUserId);
        }

        [Command("definitions", "gets the list of all pair names possible")]
        [Permission(MyPromoteLevel.None)]
        public void ListBlockDefinitions(string blockType=null)
        {

            var sb = new StringBuilder();

            var allDef = MyDefinitionManager.Static.GetAllDefinitions();

            var def = new List<MyDefinitionBase>();

            if (!string.IsNullOrEmpty(blockType))
            {
                foreach (var defBase in allDef)
                {
                    if (!defBase.Id.TypeId.ToString().Substring(16).Equals(blockType,StringComparison.OrdinalIgnoreCase))
                        continue;
                    def.Add(defBase);
                }

                if (!def.Any())
                {
                    Context.Respond($"Can't find any definition for {blockType}");
                    return;
                }

            }

            else
            {
                def.AddRange(allDef);
            }

            if (!def.Any())
            {
                Context.Respond("Na Bruh!");
                return;
            }

            sb.AppendLine($"Total of {def.Count} definitions found on server");
            var definitionDictionary = new Dictionary<MyModContext, List<string>>();
            foreach (var myDefinitionId in def)
            {
                if (!MyDefinitionManager.Static.TryGetCubeBlockDefinition(myDefinitionId.Id, out var x)) continue;
                if (myDefinitionId.Context == null) continue;
                if (!definitionDictionary.ContainsKey(myDefinitionId.Context))
                {
                    definitionDictionary[myDefinitionId.Context] = new List<string> {x.Id.ToString().Substring(16)};
                    continue;
                }

                if (definitionDictionary[myDefinitionId.Context].Contains(x.BlockPairName)) continue;
                definitionDictionary[myDefinitionId.Context].Add(x.Id.ToString().Substring(16));
            }

            foreach (var (context,thisList) in definitionDictionary)
            {
                sb.AppendLine(context.IsBaseGame ? $"[{thisList.Count} Vanilla blocks]" : $"[{thisList.Count} blocks --- {context.ModName} - {context.ModId}]");
                
                thisList.ForEach(x=>sb.AppendLine(x));
                sb.AppendLine();
            }

            if (Context.Player == null || Context.Player.IdentityId == 0)
            {
                Context.Respond(sb.ToString());
                return;
            }


            ModCommunication.SendMessageTo(new DialogMessage(BlockLimiterConfig.Instance.ServerName,"List of pair names",sb.ToString()),Context.Player.SteamUserId);
        }

    }


}