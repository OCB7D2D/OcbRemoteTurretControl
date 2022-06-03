// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using System.Collections.Generic;
using UnityEngine;

static class ThrottleCams
{

	// We use a list here since we don't expect too many cameras at once
	// Adding and removing will get bad once you get into the 100s
    public static List<IThrottleCam> Cameras = new List<IThrottleCam>();

	// Maximum number of cams rendered per frame
	// Limits the maximum pressure on the GPU
	public static int MaxCamsPerFrame = 2;

	// Should be called before frame updates by unity
	// Simply register it into `ModEvents.UnityUpdate`
	public static void BeforeFrameUpdate()
	{
		int rendered = 0;
		if (Cameras == null) return;
        World world = GameManager.Instance.World;
		if (world == null || world.m_WorldEnvironment == null) return;
		// As a bonus we also update ambient color for you
		var AC = world.m_WorldEnvironment.GetAmbientColor();
		// Always loop over the full array
		// Keep work for each item to a minimum
		for (int i = 0; i < Cameras.Count; i++)
		{
			var throttle = Cameras[i];
			// Implementer should do the caching!
			Camera cam = throttle?.GetCameraCached();
			// Check if we got any surprises?
			if (throttle == null || cam == null)
			{
				// Remove current cam
				Cameras.RemoveAt(i);
				// Reverse and continue
				--i; continue;
			}
			// Check if frame is due?
			// Must call on each frame!
			if (throttle.ShouldRenderThisFrame())
			{
				// Check if we exhausted our share
				if (rendered < MaxCamsPerFrame)
				{
					// Cost accounting
					rendered += 1;
					// Adjust before rendering
					cam.backgroundColor = AC;
					// Enable cam for this frame
					cam.enabled = true;
					// Inform that it was rendered
					throttle.WasRenderedThisFrame();
				}
				else
				{
					// Disable this frame
					cam.enabled = false;
				}
			}
			else
			{
				// Disable this frame
				cam.enabled = false;
			}
		}
	}

}

