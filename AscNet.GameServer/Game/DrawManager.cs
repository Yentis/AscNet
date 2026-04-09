using AscNet.Common.Database;
using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.GameServer.Handlers;
using AscNet.Logging;
using AscNet.Table.V2.client.draw;
using AscNet.Table.V2.share.equip;
using AscNet.Table.V2.share.item;

namespace AscNet.GameServer.Game
{
    internal class DrawManager
    {
        public static readonly List<DrawSceneTable> drawSceneTables = TableReaderV2.Parse<DrawSceneTable>();
        static readonly Logger log = new(typeof(DrawManager), LogLevel.DEBUG, LogLevel.DEBUG);

        #region DrawTags
        public const int TagBase = 1;
        public const int TagEvent = 2;
        public const int TagSpecialEvent = 3;
        public const int TagTargetUniframe = 4;
        public const int TagCollab = 5;
        public const int TagEndlessSummerBlue = 6;
        public const int TagCUB = 7;
        #endregion

        #region Groups
        public const int GroupMemberTarget = 1;
        public const int GroupWeaponResearch = 2;
        public const int GroupTargetWeaponResearch = 4;
        public const int GroupDormitoryResearch = 6;
        public const int GroupThemedTargetWeapon = 10;
        public const int GroupThemedEventConstruct = 11;
        public const int GroupArrivalConstruct = 12;
        public const int GroupFateArrivalConstruct = 13;
        public const int GroupArrivalEventConstruct = 14;
        public const int GroupFateThemedConstruct = 15;
        public const int GroupTargetUniframe = 16;
        public const int GroupAnniversary = 17;
        public const int GroupFateAnniversaryLimited = 18;
        public const int GroupCollabTarget = 19;
        public const int GroupFateCollabTarget = 20;
        public const int GroupCollabWeaponTarget = 21;
        public const int GroupCUBTarget = 22;
        public const int GroupWishingTarget = 23;
        public const int GroupFateWishingTarget = 24;
        #endregion

        private readonly Dictionary<int, int> selectedDrawUp = new();

        public void SetUpDrawByGroupId(int groupId, int drawId)
        {
            if (selectedDrawUp.ContainsKey(groupId))
                selectedDrawUp[groupId] = drawId;
            else
                selectedDrawUp.Add(groupId, drawId);
        }

        public static List<DrawInfo> GetDrawInfosByGroup(int groupId)
        {
            List<DrawInfo> infos = new();

            switch (groupId)
            {
                case GroupArrivalConstruct:
                    // Querying every character scene that is omniframe.
                    infos.AddRange(drawSceneTables.Where(x => x.Type == 1 && TableReaderV2.CharacterTableDict.Values.Any(y =>
                    {
                        // only get the S chars since this is Arrival Construct
                        var firstQuality = Character.GetMinCharacterQuality(y.Id)?.Quality ?? 0;
                        return y.Type == 1 && y.Id == x.ModelId && firstQuality == 3;
                    })).DistinctBy(x => x.ModelId).Select(x => new DrawInfo()
                    {
                        Id = x.Id,
                        UseItemId = Inventory.FreeGem,
                        UseItemCount = 250,
                        GroupId = GroupArrivalConstruct,
                        BtnDrawCount = { 1, 10 },
                        Banner = "Assets/Product/Ui/Scene3DPrefab/UiMain3dXiahuo.prefab",
                        EndTime = DateTimeOffset.Now.ToUnixTimeSeconds() * 2
                    }));
                    break;
                case GroupMemberTarget:
                    // Querying every character scene that is omniframe.
                    infos.AddRange(drawSceneTables.Where(x => x.Type == 1 && TableReaderV2.CharacterTableDict.Values.Any(y =>
                    {
                        // only get the A chars since this is member target
                        var firstQuality = Character.GetMinCharacterQuality(y.Id)?.Quality ?? 0;
                        return y.Type == 1 && y.Id == x.ModelId && firstQuality == 2;
                    })).DistinctBy(x => x.ModelId).Select(x => new DrawInfo()
                    {
                        Id = x.Id,
                        UseItemId = Inventory.FreeGem,
                        UseItemCount = 250,
                        GroupId = GroupMemberTarget,
                        BtnDrawCount = { 1, 10 },
                        Banner = "Assets/Product/Ui/Scene3DPrefab/UiMain3dXiahuo.prefab",
                        EndTime = DateTimeOffset.Now.ToUnixTimeSeconds() * 2
                    }));
                    break;
            }

            return infos;
        }

        public static int GetGroupByDrawId(int draw)
        {
            foreach (var groupId in new int[] { GroupMemberTarget, GroupWeaponResearch, GroupTargetWeaponResearch, GroupDormitoryResearch, GroupThemedTargetWeapon, GroupThemedEventConstruct, GroupArrivalConstruct, GroupFateArrivalConstruct, GroupArrivalEventConstruct, GroupFateThemedConstruct, GroupTargetUniframe, GroupAnniversary, GroupFateAnniversaryLimited, GroupCollabTarget, GroupFateCollabTarget, GroupCollabWeaponTarget, GroupCUBTarget, GroupWishingTarget, GroupFateWishingTarget })
            {
                if (GetDrawInfosByGroup(groupId).Any(x => x.Id == draw))
                {
                    return groupId;
                }
            }

            log.Error($"Get group not found for draw {draw}");
            return 0;
        }

        public static List<RewardGoods> DrawDraw(int drawId)
        {
            List<RewardGoods> rewards = new();
            var drawScene = drawSceneTables.Find(x => x.Id == drawId);
            var drawPool = TableReaderV2.Parse<DrawPreviewTable>().Find(x => x.Id == drawId);
            if (drawScene is null || drawPool is null)
            {
                log.Error($"Invalid draw id {drawId}");
                return rewards;
            }
            float random = Random.Shared.NextSingle();

            switch (drawScene.Type)
            {
                case 1:
                    // Character
                    if (random >= 0.972f)
                    {
                        // S Character
                        float rate = Random.Shared.NextSingle();
                    re_rate:
                        if (rate <= 0.3f)
                        {
                            // Rate off
                            List<int> pool = [];

                            foreach (var character in TableReaderV2.CharacterTableDict.Values)
                            {
                                var characterQuality = Character.GetMinCharacterQuality(character.Id);
                                if (characterQuality is null) continue;

                                if (characterQuality.Quality >= 3 && drawPool.GoodsId.Contains(character.Id))
                                {
                                    pool.Add(character.Id);
                                }
                            }
                            if (pool.Count < 1)
                            {
                                foreach (var character in TableReaderV2.CharacterTableDict.Values)
                                {
                                    var characterQuality = Character.GetMinCharacterQuality(character.Id);
                                    if (character is null || characterQuality is null)
                                        continue;

                                    if (characterQuality.Quality >= 3 && !drawPool.UpGoodsId.Contains(character.Id))
                                    {
                                        pool.Add(character.Id);
                                    }
                                }
                            }

                            if (pool.Count > 0)
                            {
                                int rand = pool[Random.Shared.Next(pool.Count)];
                                TableReaderV2.CharacterTableDict.TryGetValue(rand, out var character);
                                var characterQuality = Character.GetMinCharacterQuality(rand);

                                rewards.Add(new()
                                {
                                    RewardType = (int)RewardType.Character,
                                    Quality = characterQuality?.Quality ?? 0,
                                    Count = 1,
                                    TemplateId = rand
                                });
                            }
                        }
                        else
                        {
                            if (drawPool.UpGoodsId.Count == 1)
                            {
                                TableReaderV2.CharacterTableDict.TryGetValue(drawPool.UpGoodsId[0], out var character);
                                var characterQuality = Character.GetMinCharacterQuality(drawPool.UpGoodsId[0]);

                                rewards.Add(new()
                                {
                                    RewardType = (int)RewardType.Character,
                                    Quality = characterQuality?.Quality ?? 0,
                                    Count = 1,
                                    TemplateId = drawPool.UpGoodsId[0]
                                });
                            }
                            else if (drawPool.UpGoodsId.Count > 0)
                            {
                                int rand = drawPool.UpGoodsId[Random.Shared.Next(drawPool.UpGoodsId.Count)];
                                TableReaderV2.CharacterTableDict.TryGetValue(rand, out var character);
                                var characterQuality = Character.GetMinCharacterQuality(rand);

                                rewards.Add(new()
                                {
                                    RewardType = (int)RewardType.Character,
                                    Quality = characterQuality?.Quality ?? 0,
                                    Count = 1,
                                    TemplateId = rand
                                });
                            }
                            else
                            {
                                rate = 0f;
                                goto re_rate;
                            }
                        }
                    }
                    else if (random >= 0.9f)
                    {
                        // A Character
                        float rate = Random.Shared.NextSingle();
                    re_rate:
                        if (rate >= 0.19f)
                        {
                            // Rate on
                            if (drawPool.UpGoodsId.Count == 1)
                            {
                                TableReaderV2.CharacterTableDict.TryGetValue(drawPool.UpGoodsId[0], out var character);
                                var characterQuality = Character.GetMinCharacterQuality(drawPool.UpGoodsId[0]);

                                if (characterQuality?.Quality == 2)
                                    rewards.Add(new()
                                    {
                                        RewardType = (int)RewardType.Character,
                                        Quality = characterQuality?.Quality ?? 0,
                                        Count = 1,
                                        TemplateId = drawPool.UpGoodsId[0]
                                    });
                            }
                            if (rewards.Count < 1)
                            {
                                rate = 0;
                                goto re_rate;
                            }
                        }
                        else
                        {
                            List<int> pool = [];

                            foreach (var character in TableReaderV2.CharacterTableDict.Values)
                            {
                                var characterQuality = Character.GetMinCharacterQuality(character.Id);
                                if (characterQuality is null) continue;

                                if (characterQuality.Quality == 2 && drawPool.GoodsId.Contains(character.Id))
                                {
                                    pool.Add(character.Id);
                                }
                            }
                            if (pool.Count < 1)
                            {
                                foreach (var character in TableReaderV2.CharacterTableDict.Values)
                                {
                                    var characterQuality = Character.GetMinCharacterQuality(character.Id);
                                    if (characterQuality is null) continue;

                                    if (characterQuality.Quality == 2 && !drawPool.UpGoodsId.Contains(character.Id))
                                    {
                                        pool.Add(character.Id);
                                    }
                                }
                            }

                            if (pool.Count > 0)
                            {
                                int rand = pool[Random.Shared.Next(pool.Count)];
                                TableReaderV2.CharacterTableDict.TryGetValue(rand, out var character);
                                var characterQuality = Character.GetMinCharacterQuality(rand);

                                rewards.Add(new()
                                {
                                    RewardType = (int)RewardType.Character,
                                    Quality = characterQuality?.Quality ?? 0,
                                    Count = 1,
                                    TemplateId = rand
                                });
                            }
                        }
                    }
                    else if (random >= 0.82f)
                    {
                        List<int> pool = [];

                        foreach (var character in TableReaderV2.CharacterTableDict.Values)
                        {
                            var characterQuality = Character.GetMinCharacterQuality(character.Id);
                            if (characterQuality is null) continue;

                            if (characterQuality.Quality == 1 && drawPool.GoodsId.Contains(character.Id))
                            {
                                pool.Add(character.Id);
                            }
                        }
                        if (pool.Count < 1)
                        {
                            foreach (var character in TableReaderV2.CharacterTableDict.Values)
                            {
                                var characterQuality = Character.GetMinCharacterQuality(character.Id);
                                if (characterQuality is null) continue;

                                if (characterQuality.Quality == 1 && !drawPool.UpGoodsId.Contains(character.Id))
                                {
                                    pool.Add(character.Id);
                                }
                            }
                        }

                        if (pool.Count > 0)
                        {
                            int rand = pool[Random.Shared.Next(pool.Count)];
                            TableReaderV2.CharacterTableDict.TryGetValue(rand, out var character);
                            var characterQuality = Character.GetMinCharacterQuality(rand);

                            rewards.Add(new()
                            {
                                RewardType = (int)RewardType.Character,
                                Quality = characterQuality?.Quality ?? 0,
                                Count = 1,
                                TemplateId = rand
                            });
                        }
                    }
                    else if (random >= 0.61f)
                    {
                        // Shard
                        var pool = new List<int>();
                        pool.AddRange(drawPool.GoodsId);
                        pool.AddRange(drawPool.UpGoodsId);

                        if (pool.Count > 0)
                        {
                            int rand = pool[Random.Shared.Next(pool.Count)];
                            TableReaderV2.CharacterTableDict.TryGetValue(rand, out var character);
                            var characterQuality = Character.GetMinCharacterQuality(rand);

                            if (character is not null)
                                rewards.Add(new()
                                {
                                    RewardType = (int)RewardType.Item,
                                    Count = characterQuality?.Quality >= 3 ? 3 : (characterQuality?.Quality == 2 ? 8 : 12),
                                    Quality = characterQuality?.Quality ?? 0,
                                    TemplateId = character.ItemId
                                });
                        }
                    }
                    else if (random >= 0.39f)
                    {
                        // 4* Equip
                        var pool = TableReaderV2.EquipTableDict.Values.Where(x => x.Quality == 4).ToList();

                        if (pool.Count > 0)
                        {
                            EquipTable rand = pool[Random.Shared.Next(pool.Count)];

                            rewards.Add(new()
                            {
                                RewardType = (int)RewardType.Equip,
                                Count = 1,
                                Quality = rand.Quality,
                                TemplateId = rand.Id
                            });
                        }
                    }
                    else if (random >= 0.25f)
                    {
                        // Overclock mats
                        var pool = TableReaderV2.ItemTableDict.Values.Where(x => x.Id >= 40100 && x.Id < 40200).ToList();

                        if (pool.Count > 0)
                        {
                            ItemTable rand = pool[Random.Shared.Next(pool.Count)];

                            rewards.Add(new()
                            {
                                RewardType = (int)RewardType.Item,
                                Count = rand.Quality > 3 ? 1 : 3,
                                Quality = rand.Quality,
                                TemplateId = rand.Id
                            });
                        }
                    }
                    else if (random >= 0.18f)
                    {
                        // Exp mats
                        var pool = TableReaderV2.ItemTableDict.Values.Where(x => x.Id >= 30011 && x.Id < 30015).ToList();

                        if (pool.Count > 0)
                        {
                            ItemTable rand = pool[Random.Shared.Next(pool.Count)];

                            rewards.Add(new()
                            {
                                RewardType = (int)RewardType.Item,
                                Count = rand.Quality > 3 ? 1 : 3,
                                Quality = rand.Quality,
                                TemplateId = rand.Id
                            });
                        }
                    }
                    else
                    {
                        // Cog boxes
                        var pool = TableReaderV2.ItemTableDict.Values.Where(x => x.Name.StartsWith("Cog Pack")).ToList();

                        if (pool.Count > 0)
                        {
                            ItemTable rand = pool[Random.Shared.Next(pool.Count)];

                            rewards.Add(new()
                            {
                                RewardType = (int)RewardType.Item,
                                Count = 1,
                                Quality = rand.Quality,
                                TemplateId = rand.Id
                            });
                        }
                    }
                    break;
                case 2:
                    // TODO: Weapon gacha
                    break;
                case 3:
                    // TODO: CUB gacha
                    break;
                default:
                    break;
            }

            return rewards;
        }
    }
}
