using System;
using System.Collections.Generic;
using System.Text;

namespace LoopQuest.Application.Activities.Commands.SyncActivities;

public sealed record SyncResultDto(int Fetched, int Qualifying, int Added, int Updated);
