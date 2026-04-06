using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.Table.V2.share.exhibition;
using MessagePack;

namespace AscNet.GameServer.Handlers
{
    #region MsgPackScheme
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [MessagePackObject(true)]
    public class GatherRewardRequest
    {
        public int Id;
    }

    [MessagePackObject(true)]
    public class GatherRewardResponse
    {
        public int Code;
        public List<RewardGoods> RewardGoods { get; set; } = new();
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    #endregion

    internal class ExhibitionModule
    {
        [RequestPacketHandler("GatherRewardRequest")]
        public static void HandleGatherRewardRequestHandler(Session session, Packet.Request packet)
        {
            GatherRewardRequest req = MessagePackSerializer.Deserialize<GatherRewardRequest>(packet.Content);
            ExhibitionRewardTable? exhibitionReward = TableReaderV2.Parse<ExhibitionRewardTable>().Find(x => x.Id == req.Id);
            var rewardId = exhibitionReward?.RewardId;
            if (rewardId == null)
            {
                session.SendResponse(new GatherRewardResponse() { Code = 1 }, packet.Id);
                return;
            }

            var rewards = RewardHandler.GetRewards((int)rewardId, session);
            var success = session.player.AddGatherReward(req.Id);
            if (success) RewardHandler.GiveRewards(rewards, session);

            GatherRewardResponse rsp = new()
            {
                RewardGoods = [.. rewards]
            };

            session.SendPush(new NotifyGatherReward() { Id = req.Id });
            session.SendResponse(rsp, packet.Id);
        }
    }
}
