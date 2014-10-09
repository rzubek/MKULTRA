using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Assets
{
    public class PerfCounter : MonoBehaviour
    {
        public float updateInterval = 0.5F;

        private float accum = 0; // FPS accumulated over the interval
        private int frames = 0; // Frames drawn over the interval
        private float timeleft; // Left time for current interval

        public float ThisFPS = 0f;

        public long FirstFrameMem = 0;
        public long LastFrameMem = 0;
        public long ThisFrameMem = 0;

        public long MemDeltaFrame = 0;
        public long MemDeltaTotal = 0;

        void LateUpdate () {
            timeleft -= Time.deltaTime;
            accum += Time.timeScale / Time.deltaTime;
            ++frames;

            // Interval ended - update stats and start new interval
            if (timeleft <= 0.0) {
                UpdateStats();

                timeleft += updateInterval;
                accum = 0.0F;
                frames = 0;
            }
        }

        void UpdateStats () {
            ThisFPS = accum/frames;

            LastFrameMem = ThisFrameMem;
            ThisFrameMem = GC.GetTotalMemory(false);
            if (frames < 5) {
                FirstFrameMem = ThisFrameMem;
            }

            MemDeltaFrame = ThisFrameMem - LastFrameMem;
            MemDeltaTotal = ThisFrameMem - FirstFrameMem;
        }

        void OnGUI () {
            string mem = String.Format("{0:F2} FPS\nFrame: mem {1:F2} mb, d {2:F2} kb", ThisFPS, ThisFrameMem / (1024 * 1024.0), MemDeltaFrame / 1024.0);
            GUI.Label(new Rect(0, Screen.height - 40, 400, 40), mem);
        }

    }
}
