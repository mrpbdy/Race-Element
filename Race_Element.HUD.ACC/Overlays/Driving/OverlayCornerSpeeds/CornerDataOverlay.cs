﻿using RaceElement.Data.ACC.Database.SessionData;
using RaceElement.Data.ACC.Session;
using RaceElement.HUD.Overlay.Internal;
using RaceElement.HUD.Overlay.OverlayUtil;
using RaceElement.Util.SystemExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RaceElement.Data.ACC.Tracks.TrackData;

namespace RaceElement.HUD.ACC.Overlays.Driving.OverlayCornerSpeeds
{
    [Overlay(Name = "Corner Data",
            Description = "Shows minimum speed and other data for each corner.",
            OverlayCategory = OverlayCategory.Lap,
            OverlayType = OverlayType.Release)]
    internal sealed class CornerDataOverlay : AbstractOverlay
    {
        private readonly CornerDataConfiguration _config = new CornerDataConfiguration();

        private readonly List<CornerData> _cornerDatas;
        private InfoTable _table;
        private AbstractTrackData _currentTrack;
        private int _previousCorner = -1;
        private CornerData _currentCorner;

        public CornerDataOverlay(Rectangle rectangle) : base(rectangle, "Corner Data")
        {
            _cornerDatas = new List<CornerData>();
            Width = 200;
            Height = 150;
        }

        public override void SetupPreviewData()
        {
            Random rand = new Random();
            for (int i = 1; i < _config.Table.CornerCount + 1; i++)
            {
                _cornerDatas.Add(new CornerData()
                {
                    CornerNumber = i,
                    MinimumSpeed = (float)(rand.NextDouble() * 100f)
                });
            }
        }

        public override void BeforeStart()
        {
            _table = new InfoTable(10, new int[] { 10, 60 });
            RaceSessionTracker.Instance.OnNewSessionStarted += OnNewSessionStarted;
        }

        private void OnNewSessionStarted(object sender, DbRaceSession rs)
        {
            _currentTrack = GetCurrentTrack();
        }

        public override void BeforeStop()
        {
            RaceSessionTracker.Instance.OnNewSessionStarted -= OnNewSessionStarted;
        }

        public AbstractTrackData GetCurrentTrack()
        {
            if (pageStatic.Track == string.Empty) return null;

            return Tracks.Find(x => x.GameName == pageStatic.Track);
        }

        public int GetCurrentCorner()
        {
            _currentTrack ??= GetCurrentTrack();
            if (_currentTrack == null) return -1;

            try
            {
                return _currentTrack.CornerNames.First(x => pageGraphics.NormalizedCarPosition > x.Key.From && pageGraphics.NormalizedCarPosition < x.Key.To).Value.Item1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private struct CornerData
        {
            public int CornerNumber { get; set; }
            public float MinimumSpeed { get; set; }
        }

        public override void Render(Graphics g)
        {
            int currentCorner = GetCurrentCorner();
            if (currentCorner == -1 && _previousCorner != -1)
            {  // corner exited
                _cornerDatas.Add(_currentCorner);
                _previousCorner = -1;
            }

            bool isInCorner = false;
            if (currentCorner != -1)
            {
                if (currentCorner == _previousCorner)
                {
                    isInCorner = true;
                    // we're still in the current corner..., perhaps do some checks?
                    if (_currentCorner.MinimumSpeed > pagePhysics.SpeedKmh)
                        _currentCorner.MinimumSpeed = pagePhysics.SpeedKmh;

                    string minSpeed = $"{_currentCorner.MinimumSpeed:F1}";
                    minSpeed = minSpeed.FillStart(5, ' ');
                    _table.AddRow($"{currentCorner}", new string[] { $"{minSpeed}" });
                }
                else
                {
                    _previousCorner = currentCorner;
                    Debug.WriteLine("Entered a new corner!");
                    _currentCorner = new CornerData()
                    {
                        CornerNumber = currentCorner,
                        MinimumSpeed = float.MaxValue
                    };
                }
            }

            foreach (var corner in _cornerDatas.Skip(_cornerDatas.Count - _config.Table.CornerCount).Reverse().Take(isInCorner ? _config.Table.CornerCount - 1 : _config.Table.CornerCount))
            {
                string minSpeed = $"{corner.MinimumSpeed:F1}";
                minSpeed = minSpeed.FillStart(5, ' ');
                _table.AddRow($"{corner.CornerNumber}", new string[] { $"{minSpeed}" });
            }

            // draw table of previous corners, min speed? corner g? min gear? 
            _table.Draw(g);
        }
    }
}
