using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BUGrockLauncher.WPFDataTypes {

    public class ChromeSpeedCalculator {
        private readonly int _windowSizeSeconds;
        private readonly Queue<double> _samples = new();
        private readonly Stopwatch _stopwatch = new();
        private long _lastBytes = 0;
        private long _lastTimeMs = 0;
        private long _lastProgressTickMs = 0;

        public ChromeSpeedCalculator(int windowSizeSeconds = 5) {
            _windowSizeSeconds = Math.Max(1, windowSizeSeconds);
        }

        public void Start(long initialBytes = 0) {
            _stopwatch.Restart();
            _lastBytes = initialBytes;
            _lastTimeMs = 0;
            _lastProgressTickMs = 0;
            _samples.Clear();
        }

        public void Reset() {
            _stopwatch.Reset();
            _lastBytes = 0;
            _lastTimeMs = 0;
            _lastProgressTickMs = 0;
            _samples.Clear();
        }

        public string UpdateProgress(long currentTotalBytes) {
            if (!_stopwatch.IsRunning) {
                Start(currentTotalBytes);
                return "0.0 MB/s";
            }

            long nowMs = _stopwatch.ElapsedMilliseconds;
            _lastProgressTickMs = nowMs;
            long elapsedMs = nowMs - _lastTimeMs;

            // Take sample every 400ms to avoid zero/micro-interval spikes and smooth socket buffer bursts
            if (elapsedMs >= 400) {
                long bytesReceived = currentTotalBytes - _lastBytes;
                if (bytesReceived >= 0) {
                    double elapsedSeconds = elapsedMs / 1000.0;
                    double instantSpeed = bytesReceived / elapsedSeconds;

                    _samples.Enqueue(instantSpeed);

                    int maxSamples = (int)(_windowSizeSeconds * 2.5); // 2.5 samples per second for _windowSizeSeconds
                    while (_samples.Count > maxSamples) {
                        _samples.Dequeue();
                    }

                    _lastTimeMs = nowMs;
                    _lastBytes = currentTotalBytes;
                }
            }

            return GetCurrentSpeedText();
        }

        public string GetCurrentSpeedText() {
            if (!_stopwatch.IsRunning || _samples.Count == 0) {
                return "";
            }

            // If no progress update received for over 2 seconds, download stalled
            if (_stopwatch.ElapsedMilliseconds - _lastProgressTickMs > 2000) {
                return "0.0 MB/s";
            }

            double sum = 0;
            foreach (double speed in _samples) {
                sum += speed;
            }
            double avgBytesPerSec = sum / _samples.Count;
            double speedMB = avgBytesPerSec / (1024.0 * 1024.0);

            if (speedMB >= 10.0) {
                return $"{speedMB:0.0} MB/s";
            } else if (speedMB >= 0.1) {
                return $"{speedMB:0.2} MB/s";
            } else if (avgBytesPerSec > 0) {
                double speedKB = avgBytesPerSec / 1024.0;
                return $"{speedKB:0} KB/s";
            } else {
                return "0.0 MB/s";
            }
        }
    }

}
