using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public static class Extensions
{
    public static void LogTime(this Stopwatch stopwatch, string message) {
        UnityEngine.Debug.Log(message + ": " + stopwatch.Elapsed.TotalMilliseconds + "ms");
    }
}
