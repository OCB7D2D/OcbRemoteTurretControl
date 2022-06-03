// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using UnityEngine;

public interface IThrottleCam
{
    bool ShouldRenderThisFrame();
    void WasRenderedThisFrame();
    Camera GetCameraCached();
}

// public int Compare(IThrottleCam a, IThrottleCam b)
// {
//     if (a == null) return b == null ? 0 : 1;
//     else if (b == null) return -1;
//     int va = a.GetScheduled();
//     int vb = b.GetScheduled();
//     if (va < vb) return -1;
//     if (va > vb) return 1;
//     if (va < vb) return -1;
//     return va > vb ? 1 : 0;
// }
// 
// public int Compare(object x, object y)
// {
//     IThrottleCam a = (IThrottleCam)x;
//     IThrottleCam b = (IThrottleCam)y;
//     return Compare(a, b);
// }
