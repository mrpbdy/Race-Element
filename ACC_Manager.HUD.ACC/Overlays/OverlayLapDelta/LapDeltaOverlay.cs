﻿using ACCManager.HUD.ACC.Data.Tracker;
using ACCManager.HUD.Overlay.Internal;
using ACCManager.HUD.Overlay.OverlayUtil;
using ACCManager.HUD.Overlay.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACCManager.ACCSharedMemory;

namespace ACCManager.HUD.ACC.Overlays.OverlayLapDelta
{
    internal sealed class LapDeltaOverlay : AbstractOverlay
    {
        private readonly LapDeltaConfig config = new LapDeltaConfig();
        private class LapDeltaConfig : OverlayConfiguration
        {
            public bool ShowSectors { get; set; } = true;
            public LapDeltaConfig() : base()
            {
                this.AllowRescale = true;
            }
        }

        private const int overlayWidth = 200;
        private int overlayHeight = 150;

        private LapData lastLap = null;

        InfoPanel panel = new InfoPanel(11, overlayWidth);
        public LapDeltaOverlay(Rectangle rectangle) : base(rectangle, "Lap Delta Overlay")
        {
            overlayHeight = panel.FontHeight * 4;

            this.Width = overlayWidth + 1;
            this.Height = overlayHeight + 1;
            RefreshRateHz = 10;
        }

        public override void BeforeStart()
        {
            if (!this.config.ShowSectors)
                this.Height -= this.panel.FontHeight * 3;

            LapTimeTracker.Instance.LapFinished += Collector_LapFinished;
        }

        public override void BeforeStop()
        {
            LapTimeTracker.Instance.LapFinished -= Collector_LapFinished;
        }

        private void Collector_LapFinished(object sender, LapData e)
        {
            if (e.Sector1 != -1 && e.Sector2 != -1 && e.Sector3 != -1)
                lastLap = e;
        }

        public override void Render(Graphics g)
        {
            double delta = (double)pageGraphics.DeltaLapTimeMillis / 1000;
            panel.AddDeltaBarWithCenteredText($"{delta:F3}", -1.5, 1.5, delta);

            if (this.config.ShowSectors)
                AddSectorLines();

            panel.Draw(g);
        }

        private void AddSectorLines()
        {
            LapData lap = LapTimeTracker.Instance.CurrentLap;

            if (lastLap != null && pageGraphics.NormalizedCarPosition < 0.08)
                lap = lastLap;

            string sector1 = "-";
            string sector2 = "-";
            string sector3 = "-";
            if (LapTimeTracker.Instance.CurrentLap.Sector1 > -1)
            {
                sector1 = $"{((float)lap.Sector1 / 1000):F3}";
            }
            else if (pageGraphics.CurrentSectorIndex == 0)
                sector1 = $"{((float)pageGraphics.CurrentTimeMs / 1000):F3}";


            if (lap.Sector2 > -1)
                sector2 = $"{((float)lap.Sector2 / 1000):F3}";
            else if (lap.Sector1 > -1)
            {
                sector2 = $"{(((float)pageGraphics.CurrentTimeMs - lap.Sector1) / 1000):F3}";
            }

            if (lap.Sector3 > -1)
                sector3 = $"{((float)lap.Sector3 / 1000):F3}";
            else if (lap.Sector2 > -1 && pageGraphics.CurrentSectorIndex == 2)
            {
                sector3 = $"{(((float)pageGraphics.CurrentTimeMs - lap.Sector2 - lap.Sector1) / 1000):F3}";
            }


            if (pageGraphics.CurrentSectorIndex != 0 && lap.Sector1 != -1 && lap.IsValid)
                panel.AddLine("S1", $"{sector1}", LapTimeTracker.Instance.IsSectorFastest(1, lap.Sector1) ? Brushes.LimeGreen : Brushes.White);
            else
                panel.AddLine("S1", $"{sector1}");

            if (pageGraphics.CurrentSectorIndex != 1 && lap.Sector2 != -1 && lap.IsValid)
                panel.AddLine("S2", $"{sector2}", LapTimeTracker.Instance.IsSectorFastest(2, lap.Sector2) ? Brushes.LimeGreen : Brushes.White);
            else
                panel.AddLine("S2", $"{sector2}");

            if (pageGraphics.CurrentSectorIndex != 2 && lap.Sector3 != -1 && lap.IsValid)
                panel.AddLine("S3", $"{sector3}", LapTimeTracker.Instance.IsSectorFastest(3, lap.Sector3) ? Brushes.LimeGreen : Brushes.White);
            else
                panel.AddLine("S3", $"{sector3}");
        }

        public override bool ShouldRender()
        {
#if DEBUG
            return true;
#endif
            bool shouldRender = true;
            if (pageGraphics.Status == AcStatus.AC_OFF || pageGraphics.Status == AcStatus.AC_PAUSE || (pageGraphics.IsInPitLane == true && !pagePhysics.IgnitionOn))
                shouldRender = false;

            return shouldRender;
        }
    }
}
