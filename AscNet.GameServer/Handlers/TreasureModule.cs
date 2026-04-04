using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using AscNet.Common;
using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.Table.V2.share.exhibition;
using AscNet.Table.V2.share.fuben.mainline;
using AscNet.Table.V2.share.reward;
using MessagePack;

namespace AscNet.GameServer.Handlers
{
    #region MsgPackScheme
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [MessagePackObject(true)]
    public class TreasureRewardRequest
    {
        public int TreasureId;
    }

    [MessagePackObject(true)]
    public class TreasureRewardResponse
    {
        public int Code;
        public List<RewardGoods> RewardGoodsList { get; set; } = new();
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    #endregion

    internal class TreasureModule
    {
        [RequestPacketHandler("ReceiveTreasureRewardRequest")]
        public static void HandleReceiveTreasureRewardRequestHandler(Session session, Packet.Request packet)
        {
            var request = MessagePackSerializer.Deserialize<TreasureRewardRequest>(packet.Content);
            var treasure = TableReaderV2.Parse<TreasureTable>().Find(x => x.TreasureId == request.TreasureId);
            var rewardId = treasure?.RewardId.ToString();
            if (rewardId == null)
            {
                session.SendResponse(new TreasureRewardResponse() { Code = 1 }, packet.Id);
                return;
            }

            var rewardGoods = TableReaderV2.Parse<RewardGoodsTable>().Where(x =>
            {
                var id = x.Id.ToString();
                return id.StartsWith(rewardId) && id.Length > rewardId.Length;
            });

            var success = session.player.AddTreasure(request.TreasureId);
            if (!success)
            {
                session.SendResponse(new TreasureRewardResponse() { Code = 1 }, packet.Id);
                return;
            }

            TreasureRewardResponse response = new()
            {
                RewardGoodsList = RewardHandler.GiveRewards(rewardGoods, session)
            };

            session.SendResponse(response, packet.Id);
        }
    }
}
