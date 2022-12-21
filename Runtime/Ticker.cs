using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Farmand.Utilities
{
    public class Ticker : MonoBehaviour
    {
        

        private void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        private static Ticker Instance { get; set; }

        private struct Tick
        {
            private float _from;
            private float _to;
            private float _duration;

            private Func<float, float, float, float, float> _easing;

            private Action<float> _onUpdate;
            private Func<TickStop> _checkStop;
            private Action _onDone;
            private Action _onStart;
            
            private float _elapsed;
            private bool _stopping;
            private bool _hasUpdateCallback;
            private bool _hasDoneCallback;
            private bool _hasStopCheck;
            private bool _hasStartCallback;
            private bool _starting;

            public bool Active { get; private set; }

            public Tick(float duration, Action<float> onUpdate = null, Action onDone = null, Func<TickStop> checkStop = null, Action onStart = null)
                : this(0, 1, duration, null, onUpdate, onDone, checkStop, onStart) {}

            public Tick(float duration, Func<float, float, float, float, float> easing,
                Action<float> onUpdate = null, Action onDone = null, Func<TickStop> checkStop = null, Action onStart = null)
                : this(0, 1, duration, easing, onUpdate, onDone, checkStop, onStart) {}

            public Tick(float from, float to, float duration, Action<float> onUpdate = null,
                Action onDone = null, Func<TickStop> checkStop = null, Action onStart = null)
                : this(from, to, duration, null, onUpdate, onDone, checkStop, onStart) {}

            public Tick(float from, float to, float duration,
                Func<float, float, float, float, float> easing,
                Action<float> onUpdate,
                Action onDone,
                Func<TickStop> checkStop,
                Action onStart)
            {
                _elapsed = 0f;
                _stopping = false;

                _from = from;
                _to = to;
                _duration = duration;
                _easing = easing ?? DefaultEasing;

                _onUpdate = onUpdate;
                _checkStop = checkStop;
                _onDone = onDone;
                _onStart = onStart;
                _hasUpdateCallback = _onUpdate != null;
                _hasDoneCallback = _onDone != null;
                _hasStopCheck = _checkStop != null;
                _hasStartCallback = _onStart != null;
                
                Active = _starting = true;
            }

            private static float DefaultEasing(float time, float from, float to, float duration) =>
                Mathf.Lerp(from, to, time / duration);

            public bool Update()
            {
                if (!Active)
                {
                    Profiler.BeginSample("Tick Skip");
                    Profiler.EndSample();
                    return Active;
                }

                Profiler.BeginSample("Tick Update");

                _elapsed += Time.deltaTime;

                if (_hasStopCheck)
                {
                    var stop = _checkStop();
                    if (stop != TickStop.Continue)
                    {
                        Stop(stop == TickStop.StopMoveToEnd);
                    }
                }

                bool done = _elapsed >= _duration;
                float eased = done ? _to : _easing(_elapsed, _from, _to, _duration);

                //if (_stopping)
                //    print($"{Time.frameCount} one last update:");

                if (_starting)
                {
                    RaiseOnStart();
                    _starting = false;
                }
                
                RaiseOnUpdate(eased);

                if (done)
                {
                    RaiseOnDone();
                }

                if (_stopping || done)
                {
                    Reset();
                }
                

                Profiler.EndSample();
                return Active;
            }

            public void Stop(bool moveToEnd = true)
            {
                if (!Active) return;
                _stopping = !moveToEnd;
                if (moveToEnd)
                {
                    _elapsed = _duration;
                }
            }

            private void RaiseOnUpdate(float eased)
            {
                if (_hasUpdateCallback)
                {
                    try
                    {
                        _onUpdate(eased);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            private void RaiseOnDone()
            {
                if (_hasDoneCallback)
                {
                    try
                    {
                        _onDone();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            
            private void RaiseOnStart()
            {
                if (_hasStartCallback)
                {
                    try
                    {
                        _onStart();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }

            public void Reset()
            {
                Active = _starting = false;
                _easing = null;
                _onUpdate = null;
                _onDone = null;
                _onStart = null;
                _checkStop = null;
                _hasUpdateCallback = false;
                _hasDoneCallback = false;
                _hasStopCheck = false;
                _hasStartCallback = false;
            }
        }

        private Queue<Tick> _currentQueue = new Queue<Tick>();
        private Queue<Tick> _nextQueue = new Queue<Tick>();
        private readonly List<Tick> _tickers = new List<Tick>(1000);

        public bool IsPaused { get; set; }

        public static void Tween(float duration, Action<float> onUpdate, Action onDone = null, Action onStart = null)
            => Instance.QueueTween(duration, onUpdate, onDone, onStart);

        public static void Tween(float duration, Func<float, float, float, float, float> easing,
            Action<float> onUpdate, Action onDone = null, Action onStart = null) =>
            Instance.QueueTween(duration, easing, onUpdate, onDone, onStart);

        public static void Tween(float duration, Func<TickStop> checkStop,
            Action<float> onUpdate, Action onDone = null, Action onStart = null)
            => Instance.QueueTween(duration, checkStop, onUpdate, onDone, onStart);

        public static void Tween(float duration, Func<float, float, float, float, float> easing,
            Func<TickStop> checkStop, Action<float> onUpdate, Action onDone = null, Action onStart = null) =>
            Instance.QueueTween(duration, easing, checkStop, onUpdate, onDone, onStart);

        public static void DelayedAction(float delay, Action onDone) => Instance.QueueTween(delay, onDone);

        public void QueueTween(float duration, Action<float> onUpdate, Action onDone = null, Action onStart = null)
        {
            QueueTween(new Tick(duration, onUpdate, onDone, null, onStart));
        }

        public void QueueTween(float duration,
            Func<float, float, float, float, float> easing,
            Action<float> onUpdate, Action onDone = null, Action onStart = null)
        {
            QueueTween(new Tick(duration, easing, onUpdate, onDone, null, onStart));
        }

        public void QueueTween(float duration, Func<TickStop> checkStop,
            Action<float> onUpdate, Action onDone = null, Action onStart = null)
        {
            QueueTween(new Tick(duration, onUpdate, onDone, checkStop, onStart));
        }

        public void QueueTween(float duration, Func<float, float, float, float, float> easing,
            Func<TickStop> checkStop, Action<float> onUpdate, Action onDone = null, Action onStart = null)
        {
            QueueTween(new Tick(duration, easing, onUpdate, onDone, checkStop, onStart));
        }

        public void QueueTween(float delay, Action onDone)
        {
            QueueTween(new Tick(delay, null, onDone));
        }

        private void QueueTween(Tick tick)
        {
            _nextQueue.Enqueue(tick);
        }

        private void Update()
        {
            if (IsPaused)
                return;

            (_currentQueue, _nextQueue) = (_nextQueue, _currentQueue);

            //print($"{Time.frameCount} UPDATE ({_tickers.Count}) ({_currentQueue.Count})");

            for (var i = 0; i < _tickers.Count; i++)
            {
                var tick = _tickers[i];
                if (!tick.Active)
                {
                    if (_currentQueue.Count == 0)
                        continue;

                    // we have new ticks queued, let's pop one and reuse this tick slot for the next frame
                    tick = _currentQueue.Dequeue();
                }
                else
                {
                    tick.Update();
                }

                _tickers[i] = tick;
            }

            while (_currentQueue.Count > 0)
            {
                _tickers.Add(_currentQueue.Dequeue());
            }

        }

        public enum TickStop
        {
            Continue,
            Stop,
            StopMoveToEnd,
        }

        public class Tracker
        {
            private readonly Queue<bool> stops = new Queue<bool>();
            private int queuedTicks;
            private string name;

            public Tracker(string name = null)
            {
                this.name = name ?? "Unknown";
            }

            public void Stop(bool moveToEnd)
            {
                if (queuedTicks == 0 || stops.Count >= queuedTicks)
                    return;
                stops.Enqueue(moveToEnd);
            }

            public void Tween(float duration, Action<float> onUpdate = null, Action onDone = null, Action onStart = null)
            {
                if (onUpdate == null)
                {
                    DelayedAction(duration, onDone);
                }
                else
                {
                    queuedTicks++;
                    //print($"{Time.frameCount} queuing tracker {queuedTicks} {name}");
                    Ticker.Tween(duration, CheckStop, onUpdate,
                        onDone: () =>
                        {
                            queuedTicks--;
                            onDone?.Invoke();
                        },
                        onStart);
                }
            }

            public void Tween(float duration, Func<float, float, float, float, float> easing,
                Action<float> onUpdate = null, Action onDone = null, Action onStart = null)
            {
                if (onUpdate == null)
                {
                    DelayedAction(duration, onDone);
                }
                else
                {
                    queuedTicks++;
                    //print($"{Time.frameCount} queuing tracker {queuedTicks} {name}");
                    Ticker.Tween(duration, easing, CheckStop, onUpdate, 
                        onDone: () =>
                        {
                            queuedTicks--;
                            onDone?.Invoke();
                        },
                        onStart);
                }
            }

            public void DelayedAction(float delay, Action onDone)
            {
                if (onDone == null) 
                    return;

                queuedTicks++;
                Ticker.Tween(delay, CheckStop, onUpdate: null,
                    onDone: () =>
                    {
                        queuedTicks--;
                        onDone?.Invoke();
                    },
                    onStart: null);
            }

            private TickStop CheckStop()
            {
                if (stops.Count > 0)
                {
                    var moveToEnd = stops.Dequeue();
                    if (!moveToEnd)
                    {
                        // done is not going to be called, update this now
                        queuedTicks--;
                    }
                    //print($"{Time.frameCount} stopping tracker {name} {moveToEnd}");
                    return moveToEnd ? TickStop.StopMoveToEnd : TickStop.Stop;
                }

                return TickStop.Continue;
            }

            private void TickDone()
            {
                queuedTicks--;
            }
        }
    }
}