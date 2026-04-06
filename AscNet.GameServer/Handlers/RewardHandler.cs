using AscNet.Common;
using AscNet.Common.Database;
using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.GameServer.Handlers.Drops;
using AscNet.Table.V2.share.exhibition;
using AscNet.Table.V2.share.reward;

namespace AscNet.GameServer.Handlers
{
    public class Reward
    {
        public int Id;
        public int Count = 1;
        public int Level = 1;
        public RewardType Type;
    }

    internal class RewardHandler
    {
        public static RewardType? GetRewardType(RewardGoodsTable reward)
        {
            var idVal = (int)MathF.Floor((reward.TemplateId > 0 ? reward.TemplateId : reward.Id) / 1000000);

            return idVal switch
            {
                3 => RewardType.Equip,
                // Unknown, not used in RewardGoodsTable
                4 or 5 => null,
                6 => RewardType.Fashion,
                7 => RewardType.BaseEquip,
                _ => (RewardType)(idVal + 1),
            };
        }

        public static IEnumerable<RewardGoods> GetRewards(int rewardId, Session session)
        {
            TableReaderV2.RewardTableDict.TryGetValue(rewardId, out var reward);
            if (reward == null) return [];

            var rewardGoods = reward.SubIds.Select(x =>
            {
                TableReaderV2.RewardGoodsTableDict.TryGetValue(x, out var rewardGood);
                return rewardGood;
            }).OfType<RewardGoodsTable>();

            return GetRewards(rewardGoods, session);
        }

        public static IEnumerable<RewardGoods> GetRewards(IEnumerable<RewardGoodsTable> rewardGoods, Session session)
        {
            return rewardGoods
                .Select(x =>
                {
                    var rewardType = GetRewardType(x);
                    if (rewardType == null)
                    {
                        session.log.Error($"Could not get reward type for template id {x.TemplateId} or id {x.Id}");
                        return null;
                    }

                    return new RewardGoods()
                    {
                        Id = x.Id,
                        TemplateId = x.TemplateId,
                        Count = x.Count,
                        RewardType = (int)rewardType,
                    };
                }).OfType<RewardGoods>();
        }

        public static void GiveRewards(IEnumerable<RewardGoods> rewardGoods, Session session)
        {
            var rewards = rewardGoods.Select(x =>
            {
                return new Reward()
                {
                    Id = x.TemplateId,
                    Count = x.Count,
                    Type = (RewardType)x.RewardType,
                };
            });

            GiveRewards(rewards, session);
        }

        public static void GiveRewards(IEnumerable<Reward> rewards, Session session)
        {
            List<Reward> transformedRewards = [];

            NotifyEquipDataList notifyEquipData = new();
            FashionSyncNotify fashionSync = new();
            NotifyCharacterDataList notifyCharacterData = new();
            NotifyItemDataList notifyItemData = new();

            foreach (var reward in rewards)
            {
                transformedRewards.AddRange(
                    HandleReward(
                        reward,
                        session,
                        notifyItemData.ItemDataList,
                        notifyCharacterData.CharacterDataList,
                        fashionSync.FashionList,
                        notifyEquipData.EquipDataList
                    )
                );
            }

            foreach (var transformedReward in transformedRewards)
            {
                HandleReward(
                    transformedReward,
                    session,
                    notifyItemData.ItemDataList,
                    notifyCharacterData.CharacterDataList,
                    fashionSync.FashionList,
                    notifyEquipData.EquipDataList
                );
            }

            if (notifyItemData.ItemDataList.Count > 0)
            {
                session.SendPush(notifyItemData);
            }

            if (notifyEquipData.EquipDataList.Count > 0)
            {
                session.SendPush(notifyEquipData);
            }

            if (fashionSync.FashionList.Count > 0)
            {
                session.SendPush(fashionSync);
            }

            if (notifyCharacterData.CharacterDataList.Count > 0)
            {
                session.SendPush(notifyCharacterData);
            }
        }

        private static IEnumerable<Reward> HandleReward(
            Reward reward,
            Session session,
            List<Item> itemDataList,
            List<CharacterData> characterDataList,
            List<FashionList> fashionList,
            List<EquipData> equipDataList
        )
        {
            switch (reward.Type)
            {
                case RewardType.Item:
                    TableReaderV2.ItemTableDict.TryGetValue(reward.Id, out var itemData);
                    if (itemData is not null)
                    {
                        // Custom handler for some items that aren't meant to be in the inventory.
                        DropHandlerDelegate? dropHandler = DropsHandlerFactory.GetDropHandler(itemData.Id);
                        if (itemData.IsHidden() && dropHandler is not null)
                        {
                            return dropHandler.Invoke(session, reward.Count).Select(x => new Reward()
                            {
                                Id = x.TemplateId,
                                Count = x.Count,
                                Type = x.Type,
                                Level = x.Level,
                            });
                        }
                    }

                    itemDataList.Add(session.inventory.Do(reward.Id, reward.Count));
                    break;
                case RewardType.Character:
                    if (session.character.Characters.Any(x => x.Id == reward.Id))
                    {
                        TableReaderV2.CharacterTableDict.TryGetValue(reward.Id, out var characterData);
                        if (characterData == null) return [];

                        var decomposeCount = Character.GetMinCharacterFragment(reward.Id)?.DecomposeCount ?? 18;
                        return [new()
                        {
                            Id = characterData.ItemId,
                            Count = decomposeCount,
                            Type = RewardType.Item,
                        }];
                    }


                    var characterRet = session.character.AddCharacter((uint)reward.Id, level: reward.Level);
                    var exhibitionRewardId = TableReaderV2.Parse<ExhibitionRewardTable>().Find(x => x.CharacterId == reward.Id && x.LevelId == 1)?.Id;
                    if (exhibitionRewardId is int id)
                    {
                        if (session.player.AddGatherReward(id))
                        {
                            session.SendPush(new NotifyGatherReward() { Id = id });
                        }
                    }

                    characterDataList.Add(characterRet.Character);
                    fashionList.Add(characterRet.Fashion);
                    equipDataList.Add(characterRet.Equip);

                    break;
                case RewardType.Equip:
                    equipDataList.Add(session.character.AddEquip((uint)reward.Id, level: reward.Level));
                    break;
                case RewardType.Fashion:
                case RewardType.BaseEquip:
                case RewardType.Furniture:
                case RewardType.HeadPortrait:
                case RewardType.DormCharacter:
                case RewardType.ChatEmoji:
                case RewardType.WeaponFashion:
                case RewardType.Collection:
                case RewardType.Background:
                case RewardType.Pokemon:
                case RewardType.Partner:
                case RewardType.Nameplate:
                case RewardType.RankScore:
                case RewardType.Medal:
                case RewardType.DrawTicket:
                    session.log.Warn($"Unimplemented reward requested: {reward.Type} - id: {reward.Id}");
                    break;
            }

            return [];
        }
    }
}
