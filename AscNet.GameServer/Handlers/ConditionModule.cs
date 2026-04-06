using AscNet.Common.MsgPack;
using AscNet.Common.Util;
using AscNet.Table.V2.share.fuben.practice;
using AscNet.Table.V2.share.task;

namespace AscNet.GameServer.Handlers
{
    public enum ConditionType
    {
        CommandantLevel = 10101,
        LoginDays = 10202,
        EquipGear = 12102,
        StagePassedCount = 15201,
        StagePassed = 15220,
        PracticePassed = 15221,
        TaskFinished = 17203,
    }

    public enum ConditionCategory
    {
        Stage,
        Equip,
    }

    public class ConditionsRet
    {
        public bool Passed { get; set; }
        public List<TaskSchedule> Schedule { get; set; } = [];
    }

    internal class ConditionModule
    {
        private class ConditionRet
        {
            public bool Passed { get; set; }
            public long Value { get; set; }
        }

        public static ConditionCategory? GetCategory(ConditionType conditionType)
        {
            return conditionType switch
            {
                ConditionType.EquipGear => ConditionCategory.Equip,
                ConditionType.StagePassedCount => ConditionCategory.Stage,
                ConditionType.StagePassed => ConditionCategory.Stage,
                _ => null,
            };
        }

        public static ConditionsRet CheckConditions(
            TaskTable task,
            TaskItem? existingTask,
            List<int> conditionIds,
            Session session,
            bool isNewDay,
            ConditionCategory? category
        )
        {
            var passed = true;
            var schedules = existingTask?.Schedule.ToDictionary(x => x.Id) ?? [];
            List<TaskSchedule> resultSchedules = [];

            var conditions = conditionIds.Select(x =>
            {
                TableReaderV2.ConditionTableDict.TryGetValue(x, out var condition);
                return condition;
            }).OfType<ConditionTable>();

            foreach (var condition in conditions)
            {
                var type = (ConditionType)condition.Type;
                if (category != null && GetCategory(type) != category)
                {
                    schedules.TryGetValue((uint)condition.Id, out var schedule);
                    if (schedule == null) return new() { Passed = false };

                    resultSchedules.Add(schedule);
                    continue;
                }

                var result = type switch
                {
                    ConditionType.CommandantLevel => CheckCommandantLevel(condition.Params, session),
                    ConditionType.LoginDays => CheckLoginDays(
                        taskId: task.Id,
                        conditionId: condition.Id,
                        paramList: condition.Params,
                        session: session,
                        isNewDay: isNewDay
                    ),
                    ConditionType.EquipGear => CheckEquipGear(condition.Params, session),
                    ConditionType.StagePassedCount => CheckStagePassedCount((TaskType)task.Type, condition.Params, session),
                    ConditionType.StagePassed => CheckStagePassed(condition.Params, session),
                    ConditionType.PracticePassed => CheckPracticePassed(condition.Params, session),
                    ConditionType.TaskFinished => CheckTaskFinished(condition.Params, session),
                    _ => null,
                };

                if (result == null)
                {
#if DEBUG
                    session.log.Warn($"Unknown condition type {condition.Type}");
#endif
                    return new() { Passed = false };
                }

                if (!result.Passed)
                {
                    passed = false;
                }

                resultSchedules.Add(new()
                {
                    Id = (uint)condition.Id,
                    Value = (int)result.Value
                });
            }

            return new()
            {
                Passed = passed,
                Schedule = resultSchedules
            };
        }

        private static ConditionRet CheckStagePassedCount(TaskType taskType, List<int> paramList, Session session)
        {
            var minPassCount = paramList.FirstOrDefault();
            long passCount = 0;

            if (paramList.Count == 1)
            {
                passCount = session.stage.Stages.Values.Sum(x => taskType == TaskType.Daily ? x.PassTimesToday : x.PassTimesTotal);
            }
            else
            {
                passCount = paramList.Skip(1).Sum(x =>
                {
                    session.stage.Stages.TryGetValue(x, out var stage);
                    return (taskType == TaskType.Daily ? stage?.PassTimesToday : stage?.PassTimesTotal) ?? 0;
                });
            }

            return new()
            {
                Passed = passCount >= minPassCount,
                Value = Math.Min(passCount, minPassCount),
            };
        }

        private static ConditionRet CheckStagePassed(IEnumerable<int> paramList, Session session)
        {
            var passedList = paramList.Select(x =>
            {
                session.stage.Stages.TryGetValue(x, out var stage);
                return stage?.Passed == true;
            });

            return new()
            {
                Passed = passedList.All(x => x),
                Value = passedList.Count(x => x),
            };
        }

        private static ConditionRet CheckCommandantLevel(List<int> paramList, Session session)
        {
            var level = session.player.PlayerData.Level;
            var minLevel = paramList.FirstOrDefault();

            return new()
            {
                Passed = level >= minLevel,
                Value = Math.Min(level, minLevel),
            };
        }

        private static ConditionRet CheckPracticePassed(List<int> paramList, Session session)
        {
            var practiceInfos = session.stage.GetPracticeChapterInfos();
            var passedCount = TableReaderV2.Parse<PracticeGroupTable>().Where(x =>
            {
                practiceInfos.TryGetValue(x.GroupId, out var value);
                if (value == null) return false;

                return x.StageIds.All(y => value.FinishStages.Contains((uint)y));
            })?.Count() ?? 0;

            var minPassedCount = paramList.FirstOrDefault();

            return new()
            {
                Passed = passedCount >= minPassedCount,
                Value = Math.Min(passedCount, minPassedCount),
            };
        }

        private static ConditionRet CheckLoginDays(int taskId, int conditionId, List<int> paramList, Session session, bool isNewDay)
        {
            session.player.Tasks.TryGetValue((uint)taskId, out var task);
            var loginDays = task?.Schedule.FirstOrDefault(x => x.Id == conditionId)?.Value ?? 0;
            var minLoginDays = paramList.FirstOrDefault();

            if (isNewDay) loginDays += 1;

            return new()
            {
                Passed = loginDays >= minLoginDays,
                Value = Math.Min(loginDays, minLoginDays),
            };
        }

        private static ConditionRet CheckTaskFinished(List<int> paramList, Session session)
        {
            var minFinishCount = paramList.FirstOrDefault();
            var taskIds = paramList.Skip(1);

            var finishCount = taskIds.Where(x =>
            {
                session.player.Tasks.TryGetValue((uint)x, out var task);
                return task?.State == (int)TaskState.Finish;
            }).Count();

            return new()
            {
                Passed = finishCount >= minFinishCount,
                Value = Math.Min(finishCount, minFinishCount)
            };
        }

        private static ConditionRet CheckEquipGear(List<int> paramList, Session session)
        {
            var minEquipCount = paramList.FirstOrDefault();
            var unknown = paramList.ElementAtOrDefault(1);
            var unknown2 = paramList.ElementAtOrDefault(2);
            var unknown3 = paramList.ElementAtOrDefault(3);
            var unknown4 = paramList.ElementAtOrDefault(4);
            var stars = paramList.ElementAtOrDefault(5);

            var equipCount = session.character.Equips.Where(x =>
            {
                if (x.CharacterId <= 0) return false;

                TableReaderV2.EquipTableDict.TryGetValue((int)x.TemplateId, out var equip);
                if (equip == null || equip.Star < stars) return false;

                return true;
            }).Count();

            return new()
            {
                Passed = equipCount >= minEquipCount,
                Value = Math.Min(equipCount, minEquipCount)
            };
        }
    }
}
