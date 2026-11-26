#if XINGSHU_ANDROID_PREVIEW
using System;
using UnityEngine;

namespace XingShuRuntime.Utilities
{
    /// <summary>
    /// Android-native companion audio player using android.media.MediaPlayer via JNI.
    /// Zero P/Invoke imports, zero stripped void Call() dependencies, and zero dependency on Unity AudioManager.
    /// Uses java.lang.reflect.Method.invoke (returns Object -> CallObjectMethod) so all void Java methods
    /// execute cleanly even with Astral Party's stripped UnityEngine.AndroidJNIModule.dll.
    /// </summary>
    internal sealed class AndroidLobbyAudioPlayer : IDisposable
    {
        private AndroidJavaObject _player;
        private bool _prepared;
        private bool _paused;
        private bool _muted = true;
        private float _volume = CustomLobbyBgPolicy.DefaultVolume;
        private float _speed = CustomLobbyBgPolicy.DefaultSpeed;

        private static bool _jniInit;
        private static IntPtr _clsClass;
        private static IntPtr _clsObject;
        private static IntPtr _clsMethod;
        private static IntPtr _midGetMethod;
        private static IntPtr _midInvoke;

        internal bool HasAudio => _prepared && _player != null;

        internal void Prewarm()
        {
        }

        internal void TickMainThread()
        {
        }

        internal double CurrentTimeSeconds
        {
            get
            {
                if (!_prepared || _player == null) return 0.0;
                try
                {
                    return _player.Call<int>("getCurrentPosition") / 1000.0;
                }
                catch
                {
                    return 0.0;
                }
            }
        }

        internal void StartFile(string fullPath, bool muted, float volume, float speed, bool paused, double startSeconds)
        {
            Stop();
            if (Application.platform != RuntimePlatform.Android || string.IsNullOrEmpty(fullPath))
            {
                return;
            }
            _muted = muted;
            _volume = CustomLobbyBgPolicy.ClampVolume(volume);
            _speed = CustomLobbyBgPolicy.ClampSpeed(speed);
            _paused = paused;

            try
            {
                AndroidJavaObject mp = new AndroidJavaObject("android.media.MediaPlayer");
                if (!InvokeString(mp, "setDataSource", fullPath))
                {
                    SafeDisposePlayer(mp);
                    return;
                }
                InvokeBool(mp, "setLooping", true);
                if (!InvokeZeroArg(mp, "prepare"))
                {
                    SafeDisposePlayer(mp);
                    return;
                }

                _player = mp;
                _prepared = true;

                ApplyVolumeAndSpeed();
                if (startSeconds > 0.05)
                {
                    InvokeInt(mp, "seekTo", (int)(startSeconds * 1000.0));
                }
                if (!paused)
                {
                    InvokeZeroArg(mp, "start");
                }
            }
            catch
            {
                Stop();
            }
        }

        internal void SetAudioParams(bool muted, float volume, float speed)
        {
            _muted = muted;
            _volume = CustomLobbyBgPolicy.ClampVolume(volume);
            _speed = CustomLobbyBgPolicy.ClampSpeed(speed);
            ApplyVolumeAndSpeed();
        }

        internal void SetPaused(bool paused)
        {
            _paused = paused;
            if (!_prepared || _player == null) return;
            try
            {
                bool playing = _player.Call<bool>("isPlaying");
                if (paused && playing)
                {
                    InvokeZeroArg(_player, "pause");
                }
                else if (!paused && !playing)
                {
                    InvokeZeroArg(_player, "start");
                }
            }
            catch
            {
            }
        }

        internal void RequestSeek(double seconds)
        {
            if (!_prepared || _player == null) return;
            int ms = seconds > 0.0 ? (int)(seconds * 1000.0) : 0;
            InvokeInt(_player, "seekTo", ms);
        }

        internal void Stop()
        {
            _prepared = false;
            if (_player != null)
            {
                AndroidJavaObject old = _player;
                _player = null;
                SafeDisposePlayer(old);
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void ApplyVolumeAndSpeed()
        {
            if (!_prepared || _player == null) return;
            float eff = _muted ? 0f : _volume;
            InvokeTwoFloats(_player, "setVolume", eff, eff);

            try
            {
                using (AndroidJavaObject pp = _player.Call<AndroidJavaObject>("getPlaybackParams"))
                {
                    if (pp != null)
                    {
                        using (AndroidJavaObject updated = pp.Call<AndroidJavaObject>("setSpeed", _speed))
                        {
                            InvokePlaybackParams(_player, updated ?? pp);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void SafeDisposePlayer(AndroidJavaObject mp)
        {
            if (mp == null) return;
            try { InvokeZeroArg(mp, "stop"); } catch { }
            try { InvokeZeroArg(mp, "release"); } catch { }
            try { mp.Dispose(); } catch { }
        }

        private static bool EnsureJniRefs()
        {
            if (_jniInit)
            {
                return _clsClass != IntPtr.Zero && _midGetMethod != IntPtr.Zero && _midInvoke != IntPtr.Zero;
            }
            _jniInit = true;
            try
            {
                IntPtr localClass = AndroidJNI.FindClass("java/lang/Class");
                IntPtr localObj = AndroidJNI.FindClass("java/lang/Object");
                IntPtr localMethod = AndroidJNI.FindClass("java/lang/reflect/Method");
                ClearJniException();
                if (localClass == IntPtr.Zero || localObj == IntPtr.Zero || localMethod == IntPtr.Zero)
                {
                    return false;
                }
                _clsClass = AndroidJNI.NewGlobalRef(localClass);
                _clsObject = AndroidJNI.NewGlobalRef(localObj);
                _clsMethod = AndroidJNI.NewGlobalRef(localMethod);
                AndroidJNI.DeleteLocalRef(localClass);
                AndroidJNI.DeleteLocalRef(localObj);
                AndroidJNI.DeleteLocalRef(localMethod);

                _midGetMethod = AndroidJNI.GetMethodID(_clsClass, "getMethod", "(Ljava/lang/String;[Ljava/lang/Class;)Ljava/lang/reflect/Method;");
                _midInvoke = AndroidJNI.GetMethodID(_clsMethod, "invoke", "(Ljava/lang/Object;[Ljava/lang/Object;)Ljava/lang/Object;");
                ClearJniException();
                return _midGetMethod != IntPtr.Zero && _midInvoke != IntPtr.Zero;
            }
            catch
            {
                ClearJniException();
                return false;
            }
        }

        private static bool InvokeZeroArg(AndroidJavaObject target, string name)
        {
            return InvokeReflected(target, name, null, null);
        }

        private static bool InvokeString(AndroidJavaObject target, string name, string val)
        {
            IntPtr strPtr = IntPtr.Zero;
            try
            {
                using (AndroidJavaClass clsUtil = new AndroidJavaClass("java.lang.Class"))
                using (AndroidJavaObject strClass = clsUtil.CallStatic<AndroidJavaObject>("forName", "java.lang.String"))
                {
                    strPtr = AndroidJNI.NewString(val ?? "");
                    return InvokeReflected(
                        target,
                        name,
                        new IntPtr[] { strClass.GetRawObject() },
                        new IntPtr[] { strPtr });
                }
            }
            catch
            {
                ClearJniException();
                return false;
            }
            finally
            {
                if (strPtr != IntPtr.Zero) AndroidJNI.DeleteLocalRef(strPtr);
            }
        }

        private static bool InvokeBool(AndroidJavaObject target, string name, bool val)
        {
            try
            {
                using (AndroidJavaClass wrapCls = new AndroidJavaClass("java.lang.Boolean"))
                using (AndroidJavaObject typeObj = wrapCls.GetStatic<AndroidJavaObject>("TYPE"))
                using (AndroidJavaObject argObj = wrapCls.CallStatic<AndroidJavaObject>("valueOf", val))
                {
                    return InvokeReflected(
                        target,
                        name,
                        new IntPtr[] { typeObj.GetRawObject() },
                        new IntPtr[] { argObj.GetRawObject() });
                }
            }
            catch
            {
                ClearJniException();
                return false;
            }
        }

        private static bool InvokeInt(AndroidJavaObject target, string name, int val)
        {
            try
            {
                using (AndroidJavaClass wrapCls = new AndroidJavaClass("java.lang.Integer"))
                using (AndroidJavaObject typeObj = wrapCls.GetStatic<AndroidJavaObject>("TYPE"))
                using (AndroidJavaObject argObj = wrapCls.CallStatic<AndroidJavaObject>("valueOf", val))
                {
                    return InvokeReflected(
                        target,
                        name,
                        new IntPtr[] { typeObj.GetRawObject() },
                        new IntPtr[] { argObj.GetRawObject() });
                }
            }
            catch
            {
                ClearJniException();
                return false;
            }
        }

        private static bool InvokeTwoFloats(AndroidJavaObject target, string name, float a, float b)
        {
            try
            {
                using (AndroidJavaClass wrapCls = new AndroidJavaClass("java.lang.Float"))
                using (AndroidJavaObject typeObj = wrapCls.GetStatic<AndroidJavaObject>("TYPE"))
                using (AndroidJavaObject argA = wrapCls.CallStatic<AndroidJavaObject>("valueOf", a))
                using (AndroidJavaObject argB = wrapCls.CallStatic<AndroidJavaObject>("valueOf", b))
                {
                    IntPtr tPtr = typeObj.GetRawObject();
                    return InvokeReflected(
                        target,
                        name,
                        new IntPtr[] { tPtr, tPtr },
                        new IntPtr[] { argA.GetRawObject(), argB.GetRawObject() });
                }
            }
            catch
            {
                ClearJniException();
                return false;
            }
        }

        private static bool InvokePlaybackParams(AndroidJavaObject target, AndroidJavaObject pp)
        {
            if (pp == null) return false;
            try
            {
                using (AndroidJavaClass clsUtil = new AndroidJavaClass("java.lang.Class"))
                using (AndroidJavaObject ppClass = clsUtil.CallStatic<AndroidJavaObject>("forName", "android.media.PlaybackParams"))
                {
                    return InvokeReflected(
                        target,
                        "setPlaybackParams",
                        new IntPtr[] { ppClass.GetRawObject() },
                        new IntPtr[] { pp.GetRawObject() });
                }
            }
            catch
            {
                ClearJniException();
                return false;
            }
        }

        private static bool InvokeReflected(AndroidJavaObject target, string methodName, IntPtr[] paramClassPtrs, IntPtr[] argObjPtrs)
        {
            if (target == null || !EnsureJniRefs()) return false;

            IntPtr rawTarget = target.GetRawObject();
            if (rawTarget == IntPtr.Zero) return false;

            IntPtr targetCls = IntPtr.Zero;
            IntPtr jName = IntPtr.Zero;
            IntPtr pArray = IntPtr.Zero;
            IntPtr methodObj = IntPtr.Zero;
            IntPtr aArray = IntPtr.Zero;
            IntPtr retObj = IntPtr.Zero;

            try
            {
                targetCls = AndroidJNI.GetObjectClass(rawTarget);
                if (targetCls == IntPtr.Zero) return false;

                jName = AndroidJNI.NewString(methodName);
                int pCount = paramClassPtrs != null ? paramClassPtrs.Length : 0;
                pArray = AndroidJNI.NewObjectArray(pCount, _clsClass, IntPtr.Zero);
                for (int i = 0; i < pCount; i++)
                {
                    AndroidJNI.SetObjectArrayElement(pArray, i, paramClassPtrs[i]);
                }

                jvalue[] gmArgs = new jvalue[2];
                gmArgs[0].l = jName;
                gmArgs[1].l = pArray;
                methodObj = AndroidJNI.CallObjectMethod(targetCls, _midGetMethod, gmArgs);
                if (ClearJniException() || methodObj == IntPtr.Zero)
                {
                    return false;
                }

                int aCount = argObjPtrs != null ? argObjPtrs.Length : 0;
                aArray = AndroidJNI.NewObjectArray(aCount, _clsObject, IntPtr.Zero);
                for (int i = 0; i < aCount; i++)
                {
                    AndroidJNI.SetObjectArrayElement(aArray, i, argObjPtrs[i]);
                }

                jvalue[] invArgs = new jvalue[2];
                invArgs[0].l = rawTarget;
                invArgs[1].l = aArray;
                retObj = AndroidJNI.CallObjectMethod(methodObj, _midInvoke, invArgs);
                return !ClearJniException();
            }
            catch
            {
                ClearJniException();
                return false;
            }
            finally
            {
                if (retObj != IntPtr.Zero) AndroidJNI.DeleteLocalRef(retObj);
                if (aArray != IntPtr.Zero) AndroidJNI.DeleteLocalRef(aArray);
                if (methodObj != IntPtr.Zero) AndroidJNI.DeleteLocalRef(methodObj);
                if (pArray != IntPtr.Zero) AndroidJNI.DeleteLocalRef(pArray);
                if (jName != IntPtr.Zero) AndroidJNI.DeleteLocalRef(jName);
                if (targetCls != IntPtr.Zero) AndroidJNI.DeleteLocalRef(targetCls);
            }
        }

        private static bool ClearJniException()
        {
            if (AndroidJNI.ExceptionOccurred() != IntPtr.Zero)
            {
                AndroidJNI.ExceptionClear();
                return true;
            }
            return false;
        }
    }
}
#endif
