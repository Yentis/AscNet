using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.Table.V2.share.task;
using MessagePack;

namespace AscNet.GameServer.Handlers
{
    public enum TaskState
    {
        InActive = -1,
        Standby = 0,
        Active = 1,
        Accepted = 2,
        Achieved = 3,
        Finish = 4,
        Invalid = 5,
    }

    public enum TaskType
    {
        Story = 1,
        Daily = 2,
        Weekly = 3,
        Achievement = 4,
        Activity = 5,
        OffLine = 6,
        NewPlayer = 7,
        CharacterStory = 8,
        Bfrt = 9,
        ArenaChallenge = 10,
        TimeLimit = 11,
        DormNormal = 12,
        DormDaily = 13,
        BossSingle = 14,
        BabelTower = 15,
        RogueLike = 16,
        Regression = 17,
        GuildMainly = 18,
        GuildDaily = 19,
        ArenaOnlineWeekly = 20,
        SpecialTrain = 21,
        InfestorWeekly = 22,
        GuildWeekly = 23,
        BossOnLine = 24,
        WorldBoss = 25,
        Expedition = 26,
        RpgTower = 27,
        MentorShipGrow = 28,
        MentorShipGraduate = 29,
        MentorShipWeekly = 30,
        NieR = 31,
        Pokemon = 32,
        ZhouMu = 39,
        ChristmasTree = 40,
        ChessPursuit = 41,
        Couplet = 42,
        SimulatedCombat = 43,
        WhiteValentine = 44,
        MoeWarDaily = 45,
        MoeWarNormal = 46,
        FingerGuessing = 47,
        PokerGuessing = 48,
        Reform = 49,
        PokerGuessingCollection = 50,
        Passport = 51,
        CoupleCombat = 52,
        LivWarmSoundsActivity = 53,
        LivWarmExtActivity = 54,
        Maverick = 56,
        Doomsday = 57,
        PivotCombat = 62,
        BodyCombineGame = 63,
        GuildWar = 64,
        GuildWarTerm2 = 65,
        WeekChallenge = 66,
        DoubleTower = 67,
        GoldenMiner = 68,
        TaikoMaster = 69,
        SuperSmash = 70,
        Newbie = 71,
        CharacterTower = 73,
        DlcHunt = 75,
        BackFlow = 76,
        YuanXiao = 77,
    }

    #region MsgPackScheme
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [MessagePackObject(true)]
    public class GetCourseRewardRequest
    {
        public int StageId;
    }

    [MessagePackObject(true)]
    public class GetCourseRewardResponse
    {
        public int Code;
        public List<RewardGoods> RewardGoodsList { get; set; } = [];
    }

    [MessagePackObject(true)]
    public class FinishTaskRequest
    {
        public int TaskId;
    }

    [MessagePackObject(true)]
    public class FinishTaskResponse
    {
        public int Code;
        public List<RewardGoods> RewardGoodsList { get; set; } = [];
    }

    [MessagePackObject(true)]
    public class FinishMultiTaskRequest
    {
        public List<int> TaskIds;
    }

    [MessagePackObject(true)]
    public class FinishMultiTaskResponse
    {
        public int Code;
        public List<RewardGoods> RewardGoodsList { get; set; } = [];
    }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    #endregion

    internal class TaskModule
    {
        private static readonly Dictionary<ConditionCategory, HashSet<int>> TaskCategories;

        static TaskModule()
        {
            TaskCategories = [];

            foreach (var task in TableReaderV2.TaskTableDict.Values)
            {
                RegisterTaskConditions(task.Id, task.ActivateCondition);
                RegisterTaskConditions(task.Id, task.Condition);
            }
        }

        private static void RegisterTaskConditions(int taskId, List<int> conditions)
        {
            foreach (var conditionId in conditions)
            {
                TableReaderV2.ConditionTableDict.TryGetValue(conditionId, out var condition);
                if (condition == null) return;

                var category = ConditionModule.GetCategory((ConditionType)condition.Type);
                if (category == null) return;

                if (!TaskCategories.TryAdd((ConditionCategory)category, [taskId]))
                {
                    TaskCategories[(ConditionCategory)category].Add(taskId);
                }
            }
        }

        public static void Init(Session session, bool isNewDay)
        {
            UpdateTasks(session, isNewDay, category: null);

            List<TaskItem> tasks = [.. session.player.Tasks.Values];
            NotifyTaskData notifyTaskData = new()
            {
                TaskData = new()
                {
                    Tasks = tasks,
                    FinishedTasks = [.. tasks.Where(x => x.State == (int)TaskState.Finish).Select(x => (int)x.Id)],
                    TaskLimitIdActiveInfos = [],
                    NewPlayerRewardRecord = [],
                    NewbieUnlockPeriod = 7,
                    NewbieRecvProgress = [],
                    NewbieHonorReward = false,
                    Course = session.stage.Course,
                }
            };

            session.SendPush(notifyTaskData);
        }

        public static void OnReady(Session session)
        {
            session.Events += (sender, arg) =>
            {
                if (arg is NotifyStageData)
                {
                    NotifyTasks(session, ConditionCategory.Stage);
                }
                else if (arg is NotifyEquipDataList)
                {
                    NotifyTasks(session, ConditionCategory.Equip);
                }
            };
        }

        private static void NotifyTasks(Session session, ConditionCategory category)
        {
            var tasks = UpdateTasks(session, isNewDay: false, category);
            if (tasks.Count <= 0) return;

            NotifyTask notifyTask = new()
            {
                Tasks = new()
                {
                    Tasks = tasks
                }
            };

            session.SendPush(notifyTask);
        }

        public static List<TaskItem> UpdateTasks(Session session, bool isNewDay, ConditionCategory? category)
        {
            var tasks = category == null
                ? TableReaderV2.TaskTableDict.Values
                : TaskCategories[(ConditionCategory)category].Select(x => TableReaderV2.TaskTableDict[x]);

            var parsedTasks = tasks
                .Select(x => GetTask(x, session, isNewDay, category))
                .OfType<TaskItem>()
                .ToList();

            foreach (var newTask in parsedTasks)
            {
                session.player.AddTask(newTask);
            }

            return parsedTasks;
        }

        private static TaskItem? GetTask(TaskTable task, Session session, bool isNewDay, ConditionCategory? category)
        {
            session.player.Tasks.TryGetValue((uint)task.Id, out var existingTask);
            var existingTaskState = (TaskState)(existingTask?.State ?? 0);

            if (existingTaskState >= TaskState.Finish)
            {
                if (category == null) return existingTask;
                else return null;
            }

            if (existingTaskState == TaskState.Achieved) return existingTask;
            if (existingTaskState != TaskState.Active)
            {
                var activateResult = ConditionModule.CheckConditions(task, existingTask, task.ActivateCondition, session, isNewDay, category);
                if (!activateResult.Passed) return null;
            }

            var result = ConditionModule.CheckConditions(task, existingTask, task.Condition, session, isNewDay, category);
            if (result.Schedule.All(x => x.Value == 0)) return null;

            return new TaskItem()
            {
                Id = (uint)task.Id,
                Schedule = result.Schedule,
                State = result.Passed
                    ? (int)TaskState.Achieved
                    : (int)TaskState.Active,
            };
        }

        [RequestPacketHandler("DoClientTaskEventRequest")]
        public static void DoClientTaskEventRequestHandler(Session session, Packet.Request packet)
        {
            session.SendResponse(new DoClientTaskEventResponse(), packet.Id);
        }

        [RequestPacketHandler("FinishTaskRequest")]
        public static void FinishTaskRequestHandler(Session session, Packet.Request packet)
        {
            var request = MessagePackSerializer.Deserialize<FinishTaskRequest>(packet.Content);
            TableReaderV2.TaskTableDict.TryGetValue(request.TaskId, out var task);

            var rewards = FinishTasks([task], session);
            if (rewards.Count <= 0)
            {
                session.SendResponse(new FinishTaskResponse() { Code = 1 }, packet.Id);
                return;
            }

            FinishTaskResponse response = new()
            {
                RewardGoodsList = rewards
            };

            session.SendResponse(response, packet.Id);
        }

        [RequestPacketHandler("FinishMultiTaskRequest")]
        public static void FinishMultiTaskRequestHandler(Session session, Packet.Request packet)
        {
            var request = MessagePackSerializer.Deserialize<FinishMultiTaskRequest>(packet.Content);
            var tasks = request.TaskIds.Select(x =>
            {
                TableReaderV2.TaskTableDict.TryGetValue(x, out var task);
                return task;
            });

            var rewards = FinishTasks(tasks, session);
            if (rewards.Count <= 0)
            {
                session.SendResponse(new FinishMultiTaskResponse() { Code = 1 }, packet.Id);
                return;
            }

            FinishMultiTaskResponse response = new()
            {
                RewardGoodsList = rewards
            };

            session.SendResponse(response, packet.Id);
        }

        private static List<RewardGoods> FinishTasks(IEnumerable<TaskTable?> tasks, Session session)
        {
            List<RewardGoods> rewardGoods = [];
            List<TaskItem> taskItems = [];

            foreach (var task in tasks)
            {
                var rewardId = task?.RewardId;
                if (task == null || rewardId == null) continue;

                var newTask = GetTask(task, session, isNewDay: false, category: null);
                if (newTask == null || newTask.State != (int)TaskState.Achieved) continue;

                newTask.State = (int)TaskState.Finish;
                session.player.AddTask(newTask);

                var rewards = RewardHandler.GetRewards((int)rewardId, session);
                RewardHandler.GiveRewards(rewards, session);

                rewardGoods.AddRange(rewards);
                taskItems.Add(newTask);
            }

            NotifyTask notifyTask = new()
            {
                Tasks = new()
                {
                    Tasks = taskItems
                }
            };

            session.SendPush(notifyTask);
            return rewardGoods;
        }

        [RequestPacketHandler("GetCourseRewardRequest")]
        public static void GetCourseRewardRequestHandler(Session session, Packet.Request packet)
        {
            var request = MessagePackSerializer.Deserialize<GetCourseRewardRequest>(packet.Content);
            var course = TableReaderV2.Parse<CourseTable>().Find(x => x.StageId == request.StageId);
            var rewardId = course?.RewardId;
            if (rewardId == null)
            {
                session.SendResponse(new GetCourseRewardResponse() { Code = 1 }, packet.Id);
                return;
            }

            var success = session.stage.AddCourse((uint)request.StageId);
            var rewards = RewardHandler.GetRewards((int)rewardId, session);
            if (success) RewardHandler.GiveRewards(rewards, session);

            GetCourseRewardResponse response = new()
            {
                RewardGoodsList = [.. rewards]
            };

            session.SendResponse(response, packet.Id);
        }
    }
}
