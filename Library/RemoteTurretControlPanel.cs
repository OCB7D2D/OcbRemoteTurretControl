// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System;
using System.Collections.Generic;
using UnityEngine;

public class RemoteTurretPanel : PoweredScreenPanel
{

    // Attached block for config and more
    private readonly BlockRemoteTurret Block;

    // Just a reference to our world
    private readonly WorldBase World;

    // Never know what this does!?
    private readonly int ClrIdx = 0;

    // Position of the remote turret control panel
    private readonly Vector3i Position;

    // Reference to in-game screen overlay
    private readonly MeshRenderer Monitor;

    // Remember original material textures
    private readonly Texture OldMainTex;
    private readonly Texture OldEmissionMap;

    // Camera moving from turret to turret
    // Created once on demand, then re-parented
    private Camera Kamera = null;

    // Render texture going along with camera
    // We render the scene into this texture
    // It will then be used in the material
    private RenderTexture Texture = null;

    // Current broadcasting turret 
    private int CurrentCam = 0;

    // List of currently connected turrets
    // Gets updated regularly, e.g. when we
    // have fully cycled through all entries.
    public readonly List<TileEntityPoweredRangedTrap>
        RemoteTurrets = new List<TileEntityPoweredRangedTrap>();

    // Holding all downstream control panels.
    // First entry is always our own tile entity.
    // Designed as a list for future shenanigans.
    public readonly List<TileEntityPowered>
        ControlPanels = new List<TileEntityPowered>();

    // Interval to switch between cameras
    private float CamInterval => Block.ScreenCameraInterval;

    // Return a small deviation for the cam interval
    private float GetRandomCamIntervalOffset(float range)
    {
        return UnityEngine.Random.Range(
            -CamInterval * range,
            +CamInterval * range);
    }

    // Constructor 
    public RemoteTurretPanel(WorldBase world, int clrIdx, Vector3i pos, Transform monitor, BlockRemoteTurret block)
    {
        Block = block; // Set as already as possible
        if (block == null) throw new Exception("No Block");
        LastTick = GetRandomCamIntervalOffset(0.3f);
        World = world; ClrIdx = clrIdx; Position = pos;
        if (World == null) throw new Exception("No World");
        Monitor = monitor?.GetComponent<MeshRenderer>();
        if (Monitor == null) throw new Exception("No Monitor");
        if (Monitor.material == null) throw new Exception("No Material");
        // Store old texture to reset them later if required
        // Note: make sure to reset them on destruction
        OldMainTex = Monitor.material.GetTexture("_MainTex");
        OldEmissionMap = Monitor.material.GetTexture("_EmissionMap");
        Monitor.material.SetColor("_ScreenColor", block.ScreenAlbedoColor);
        Monitor.material.SetColor("_EmissionColor", block.ScreenEmissionColor);
        Monitor.material.SetColor("_EffectColor1", block.ScreenEffectColor1);
        Monitor.material.SetColor("_EffectColor2", block.ScreenEffectColor2);
        Monitor.material.SetInt("_Mode", 1);
    }

    // Called when unloaded
    public void Destroy()
    {
        IsActive = false; // Update throttled camera
        Monitor.material.SetTexture("_MainTex", OldMainTex);
        Monitor.material.SetTexture("_EmissionMap", OldEmissionMap);
        if (Texture) Texture.Release();
        if (Kamera) UnityEngine.Object.DestroyImmediate(Kamera);
        if (Texture) UnityEngine.Object.DestroyImmediate(Texture);
        IsThrottled = false; // Reset just in case
    }

    // Flag to detect time passed
    private float LastTick = 0;

    // Called from our block "manager" (ThrottleCams)
    // Note: this has currently a tick rate of ~0.42s
    public void Tick(bool active)
    {
        // May be true once removed
        active &= Monitor != null;
        // Do nothing if still inactive
        if (!isActive && !active) return;
        // Set active flag
        // Updates throttled
        IsActive = active;
        float now = Time.time;
        // Abort if not waited long enough
        if (now - LastTick < CamInterval) return;
        // Create Cam on demand, but keep it once made
        // Shouldn't be too expensive once disabled?
        CreateCameraOnce();
        // Just for good measures
        if (Kamera == null) return;
        // Switch to next camera
        // Overflow will be checked
        // Resets if nothing loaded
        CurrentCam += 1;
        // Recollect turrets once we have
        // switch through all available ones
        if (CurrentCam >= RemoteTurrets.Count)
        {
            // Gather all connected turrets
            // Does so by following wires
            RemoteTurretUtils.CollectTurrets(
                World, ClrIdx, Position,
                ControlPanels, RemoteTurrets);
            // Reset view index
            CurrentCam = 0;
        }

        // Check what we have potentially re-collected
        bool panel = ControlPanels.Count > 0 && ControlPanels[0] != null;
        IsPowered = panel && ControlPanels[0].IsPowered;
        HasParent = panel && ControlPanels[0].HasParent();

        // Check if still no turrets
        // Or not turrets after recollect
        if (RemoteTurrets.Count == 0)
        {
            Kamera.transform.SetParent(null, false);
            IsCamPowered = HasCamera = false;
        }
        else if (isPowered)
        {
            // This check seems to work fine to discover destroyed tiles!
            // Alternatively we could also register on `TE.Destroyed` events!
            if (RemoteTurrets[CurrentCam].BlockTransform is Transform parent && parent)
            {
                // Fetch camera from potential new turret
                var cam = parent.FindInChilds("camera");
                // For fun try `Camera.main.transform`
                Kamera.transform.SetParent(cam, false);
                // Special check when previous camera was not powered
                // and next one isn't either, force switch of effect to
                // indicate that we actually switched to the next camera.
                if (!IsCamPowered && !RemoteTurrets[CurrentCam].IsPowered)
                {
                    // Forces a material channel/effect switch
                    // Note: ensure to have more than 1 effect
                    int mode = Monitor.material.GetInt("_Mode");
                    int changed = mode; while (changed == mode)
                        changed = UnityEngine.Random.Range(2, 4);
                    Monitor.material.SetInt("_Mode", changed);
                }
                // Update the power state after our special check
                IsCamPowered = RemoteTurrets[CurrentCam].IsPowered;
                HasCamera = cam != null; // Weird, but why not
            }
            // Detected a boo-boo
            // Tile seems to be gone
            else
            {
                // Reset camera and set states to false
                // Note: will not be picked up on next round
                Kamera.transform.SetParent(null, false);
                IsCamPowered = false;
                HasCamera = false;
            }
        }

        // Update monitor state and material
        MonitorState = CurrentScreenState();
        // Fully enable/disable the monitor mesh according to condition
        Monitor.enabled = CurrentScreenVisibility(Block.ScreenShownWhen);

        // Check for state changes
        CheckScreenChanges();

        // Add a slight random variation to the last tick
        // This will make sure next round will deviate a bit
        // Useful if multiple panels start at the same time
        LastTick = now + GetRandomCamIntervalOffset(0.1f);

        // Implement poor mans "night vision"
        // Note: Must be done after other "updates"
        // This is forced to update on every tick
        // Note: this is an experimental feature
        if (isActive && isPowered && hasCamera)
        {
            Color emission = RemoteTurrets[CurrentCam].IsPowered
                ? Block.ScreenEmissionColorCam
                : Block.ScreenEmissionColorEffect;
            var world = GameManager.Instance.World;
            float hour = world.worldTime % 24000UL / 1000f;
            // We assume dawn is always before dusk in day hours
            if (world.DawnHour < world.DuskHour)
            {
                // Calculate hours after dawn or before dusk
                var lighthours = Math.Abs(world.DuskHour - world.DawnHour);
                float light = Mathf.Abs(lighthours / 2f - (hour - world.DawnHour));
                light = lighthours / 2f - light; // Invert range
                // Enhance red channel before dawn and after dusk
                float factor = Mathf.InverseLerp(-0.25f, -0.75f, light);
                emission.r *= 1f + factor * 0.65f;
                emission.g *= 1f + factor * 0.15f;
                emission.b *= 1f + factor * 0.05f;
                Monitor.material.SetColor("_EmissionColor", emission);
            }
        }
    }

    // Copied from `XUiC_CameraWindow:CreateCamera`
    private void CreateCameraOnce()
    {
        if (Kamera != null) return;
        GameObject go = new GameObject("Camera", typeof(Camera));
        Kamera = go.GetComponent<Camera>();
        var main = Camera.main;

        Kamera.allowHDR = main.allowHDR;
        Kamera.allowMSAA = main.allowMSAA;

        Kamera.nearClipPlane = main.nearClipPlane;
        Kamera.depth = main.depth;
        Kamera.farClipPlane = main.farClipPlane;
        Kamera.fieldOfView = 65;

        Kamera.cullingMask = main.cullingMask;

        Kamera.opaqueSortMode = main.opaqueSortMode;
        Kamera.overrideSceneCullingMask = main.overrideSceneCullingMask;
        Kamera.depthTextureMode = main.depthTextureMode;
        
        Kamera.renderingPath = main.renderingPath;

        Kamera.scene = main.scene;
        Kamera.gateFit = main.gateFit;

        Kamera.cameraType = main.cameraType;

        Kamera.backgroundColor = main.backgroundColor;

        Kamera.clearFlags = main.clearFlags;
        Kamera.clearStencilAfterLightingPass = main.clearStencilAfterLightingPass;
        Kamera.useOcclusionCulling = main.useOcclusionCulling;
        Kamera.sensorSize = main.sensorSize;

        // Kamera.focusDistance = main.focusDistance;
        Kamera.focalLength = main.focalLength;

        Kamera.forceIntoRenderTexture = true;


        // Kamera.nearClipPlane = 0.01f;
        // Kamera.depth = -10f;
        // Kamera.farClipPlane = 1000f;
        // Kamera.fieldOfView = 80f;

        // Kamera.depthTextureMode = DepthTextureMode.DepthNormals;
        // Kamera.cullingMask &= -513;
        // Kamera.cullingMask &= -1025;
        // Kamera.renderingPath = RenderingPath.Forward;
        // Kamera.clearFlags = CameraClearFlags.SolidColor;
        Kamera.enabled = false; // Disable initially
        // Fixing if camera has disappeared
        // Otherwise shows black screen only
        if (Texture) IsMonitorDirty = true;
        Texture = CreateRenderTexture();
        Kamera.targetTexture = Texture; // Maybe re-use
        Monitor.material.SetTexture("_MainTex", Texture);
        Monitor.material.SetTexture("_EmissionMap", Texture);
    }

    // Scale down a little from full-screen size
    private RenderTexture CreateRenderTexture()
    {
        int w = Mathf.NextPowerOfTwo(Screen.width / 5);
        return new RenderTexture(w, w / 2, 24);
    }

    // Implement `PoweredScreenPanel`
    // protected override void OnHasParentChanged() { }
    // protected override void OnIsPoweredChanged() { }
    // protected override void OnHasCameraChanged() { }
    // protected override void OnCamPoweredChanged() { }

    protected override void OnScreenChanged()
    {
        // Create on demand
        CreateCameraOnce();
        // Still check for validity
        if (Kamera == null) return;
        // Disable camera rendering first
        Kamera.enabled = isActive;
    }
    
    protected override void OnIsActiveChanged() => IsThrottled = IsActive;

    protected override void OnMonitorStateChanged()
    {
        // Update material
        switch (MonitorState)
        {
            case ScreenState.Unpowered:
                Monitor.material.SetTexture("_MainTex", OldMainTex);
                Monitor.material.SetTexture("_EmissionMap", OldEmissionMap);
                Monitor.material.SetColor("_ScreenColor", Block.ScreenAlbedoColor);
                Monitor.material.SetColor("_EmissionColor", Block.ScreenEmissionColor);
                Monitor.material.SetInt("_Mode", 1);
                break;
            case ScreenState.Powered:
                Monitor.material.SetTexture("_MainTex", OldMainTex);
                Monitor.material.SetTexture("_EmissionMap", OldEmissionMap);
                Monitor.material.SetColor("_ScreenColor", Block.ScreenAlbedoColorOn);
                Monitor.material.SetColor("_EmissionColor", Block.ScreenEmissionColorOn);
                Monitor.material.SetInt("_Mode", 1);
                break;
            case ScreenState.CamView:
                Monitor.material.SetTexture("_MainTex", Texture);
                Monitor.material.SetTexture("_EmissionMap", Texture);
                Monitor.material.SetColor("_ScreenColor", Block.ScreenAlbedoColorCam);
                Monitor.material.SetColor("_EmissionColor", Block.ScreenEmissionColorCam);
                Monitor.material.SetInt("_Mode", 1);
                break;
            case ScreenState.CamEffect:
                Monitor.material.SetTexture("_MainTex", Texture);
                Monitor.material.SetTexture("_EmissionMap", Texture);
                Monitor.material.SetColor("_ScreenColor", Block.ScreenAlbedoColorEffect);
                Monitor.material.SetColor("_EmissionColor", Block.ScreenEmissionColorEffect);
                var seed = UnityEngine.Random.Range(600f, 1200f);
                Monitor.material.SetFloat("_Seed", seed);
                int mode = Monitor.material.GetInt("_Mode");
                int changed = mode; while (changed == mode)
                    changed = UnityEngine.Random.Range(2, 4);
                Monitor.material.SetInt("_Mode", changed);
                break;
        }
    }

    protected override void OnIsThrottledChanged()
    {
        // Add/Remove Camera to/from global ThrottleCams list
        // Will be toggled off on most update calls for performance
        if (IsThrottled) { ThrottleCams.Cameras.Add(this); isThrottled = true; }
        else { ThrottleCams.Cameras.Remove(this); isThrottled = false; }
        OcclusionManager.Instance.SetMultipleCameras(ThrottleCams.Cameras.Count > 0);
    }

    // Implement `IThrottleCam` interface
    public override Camera GetCameraCached() => Kamera;

}
