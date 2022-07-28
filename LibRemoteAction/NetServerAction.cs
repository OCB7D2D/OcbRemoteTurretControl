// Part of Remote Turret Control Mod
// Copyright 2022 Marcel Greter

using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

static class NetServerAction
{

    // White-list methods that are allowed to be called
    // Otherwise opens it for arbitrary remote code execution
    public static readonly HashSet<string> AllowedFunctions = new HashSet<string>
    {
        "XUI_PowerRemoteTurretPanel:LockForClient"
    };

    // Wrap-around should be fairly seldom
    // This is handled client-side, by the way
    static int RequestID = int.MinValue;
    static IEnumerator Coroutine = null;

    // Hold data while we wait
    struct AwaitAnswer
    {
        public float Timeout;
        public Action<object> OnSuccess;
        public Action<object> OnError;
        public Action OnTimeout;
    }

    // List of all waiting responses
    static Dictionary<int, AwaitAnswer> Awaiting
        = new Dictionary<int, AwaitAnswer>();

    // Helper to get fully qualified static function name
    public static string GetMethodDescription(MethodInfo method)
    {
        if (method.DeclaringType != null)
        {
            return method.DeclaringType.FullDescription()
                + ":" + method.Name;
        }
        return method.Name;
    }

    // Called when a package is received from the server
    internal static void OnServerAnswer(int reqid, object rv, bool error)
    {
        // Match the id with our waiting queue
        if (!Awaiting.TryGetValue(reqid, out var promise))
        {
            Log.Error("Server answered to unknown question ({0})", reqid);
        }
        else if (error)
        {
            Awaiting.Remove(reqid);
            promise.OnError.Invoke(rv);
        }
        else
        {
            Awaiting.Remove(reqid);
            promise.OnSuccess.Invoke(rv);
        }
    }

    // Called from time to time to detect timeouts within our waiting queue
    private static bool DetectTimeout(AwaitAnswer promise)
    {
        if (promise.Timeout < 0) return false;
        if (Time.realtimeSinceStartup < promise.Timeout) return false;
        promise.OnTimeout.Invoke(); // Timeout is due
        return true;
    }

    // Coroutine ticking our timeouts
    // Only active if we wait for something
    private static IEnumerator TickTimeouts()
    {
        while(Coroutine != null)
        {
            float now = Time.realtimeSinceStartup;
            Awaiting.RemoveAll(DetectTimeout);
            if (Awaiting.Count == 0) GameManager.
                    Instance.StopCoroutine(Coroutine);
            yield return new WaitForSeconds(0.93f);
        }
    }

    // Main entry function to execute either on client or server
    internal static void RemoteServerCall(string fqfn, object[] args, 
        Action<object> success, Action<object> error, Action abort,
        int entityId = -1, int timeout = -1)
    {
        if (!NetServerAction.AllowedFunctions.Contains(fqfn))
            throw new Exception("Method not white-listed " + fqfn);
        // We can execute it directly on the server
        if (ConnectionManager.Instance.IsServer)
        {
            var types = new Type[args.Length];
            for (var i = 0; i < args.Length; i++)
                types[i] = args[i].GetType();
            MethodInfo method = AccessTools.Method(fqfn, types);
            if (method == null) throw new Exception(
                "Static method not found " + fqfn);
            if (!method.IsStatic) throw new Exception(
                "Only Static methods allowed " + fqfn);
            success(method.Invoke(null, args));
        }
        else
        {
            // Increment static id
            RequestID += 1;
            // Store data for response
            Awaiting.Add(RequestID,
                new AwaitAnswer() {
                    // Set the timer into the future
                    Timeout = timeout < 0 ? -1 : // never?
                        Time.realtimeSinceStartup + timeout,
                    OnSuccess = success,
                    OnError = error,
                    OnTimeout = abort
                });
            // Send package to server to execute the action
            ConnectionManager.Instance.SendToServer(NetPackageManager
                .GetPackage<NetPackageServerAction>()
                .Setup(fqfn, args, entityId, RequestID));
            // Start up coroutine once?
            if (Awaiting.Count == 1)
            {
                Coroutine = TickTimeouts(); // Remember to stop
                GameManager.Instance.StartCoroutine(Coroutine);
            }
        }
    }

}
