﻿using ACCManager.HUD.Overlay.Internal;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ACCManager.ACCSharedMemory;

namespace ACCManager.HUD.ACC.Overlays.OverlayPressureTrace
{
    internal sealed class PressureTraceOverlay : AbstractOverlay
    {
        internal static PressureTraceOverlay Instance;
        private TyrePressureDataCollector _dataCollector;

        public PressureTraceOverlay(Rectangle rectangle) : base(rectangle, "Pressure Trace Overlay")
        {
            //this.X = ScreenWidth - width;

            this.Width = 140;
            this.Height = 60 * 2;
            //this.Y = (int)(ScreenHeight / 2) - this.Height / 2;
            this.RequestsDrawItself = true;
            Instance = this;
        }

        public sealed override void BeforeStart()
        {
            TyrePressureGraph.PressureRange = TyrePressures.GetCurrentRange(pageGraphics.TyreCompound, pageStatic.CarModel);
            _dataCollector = new TyrePressureDataCollector() { TraceCount = this.Width / 2 - 1 };
            _dataCollector.Start();
        }

        public sealed override void BeforeStop()
        {
            _dataCollector.Stop();
        }

        public sealed override void Render(Graphics g)
        {

            TyrePressureGraph.PressureRange = TyrePressures.GetCurrentRange(pageGraphics.TyreCompound, pageStatic.CarModel);
            TyrePressureGraph graph = new TyrePressureGraph(0, 0, this.Width / 2 - 1, (this.Height / 2) - 1, _dataCollector.FrontLeft);
            TyrePressureGraph graph1 = new TyrePressureGraph(this.Width / 2, 0, this.Width / 2 - 1, (this.Height / 2) - 1, _dataCollector.FrontRight);
            TyrePressureGraph graph2 = new TyrePressureGraph(0, (this.Height / 2) * 1, this.Width / 2 - 1, (this.Height / 2) - 1, _dataCollector.RearLeft);
            TyrePressureGraph graph3 = new TyrePressureGraph(this.Width / 2, (this.Height / 2) * 1, this.Width / 2 - 1, (this.Height / 2) - 1, _dataCollector.RearRight);

            graph.Draw(g);
            graph1.Draw(g);
            graph2.Draw(g);
            graph3.Draw(g);
        }

        public sealed override bool ShouldRender()
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
