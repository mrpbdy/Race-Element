﻿using ACCManager.Data.ACC.Cars;
using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACCManager.Hardware.ACC.SteeringLock
{
    /// <summary>
    /// Partially From https://github.com/Havner/acc-steering-lock
    /// </summary>
    public class SteeringLockTracker : IDisposable
    {
        private static SteeringLockTracker _instance;
        public static SteeringLockTracker Instance
        {
            get
            {
                if (_instance == null) _instance = new SteeringLockTracker();
                return _instance;
            }
        }

        public bool IsTracking = false;

        private string _lastCar;
        private int _lastRotation;
        private IWheelSteerLockSetter _wheel;
        private readonly ACCSharedMemory _sharedMemory;

        private SteeringLockTracker()
        {
            _sharedMemory = new ACCSharedMemory();
            DetectDevices();
        }


        public void Dispose()
        {
            IsTracking = false;

            ResetRotation();

            Debug.WriteLine("Disposed steering lock tracker.");
            _instance = null;
        }

        public void StartTracking()
        {
            new Thread(() =>
            {
                while (IsTracking)
                {
                    Thread.Sleep(1000);

                    if (_sharedMemory.ReadGraphicsPageFile().Status != ACCSharedMemory.AcStatus.AC_OFF)
                    {
                        if (_wheel == null)
                            DetectDevices();

                        SetHardwareLock();

                    }
                    else
                    {
                        _lastCar = null;
                        ResetRotation();
                    }
                }

                ResetRotation();
            }).Start();

        }

        private void SetHardwareLock()
        {
            string currentModel = _sharedMemory.ReadStaticPageFile().CarModel;
            if (_lastCar == currentModel) return;

            // car has changed, if we have no wheel, try to re-detect
            if (_wheel == null)
                DetectDevices();
            _lastCar = currentModel;
            if (_wheel == null) return;

            ResetRotation();
            int rotation = Data.ACC.Cars.SteeringLock.Get(_lastCar);
            Debug.WriteLine("AccSteeringLock: setting rotation of " + _lastCar + " to: " + rotation);

            if (!_wheel.Apply(rotation, false, out _lastRotation))
                Debug.WriteLine("AccSteeringLock: IWheelSteerLockSetter::Apply() failed.");
            else if (rotation != _lastRotation)
                Debug.WriteLine("AccSteeringLock: rotation had to be clamped due to hardware limitations to: " + _lastRotation);

        }

        private void DetectDevices()
        {
            DirectInput di = new DirectInput();

            foreach (DeviceInstance device in di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
            {
                Debug.WriteLine("AccSteeringLock: detected: " + device.ProductGuid + " " + device.ProductName);
                _wheel = WheelSteerLock.Get(device.ProductGuid.ToString());
                if (_wheel != null)
                {
                    Debug.WriteLine("AccSteeringLock: found supported wheel: " + device.ProductName + " handled by: " + _wheel.ControllerName);
                    break;
                }
            }

            if (_wheel == null)
                Debug.WriteLine(" no supported wheel found.");
        }

        private void ResetRotation()
        {
            if (_wheel == null) return;
            if (_lastRotation <= 0) return;

            Debug.WriteLine("AccSteeringLock: resetting rotation from: " + _lastRotation);
            if (!_wheel.Apply(_lastRotation, true, out _lastRotation))
                Debug.WriteLine("AccSteeringLock: IWheelSteerLockSetter::Apply() failed.");
            _lastRotation = 0;
        }

    }
}
