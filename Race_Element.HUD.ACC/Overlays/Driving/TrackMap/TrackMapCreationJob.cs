﻿using System;
using System.IO;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;

using RaceElement.Core.Jobs.LoopJob;
using RaceElement.Data.ACC.Core;
using RaceElement.Util;

namespace RaceElement.HUD.ACC.Overlays.Driving.TrackMap;

public class TrackPoint
{
    private float _x, _y, _spline;
    private float _deltaTime, _kmh;
    private float _accX, _accY, _accZ;

    public TrackPoint()
    {
        _x = 0;
        _y = 0;
        _spline = 0;

        _kmh = 0;
        _deltaTime = 0;

        _accX = 0;
        _accY = 0;
        _accZ = 0;
    }

    public TrackPoint(TrackPoint other)
    {
        _x = other._x;
        _y = other._y;
        _spline = other._spline;

        _kmh = other._kmh;
        _deltaTime = other._deltaTime;

        _accX = other._accX;
        _accY = other._accY;
        _accZ = other._accZ;
    }

    public float X
    {
        get => _x;
        set => _x = value;
    }

    public float Y
    {
        get => _y;
        set => _y = value;
    }

    public float Spline
    {
        get => _spline;
        set => _spline = value;
    }

    public float DeltaTime
    {
        get => _deltaTime;
        set => _deltaTime = value;
    }

    public float Kmh
    {
        get => _kmh;
        set => _kmh = value;
    }

    public float AccX
    {
        get => _accX;
        set => _accX = value;
    }

    public float AccY
    {
        get => _accY;
        set => _accY = value;
    }

    public float AccZ
    {
        get => _accZ;
        set => _accZ = value;
    }

    public PointF ToPointF()
    {
        return new PointF(_x, _y);
    }
}

public class TrackMapCreationJob : AbstractLoopJob
{
    private enum CreationState
    {
        Start,
        TraceTrack,
        LoadFromFile,
        NotifySubscriber,
        End
    }

    public EventHandler<List<TrackPoint>> OnMapPositionsCallback;
    public EventHandler<string> OnMapProgressCallback;

    private readonly List<TrackPoint> _trackedPositions;
    private CreationState _mapTrackingState;

    private int _completedLaps;
    private int _prevPacketId;

    private float _trackingPercentage;
    private DateTime _dateTime;

    public TrackMapCreationJob()
    {
        _mapTrackingState = CreationState.Start;
        _trackedPositions = new();

        _completedLaps = ACCSharedMemory.Instance.PageFileGraphic.CompletedLaps;
        _prevPacketId = ACCSharedMemory.Instance.PageFileGraphic.PacketId;

        _trackingPercentage = 0;
    }

    public override void RunAction()
    {
        switch (_mapTrackingState)
        {
            case CreationState.Start:
            {
                _mapTrackingState = InitialState();
            } break;

            case CreationState.LoadFromFile:
            {
                try { _mapTrackingState = LoadMapFromFile(); }
                catch (Exception e) { Debug.WriteLine(e);  }
            } break;

            case CreationState.TraceTrack:
            {
                _mapTrackingState = PositionTracking();
            } break;

            case CreationState.NotifySubscriber:
            {
                {
                    const string msg = "Map tracked. Enjoy it!";
                    OnMapProgressCallback?.Invoke(null, msg);
                }

                OnMapPositionsCallback?.Invoke(this, _trackedPositions);
                _mapTrackingState = CreationState.End;
            } break;

            default:
            {
                OnMapProgressCallback?.Invoke(null, null);
                Thread.Sleep(10);
            } break;
        }
    }

    private CreationState InitialState()
    {
        var laps = ACCSharedMemory.Instance.PageFileGraphic.CompletedLaps;
        var trackName = ACCSharedMemory.Instance.PageFileStatic.Track;
        string path = FileUtil.RaceElementTracks + trackName + ".bin";

        if (trackName.Length > 0 && File.Exists(path))
        {
            return CreationState.LoadFromFile;
        }

        {
            const string msg = "Tracking state -> waiting for lap counter to change.";
            OnMapProgressCallback?.Invoke(null, msg);
        }

        if (!AccProcess.IsRunning)
        {
            return CreationState.Start;
        }

        if (laps != _completedLaps)
        {
            _completedLaps = laps;
            _dateTime = DateTime.Now;
            return CreationState.TraceTrack;
        }

        return CreationState.Start;
    }

    private CreationState PositionTracking()
    {
        {
            const string msg = "Tracking state -> tracking map ({0:0.0}%),\nif you invalidate the lap you will have to start again.";
            OnMapProgressCallback?.Invoke(null, String.Format(msg, _trackingPercentage * 100));
        }

        var pageGraphics = ACCSharedMemory.Instance.PageFileGraphic;
        var pagePhysics = ACCSharedMemory.Instance.PageFilePhysics;

        if (pageGraphics.PacketId == _prevPacketId)
        {
            return CreationState.TraceTrack;
        }

        if (Math.Abs(pageGraphics.NormalizedCarPosition - _trackingPercentage) <= float.Epsilon)
        {
            return CreationState.TraceTrack;
        }

        _trackingPercentage = pageGraphics.NormalizedCarPosition;
        _prevPacketId = pageGraphics.PacketId;

        if (!pageGraphics.IsValidLap)
        {
            _trackingPercentage = 0;
            _trackedPositions.Clear();
            return CreationState.Start;
        }

        for (var i = 0; i < pageGraphics.ActiveCars; ++i)
        {
            if (pageGraphics.CarIds[i] != pageGraphics.PlayerCarID)
            {
               continue;
            }

            TrackPoint pos = new()
            {
                X = pageGraphics.CarCoordinates[i].X,
                Y = pageGraphics.CarCoordinates[i].Z,

                Spline = pageGraphics.NormalizedCarPosition,
                Kmh = pagePhysics.SpeedKmh,

                AccX = pagePhysics.AccG[0],
                AccY = pagePhysics.AccG[1],
                AccZ = pagePhysics.AccG[2],

                DeltaTime = (float)(DateTime.Now - _dateTime).TotalMilliseconds
            };

            _trackedPositions.Add(pos);
            break;
        }

        if (pageGraphics.CompletedLaps != _completedLaps)
        {
            WriteMapToFile();
            return CreationState.NotifySubscriber;
        }

        return CreationState.TraceTrack;
    }

    private CreationState LoadMapFromFile()
    {
        {
            const string msg = "Tracking state -> Map found on disk, loading it.";
            OnMapProgressCallback?.Invoke(null, msg);
        }

        var trackName = ACCSharedMemory.Instance.PageFileStatic.Track.ToLower();
        string path = FileUtil.RaceElementTracks + trackName + ".bin";

        using FileStream fileStream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader binaryReader = new(fileStream);

        while (fileStream.Position < fileStream.Length)
        {
            {
                const string msg = "Tracking state -> loading from disk ({0:0.0}%)";
                OnMapProgressCallback?.Invoke(null, String.Format(msg, ((float)fileStream.Position / fileStream.Length) * 100.0f));
            }

            TrackPoint pos = new()
            {
                X = binaryReader.ReadSingle(),
                Y = binaryReader.ReadSingle(),
                Spline = binaryReader.ReadSingle(),

                DeltaTime = binaryReader.ReadSingle(),
                Kmh = binaryReader.ReadSingle(),

                AccX = binaryReader.ReadSingle(),
                AccY = binaryReader.ReadSingle(),
                AccZ = binaryReader.ReadSingle()
            };

            _trackedPositions.Add(pos);
        }

        binaryReader.Close();
        fileStream.Close();

        return CreationState.NotifySubscriber;
    }

    private void WriteMapToFile()
    {
        {
            const string msg = "Tracking state -> write map to file.";
            OnMapProgressCallback?.Invoke(null, msg);
        }

        DirectoryInfo directory = new(FileUtil.RaceElementTracks);
        if (!directory.Exists) directory.Create();

        var trackName = ACCSharedMemory.Instance.PageFileStatic.Track.ToLower();
        string path = FileUtil.RaceElementTracks + trackName + ".bin";

        using FileStream fileStream = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        using BinaryWriter binaryWriter = new(fileStream);

        for (var i = 0; i < _trackedPositions.Count; ++i)
        {
            {
                const string msg = "Tracking state -> writing from disk ({0:0.0}%)";
                OnMapProgressCallback?.Invoke(null, String.Format(msg, ((float)i / _trackedPositions.Count) * 100.0f));
            }

            binaryWriter.Write(_trackedPositions[i].X);
            binaryWriter.Write(_trackedPositions[i].Y);
            binaryWriter.Write(_trackedPositions[i].Spline);

            binaryWriter.Write(_trackedPositions[i].DeltaTime);
            binaryWriter.Write(_trackedPositions[i].Kmh);

            binaryWriter.Write(_trackedPositions[i].AccX);
            binaryWriter.Write(_trackedPositions[i].AccY);
            binaryWriter.Write(_trackedPositions[i].AccZ);
        }

        binaryWriter.Close();
        fileStream.Close();
    }
}
