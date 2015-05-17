﻿using System;
using System.Numerics;
using Common;
using SharpBgfx;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

class CallbackHandler : ICallbackHandler {
    public void ReportError (ErrorType errorType, string message) {
        // break if the debugger is attached so we can look at the message
        Debugger.Break();

        // if this wasn't a debug check, the app is dead
        if (errorType != ErrorType.DebugCheck)
            Environment.Exit(1);
    }

    public int GetCachedSize (long id) {
        var file = GetCacheFile(id);
        if (!file.Exists)
            return 0;

        return (int)file.Length;
    }

    public bool GetCacheEntry (long id, IntPtr data, int size) {
        var file = GetCacheFile(id);
        if (!file.Exists)
            return false;

        var bytes = new byte[size];
        using (var stream = file.OpenRead()) {
            var read = stream.Read(bytes, 0, size);
            if (read != size)
                return false;
        }

        // we could avoid this extra copy with some more work, but I'm lazy
        Marshal.Copy(bytes, 0, data, size);
        return true;
    }

    public void SetCacheEntry (long id, IntPtr data, int size) {
        var bytes = new byte[size];
        Marshal.Copy(data, bytes, 0, size);

        var file = GetCacheFile(id);
        using (var stream = file.OpenWrite())
            stream.Write(bytes, 0, size);
    }

    public void SaveScreenShot (string path, int width, int height, int pitch, IntPtr data, int size, bool flipVertical) {
    }

    public void CaptureStarted (int width, int height, int pitch, TextureFormat format, bool flipVertical) {
    }

    public void CaptureFrame (IntPtr data, int size) {
    }

    public void CaptureFinished () {
    }

    static FileInfo GetCacheFile (long id) {
        // we use the cache id as the filename, and just dump in the current directory
        return new FileInfo(id.ToString("x"));
    }
}

static class Program {
    static void Main () {
        // create a UI thread and kick off a separate render thread
        var sample = new Sample("Cubes", 1280, 720);
        sample.Run(RenderThread);
    }

    static unsafe void RenderThread (Sample sample) {
        // initialize the renderer
        Bgfx.Init(RendererBackend.OpenGL, callbackHandler: new CallbackHandler());
        Bgfx.Reset(sample.WindowWidth, sample.WindowHeight, ResetFlags.Vsync);

        // enable debug text
        Bgfx.SetDebugFeatures(DebugFeatures.DisplayText);

        // set view 0 clear state
        Bgfx.SetViewClear(0, ClearTargets.Color | ClearTargets.Depth, 0x303030ff);
        Bgfx.SetViewRect(0, 0, 0, sample.WindowWidth, sample.WindowHeight);

        // create vertex and index buffers
        var vbh = Cube.CreateVertexBuffer();
        var ibh = Cube.CreateIndexBuffer();

        // load shaders
        var program = ResourceLoader.LoadProgram("vs_callback", "fs_callback");

        // start the frame clock
        var time = 0.0f;
        var clock = new Clock();
        clock.Start();

        // 5 seconds of 60 Hz video
        for (int frame = 0; frame < 300; frame++) {
            // tick the clock
            var elapsed = clock.Frame();

            // write some debug text
            Bgfx.DebugTextClear();
            Bgfx.DebugTextWrite(0, 1, 0x4f, "SharpBgfx/Samples/07-Callback");
            Bgfx.DebugTextWrite(0, 2, 0x6f, "Description: Implementing application specific callbacks for taking screen shots,");
            Bgfx.DebugTextWrite(13, 3, 0x6f, "caching OpenGL binary shaders, and video capture.");
            Bgfx.DebugTextWrite(0, 4, 0x6f, string.Format("Frame: {0:F3} ms", elapsed * 1000));

            // view transforms
            var viewMatrix = Matrix4x4.CreateLookAt(new Vector3(0.0f, 0.0f, 35.0f), Vector3.Zero, Vector3.UnitY);
            var projMatrix = Matrix4x4.CreatePerspectiveFieldOfView((float)Math.PI / 3, (float)sample.WindowWidth / sample.WindowHeight, 0.1f, 100.0f);
            Bgfx.SetViewTransform(0, viewMatrix, projMatrix);

            // fixed frame rate
            time += 1.0f / 60.0f;

            // submit 11x11 cubes
            for (int y = 0; y < 11; y++) {
                for (int x = 0; x < 11 - y; x++) {
                    // model matrix
                    var transform = Matrix4x4.CreateFromYawPitchRoll(time + x * 0.21f, time + y * 0.37f, 0.0f);
                    transform.M41 = -15.0f + x * 3.0f;
                    transform.M42 = -15.0f + y * 3.0f;
                    transform.M43 = 0.0f;
                    Bgfx.SetTransform(transform);

                    // set pipeline states
                    Bgfx.SetProgram(program);
                    Bgfx.SetVertexBuffer(vbh);
                    Bgfx.SetIndexBuffer(ibh);
                    Bgfx.SetRenderState(RenderState.Default);

                    // submit primitives
                    Bgfx.Submit(0);
                }
            }

            // take a screenshot at frame 150
            if (frame == 150)
                Bgfx.SaveScreenShot("frame150");

            // advance to next frame
            Bgfx.Frame();
        }

        // clean up
        ibh.Dispose();
        vbh.Dispose();
        program.Dispose();
        Bgfx.Shutdown();
    }
}