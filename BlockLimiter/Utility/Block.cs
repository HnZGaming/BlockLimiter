using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlockLimiter.Patch;
using BlockLimiter.Settings;
using Sandbox;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.API.Managers;
using Torch.Managers.ChatManager;
using VRage.Collections;
using VRage.Game;
using VRageMath;

namespace BlockLimiter.Utility
{
    public static class Block
    {
        private static readonly HashSet<LimitItem> Limits = BlockLimiterConfig.Instance.AllLimits;


        private static void KillBlock(MyCubeBlock block)
        {
            if (!(block is MyFunctionalBlock fBlock) || BlockSwitchPatch.KeepOffBlocks.Contains(fBlock))return;
            BlockSwitchPatch.KeepOffBlocks.Add(fBlock);
        }
        public static void KillBlocks(List<MySlimBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (!(block.FatBlock is MyFunctionalBlock fBlock) || BlockSwitchPatch.KeepOffBlocks.Contains(fBlock)) continue;
                BlockSwitchPatch.KeepOffBlocks.Add(fBlock);
            }
        }

        public static bool IsWithinLimits(MyCubeBlockDefinition block, long playerId, MyObjectBuilder_CubeGrid grid, out string limitName)
        {
            limitName = null;
            var allow = true;
            if (block == null) return true;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            if (grid != null && Grid.IsSizeViolation(grid)) return false;

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                limitName = item.Name;
                if (!item.BlockList.Any() || !item.IsMatch(block)) continue;
                
                if ((Utilities.IsExcepted(playerId,item) || (grid != null && Utilities.IsExcepted(grid.EntityId,item))))
                    continue;



                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {

                    return false;
                }

                if (grid != null && item.IsGridType(grid,playerId))
                {
                    var gridId = grid.EntityId;

                    if (gridId > 0 && item.LimitGrids && item.FoundEntities.TryGetValue(gridId, out var gCount))
                    {

                        
                        if (gCount >= item.Limit)
                        {
                            allow = false;
                            break;
                        }

                    

                    }
                }


                if (playerId > 0 && item.LimitPlayers && item.FoundEntities.TryGetValue(playerId, out var pCount))
                {

                    if (pCount >= item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }



                if (faction == null || !item.LimitFaction || !item.FoundEntities.TryGetValue(faction.FactionId, out var fCount)) continue;
                {

                    if (fCount < item.Limit) continue;
                    allow = false;
                    break;
                }
                
                
            }

            return allow;
            
        }

        public static bool IsWithinLimits(MyCubeBlockDefinition def, long ownerId, long gridId, int count, out string limit)
        {
            limit = null;
            if (def == null || Math.Abs(ownerId + gridId) < 1) return true;


            var ownerFaction = MySession.Static.Factions.GetPlayerFaction(ownerId);


            var allow = true;

            if (Grid.IsSizeViolation(gridId)) return false;

            if (BlockLimiterConfig.Instance.AllLimits.Count == 0) return true;

            foreach (var item in BlockLimiterConfig.Instance.AllLimits)
            {
                limit = item.Name;
                if (!item.IsMatch(def)) continue;
                
                if ((ownerId > 0 && Utilities.IsExcepted(ownerId,item)) || (gridId > 0 && Utilities.IsExcepted(gridId,item)))
                    continue;

                var foundGrid = GridCache.TryGetGridById(gridId, out var grid);

                if (foundGrid && !item.IsGridType(grid)) continue;

                if (item.Limit == 0 && (item.LimitGrids || item.LimitPlayers || item.LimitFaction))
                {
                    return false;
                }


                if (item.LimitGrids && gridId > 0 && item.FoundEntities.TryGetValue(gridId, out var gCount))
                {
                    if (foundGrid && item.IsGridType(grid))
                    {
                        if (gCount + count > item.Limit)
                        {
                            allow = false;
                            break;
                        }

                    }
                }


                if (ownerId > 0 && item.LimitPlayers && item.FoundEntities.TryGetValue(ownerId, out var pCount))
                {

                    if (pCount + count > item.Limit)
                    {
                        allow = false;
                        break;
                    }
                }



                if (ownerFaction == null || !item.LimitFaction || !item.FoundEntities.TryGetValue(ownerFaction.FactionId, out var fCount)) continue;
                {

                    if (fCount + count <= item.Limit) continue;
                    allow = false;
                    break;
                }
                
                
            }


            return allow;

        }

        public static bool IsOwner(MySlimBlock block, long playerId)
        {
            return block.BuiltBy == playerId || block.OwnerId == playerId;
        }



        public static void IncreaseCount(MyCubeBlockDefinition def, long playerId, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;

            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);
            
            foreach (var limit in Limits)
            {
                if (!limit.IsMatch(def)) continue;

                var foundGrid = GridCache.TryGetGridById(gridId, out var grid);

                if (foundGrid && !limit.IsGridType(grid)) continue;

                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(playerId)) continue;
                    if (foundGrid && MySession.Static.Players.IdentityIsNpc(GridCache.GetOwners(grid).FirstOrDefault())) continue;
                    
                }

                if (limit.LimitPlayers && playerId > 0)
                    limit.FoundEntities.AddOrUpdate(playerId, amount, (l, i) => i+amount);

                if (limit.LimitGrids && gridId > 0)
                    limit.FoundEntities.AddOrUpdate(gridId, amount, (l, i) => i+amount);

                if (limit.LimitFaction && faction != null)
                    limit.FoundEntities.AddOrUpdate(faction.FactionId, amount, (l, i) => i+amount);


            }

        }

        public static void DecreaseCount(MyCubeBlockDefinition def, long playerId, int amount = 1, long gridId = 0)
        {
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var faction = MySession.Static.Factions.GetPlayerFaction(playerId);

            foreach (var limit in Limits)
            {
                if (!limit.IsMatch(def))continue;

                var foundGrid = GridCache.TryGetGridById(gridId, out var grid);

                if (foundGrid && !limit.IsGridType(grid))
                {
                    limit.FoundEntities.Remove(gridId);
                    continue;
                }

                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(playerId))
                    {
                        limit.FoundEntities.Remove(playerId);
                        continue;
                    }
                    if (foundGrid && MySession.Static.Players.IdentityIsNpc(GridCache.GetOwners(grid).FirstOrDefault())) continue;
                    
                }

                if (limit.LimitPlayers && playerId > 0)
                    limit.FoundEntities.AddOrUpdate(playerId, 0, (l, i) => Math.Max(0,i - amount));
                if (limit.LimitGrids && gridId > 0)
                    limit.FoundEntities.AddOrUpdate(gridId, 0, (l, i) => Math.Max(0,i - amount));
                if (limit.LimitFaction && faction != null)
                    limit.FoundEntities.AddOrUpdate(faction.FactionId, 0, (l, i) => Math.Max(0,i - amount));
                limit.ClearEmptyEntities();
            }

        }

        public static bool CanAdd(List<MyObjectBuilder_CubeBlock> blocks, long id, out List<MyObjectBuilder_CubeBlock> nonAllowedBlocks)
        {
            var newList = new List<MyObjectBuilder_CubeBlock>();
            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                nonAllowedBlocks = newList;
                return true;
            }
            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(id)) continue;
                }

                limit.FoundEntities.TryGetValue(id, out var currentCount);
                if(Utilities.IsExcepted(id, limit)) continue;
                var affectedBlocks = blocks.Where(x => limit.IsMatch(Utilities.GetDefinition(x))).ToList();
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                newList.AddRange(affectedBlocks.Where(x=>!newList.Contains(x)).Take(take));
            }

            nonAllowedBlocks = newList;
            return newList.Count == 0;
        }
       
        public static bool CanAdd(List<MySlimBlock> blocks, long id, out List<MySlimBlock> nonAllowedBlocks)
        {
            nonAllowedBlocks = new List<MySlimBlock>();

            if (!BlockLimiterConfig.Instance.EnableLimits)
            {
                return true;
            }

            foreach (var limit in BlockLimiterConfig.Instance.AllLimits)
            {
                if (limit.IgnoreNpcs)
                {
                    if (MySession.Static.Players.IdentityIsNpc(id)) continue;
                }

                if(Utilities.IsExcepted(id, limit)) continue;
                if (!limit.FoundEntities.TryGetValue(id, out var currentCount)) continue;
                var affectedBlocks = blocks.Where(x => limit.IsMatch(x.BlockDefinition)).ToList();
                if (affectedBlocks.Count <= limit.Limit - currentCount ) continue;
                var take = affectedBlocks.Count - (limit.Limit - currentCount);
                var list = nonAllowedBlocks;
                nonAllowedBlocks.AddRange(affectedBlocks.Where(x=>!list.Contains(x)).Take(take));
            }

            return nonAllowedBlocks.Count == 0;
        }


        public static void Punish(MyConcurrentDictionary<MySlimBlock, LimitItem.PunishmentType> removalCollection)
        {
            if (removalCollection.Count == 0) return;
            var log = BlockLimiter.Instance.Log;
            if (!BlockLimiterConfig.Instance.EnableLimits) return;
            var chatManager = BlockLimiter.Instance.Torch.CurrentSession.Managers.GetManager<ChatManagerServer>();
            lock (removalCollection)
            {
                Task.Run(() =>
                {
                    Parallel.ForEach(removalCollection, collective =>
                    {
                        var (block, punishment) = collective;
                        var ownerSteamId = MySession.Static.Players.TryGetSteamId(block.OwnerId);
                        if (block.IsDestroyed || block.FatBlock.Closed || block.FatBlock.MarkedForClose) return;
                        Color color = Color.Yellow;

                        switch (punishment)
                        {
                            case LimitItem.PunishmentType.None:
                                return;
                            case LimitItem.PunishmentType.DeleteBlock:
                                MySandboxGame.Static.Invoke(()=>
                                {
                                    block.CubeGrid.RemoveBlock(block);
                                },"BlockLimiter");
                                log.Info(
                                    $"Removed {block.BlockDefinition} from {block.CubeGrid.DisplayName}");
                                break;
                            case LimitItem.PunishmentType.ShutOffBlock:
                                KillBlock(block.FatBlock);
                                break;
                            case LimitItem.PunishmentType.Explode:
                                log.Info(
                                    $"Destroyed {block.BlockDefinition} from {block.CubeGrid.DisplayName}");
                                MySandboxGame.Static.Invoke(() =>
                                {
                                    block.DoDamage(block.BlockDefinition.MaxIntegrity, MyDamageType.Fire);
                                },"BlockLimiter");
                                break;
                            default:
                                return;
                        }

                       if (ownerSteamId != 0 && MySession.Static.Players.IsPlayerOnline(block.OwnerId)) 
                           chatManager?.SendMessageAsOther(BlockLimiterConfig.Instance.ServerName, $"Punishing {((MyTerminalBlock)block.FatBlock).CustomName} from {block.CubeGrid.DisplayName} with {punishment}",color,ownerSteamId);

                    });
                });
            }

        }

        public static void FixIds()
        {
            if (!BlockLimiterConfig.Instance.EnableLimits)
                return;
            var blockCache = new HashSet<MySlimBlock>();

            GridCache.GetBlocks(blockCache);

            Task.Run(() =>
            {
                Parallel.ForEach(blockCache, block =>
                {
                    if (block == null  || !block.BlockDefinition.ContainsComputer()) return;

                    if (block.OwnerId == block.BuiltBy) return;
                    if (block.OwnerId == 0 && block.BuiltBy > 0)
                    {
                        block.FatBlock.ChangeBlockOwnerRequest(block.BuiltBy,MyOwnershipShareModeEnum.Faction);

                        return;
                    }

                    block.TransferAuthorship(block.OwnerId);
                });
            });
        }


    }
}