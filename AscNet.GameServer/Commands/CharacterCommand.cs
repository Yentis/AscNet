using AscNet.Common.Util;
using AscNet.GameServer.Handlers;

namespace AscNet.GameServer.Commands
{
    [CommandName("character")]
    internal class CharacterCommand : Command
    {
        public CharacterCommand(Session session, string[] args, bool validate = true) : base(session, args, validate) { }

        public override string Help => "Command to modify characters.";

        [Argument(0, @"^add$", "The operation selected (add)")]
        string Op { get; set; } = string.Empty;

        [Argument(1, @"^[0-9]+$|^all$", "The target character, value is character id or 'all'")]
        string Target { get; set; } = string.Empty;

        public override void Execute()
        {
            int id = Miscs.ParseIntOr(Target);

            switch (Op)
            {
                case "add":
                    if (Target == "all")
                    {
                        var rewards = TableReaderV2.CharacterTableDict.Values
                            .Where(x => !session.character.Characters.Any(y => y.Id == x.Id))
                            .Select(x => new Reward { Id = x.Id, Type = RewardType.Character });

                        RewardHandler.GiveRewards(rewards, session);
                    }
                    else
                    {
                        RewardHandler.GiveRewards([new Reward() { Id = id, Type = RewardType.Character }], session);
                    }
                    break;
                default:
                    throw new InvalidOperationException("Invalid operation!");
            }
        }
    }
}
