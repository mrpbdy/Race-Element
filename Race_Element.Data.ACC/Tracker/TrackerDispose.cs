﻿using RaceElement.Data.ACC.Database;
using RaceElement.Data.ACC.EntryList;
using RaceElement.Data.ACC.Session;
using RaceElement.Data.ACC.Tracker.Laps;
using RaceElement.Data.ACC.Tyres;
using System.Diagnostics;

namespace RaceElement.Data.ACC.Tracker;

public class ACCTrackerDispose
{
    public static void Dispose()
    {
        Debug.WriteLine("Safely disposing ACC Data Tracker instances");
        BroadcastTracker.Instance.Dispose();
        EntryListTracker.Instance.Stop();
        GapTracker.Instance.CancelJoin();

        SetupHiderTracker.Instance.Dispose();

        PageGraphicsTracker.Stop();

        RaceSessionTracker.Instance.Stop();
        LapTracker.Instance.Stop();
        TyresTracker.Instance.Stop();

        RaceWeekendDatabase.Close();

    }
}
