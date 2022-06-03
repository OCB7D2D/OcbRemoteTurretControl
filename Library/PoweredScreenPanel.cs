// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using UnityEngine;

public abstract class PoweredScreenPanel : IThrottleCam
{

    // Set how many frames to wait for the next actual rendering
    // You can update this anytime and camera fps will adjust
    // Will be driven by the "loaded blocks manager"
    public int UpdateInterval = 30;

    // Internal variable counting down
    // Will be reset to `UpdateInterval`
    private int FramesToWait = 0;

    // Options when to show screen
    public enum ShowScreen 
    {
        Always,
        Parented,
        Powered,
        Valid,
    }

    // Return current monitor visibility from other states
    protected bool CurrentScreenVisibility(ShowScreen config)
    {
        // Show our monitor overlay
        switch (config)
        {
            case ShowScreen.Always: return true;
            case ShowScreen.Parented: return hasParent;
            case ShowScreen.Powered: return isPowered;
            case ShowScreen.Valid: return hasCamera;
        }
        return false;
    }

    public enum ScreenState
    {
        Unpowered, // Not enough power
        Powered, // On, but no camera showing
        ScreenSaver, // On, at inactive distance
        CamView, // On, camera is broadcasting
        CamEffect, // On, camera not powered etc.
    }

    // Return current monitor state from other states
    protected ScreenState CurrentScreenState()
    {

        if (isPowered && /* isValid && */isActive && isCamPowered) return ScreenState.CamView;
        else if (isPowered && !hasCamera) return ScreenState.Powered;
        else if (isPowered) return ScreenState.CamEffect;
        else return ScreenState.Unpowered;
    }

    // Set when camera is rendering
    // Will limit the fps for camera
    protected bool isThrottled = false;

    // Set from outside when approached
    // True if player is in proximity
    protected bool isActive = false;

    // Set when parent connect is detected
    // This really is just a wire upstream
    protected bool hasParent = false;

    // Set when the panel has enough power
    // Should only happen if hasParent is true
    protected bool isPowered = false;

    // Set when valid turret is detected
    protected bool hasCamera = false;

    // Set when the power of camera changes
    // Should only happen if hasCamera is true
    protected bool isCamPowered = false;

    // Set when we change screen mode
    protected ScreenState monitorState;

    // Called in case the values are changed
    protected abstract void OnMonitorStateChanged();
    protected abstract void OnIsActiveChanged();
    // protected abstract void OnHasParentChanged();
    // protected abstract void OnIsPoweredChanged();
    // protected abstract void OnHasCameraChanged();
    // protected abstract void OnCamPoweredChanged();
    protected abstract void OnIsThrottledChanged();
    protected abstract void OnScreenChanged();

    // Monitor state changes are a result
    // of any other states being changed
    // Therefore it doesn't set dirty flag
    // It should actually execute the change
    public ScreenState MonitorState
    {
        get => monitorState;
        set {
            if (monitorState == value) return;
            monitorState = value; // Update
            OnMonitorStateChanged();
        }
    }

    // Throttled state changes when camera is removed
    // When we render it, throttled it, otherwise not
    public bool IsThrottled
    {
        get => isThrottled;
        set
        {
            if (isThrottled == value) return;
            isThrottled = value; // Update
            OnIsThrottledChanged();
        }
    }

    // Flag if any possible state changed
    // Indicates that monitor state is due
    protected bool IsMonitorDirty = false;

    // Implement change listener
    public bool IsActive
    {
        get => isActive;
        set
        {
            if (isActive == value) return;
            isActive = value; // Update
            OnIsActiveChanged();
            IsMonitorDirty = true;
        }
    }

    // Set when parent connect is detected
    // This really is just a wire upstream
    public bool HasParent
    {
        get => hasParent;
        set
        {
            if (hasParent == value) return;
            hasParent = value; // Update
            // OnHasParentChanged();
            IsMonitorDirty = true;
        }
    }

    // Set when the panel has enough power
    // Should only happen if hasParent is true
    public bool IsPowered
    {
        get => isPowered;
        set
        {
            if (isPowered == value) return;
            isPowered = value; // Update
            // OnIsPoweredChanged();
            IsMonitorDirty = true;
        }
    }

    // Set when valid turret is detected
    public bool HasCamera
    {
        get => hasCamera;
        set
        {
            if (hasCamera == value) return;
            hasCamera = value; // Update
            // OnHasCameraChanged();
            IsMonitorDirty = true;
        }
    }

    // Set when the power of camera changes
    // Should only happen if hasCamera is true
    public bool IsCamPowered
    {
        get => isCamPowered;
        set
        {
            if (isCamPowered == value) return;
            isCamPowered = value; // Update
            // OnCamPoweredChanged();
            IsMonitorDirty = true;
        }
    }

    // Called when screen rendering is due
    // Updates material if necessary
    public void CheckScreenChanges()
    {
        if (IsMonitorDirty == false) return;
        IsMonitorDirty = false;
        OnScreenChanged();
    }

    // To implement `IThrottleCam` interface
    public abstract Camera GetCameraCached();

    // Implement `IThrottleCam` interface
    public bool ShouldRenderThisFrame()
    {
        if (FramesToWait == 0) return true;
        FramesToWait -= 1;
        return false;
    }

    // Implement `IThrottleCam` interface
    public void WasRenderedThisFrame()
    {
        FramesToWait = UpdateInterval;
    }

}
