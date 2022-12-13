using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using XLua;
using Debug = UnityEngine.Debug;

public class MandelbrotController : MonoBehaviour
{
    private LuaEnv _luaEnv;
    private string _luaScript;
    private byte[] _wasmCode;
    private byte[] _wasiCode;
    private bool _isRunning = false;
    private int N;

    [SerializeField] Image imageView;
    [SerializeField] Button generateButton;
    [SerializeField] TMP_Dropdown backendDropdown;
    [SerializeField] TMP_Dropdown sizeDropdown;

    [SerializeField] TMP_Text backendText;
    [SerializeField] TMP_Text sizeText;
    [SerializeField] TMP_Text timeText;

    enum BackendMode
    {
        xLua,
        Wasmer,
        Wasmer_JIT
    }

    enum ImageSize
    {
        N120,
        N1000,
        N4000
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
#else
    [DllImport("loader")]
#endif

    static extern uint load(IntPtr code, uint len, uint n, IntPtr result);

#if !UNITY_IOS
    [DllImport("loader_jit")]
    static extern uint load_jit(IntPtr code, uint len, uint n, IntPtr result);
#endif

    // Start is called before the first frame update
    void Start()
    {
        _luaEnv = new LuaEnv();
#if UNITY_IOS
        // Remove JIT option on iOS
        backendDropdown.options.RemoveAt(backendDropdown.options.Count - 1);
#endif
        StartCoroutine(InitCodes(() =>
        {
            Debug.Log("Init Done.");
            generateButton.onClick.AddListener(Generate);
        }));
    }

    async void Generate()
    {
        if (_isRunning) return;
        _isRunning = true;
        generateButton.interactable = false;

        N = SizeIndexToN(sizeDropdown.value);
        var B = backendDropdown.value;

        var textureData = new Byte[(N * N) * 3];
        var headerLen = Encoding.Default.GetBytes($"P4\n{N} {N}\n").Length;

        sizeText.text = $"N : {N}";
        backendText.text = $"B : {((BackendMode) B).ToString()}";

        Stopwatch elapsedTime = new Stopwatch();

        await Task.Run(() =>
        {
            elapsedTime.Start();

            switch (B)
            {
                case (int) BackendMode.xLua:
                {
                    Debug.Log("[xLua] Generating...");
                    _luaEnv.DoString($"local arg = {{{N}}}\n{_luaScript}");
                    Debug.Log("[xLua] Decoding...");
                    var luaRes = _luaEnv.Global.Get<List<List<Byte>>>("result").SelectMany(l => l).ToArray();
                    DecodePbm(new ArraySegment<byte>(luaRes, headerLen, luaRes.Length - headerLen), ref textureData);
                }
                    break;
                case (int) BackendMode.Wasmer:
                {
                    Debug.Log("[Wasmer] Generating...");
                    byte[] buf = new byte[(int) Math.Ceiling(N * N / 8.0) + headerLen];
                    Debug.Log($"{_wasiCode.Length} {N} {buf.Length}");
                    var codePtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(byte)) * _wasiCode.Length);
                    var bufPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(byte)) * buf.Length);
                    Marshal.Copy(_wasiCode, 0, codePtr, _wasiCode.Length);
                    var rLen = (int) load(codePtr, (uint) _wasiCode.Length, (uint) N, bufPtr);
                    Marshal.Copy(bufPtr, buf, 0, rLen);
                    Marshal.FreeCoTaskMem(codePtr);
                    Marshal.FreeCoTaskMem(bufPtr);

                    if (rLen == 0)
                    {
                        Debug.Log("[Wasmer] Error.");
                        break;
                    }
                    Debug.Log("[Wasmer] Decoding...");
                    DecodePbm(new ArraySegment<byte>(buf, headerLen, rLen - headerLen), ref textureData);
                }
                    break;
#if !UNITY_IOS
                case (int) BackendMode.Wasmer_JIT:
                {
                    Debug.Log("[Wasmer (JIT)] Generating...");
                    byte[] buf = new byte[(int) Math.Ceiling(N * N / 8.0) + headerLen + 1];

                    var codePtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(byte)) * _wasmCode.Length);
                    var bufPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(byte)) * buf.Length);
                    Marshal.Copy(_wasmCode, 0, codePtr, _wasmCode.Length);
                    var rLen = (int) load_jit(codePtr, (uint) _wasmCode.Length, (uint) N, bufPtr);
                    Marshal.Copy(bufPtr, buf, 0, rLen);
                    Marshal.FreeCoTaskMem(codePtr);
                    Marshal.FreeCoTaskMem(bufPtr);

                    if (rLen == 0)
                    {
                        Debug.Log("[Wasmer (JIT)] Error.");
                        break;
                    }
                    Debug.Log("[Wasmer (JIT)] Decoding...");
                    DecodePbm(new ArraySegment<byte>(buf, headerLen, rLen - headerLen), ref textureData);
                }
#endif
            }

            elapsedTime.Stop();
            Debug.Log("Done.");
        });

        var result = new Texture2D(N, N, TextureFormat.RGB24, false);
        result.filterMode = FilterMode.Point;
        result.LoadRawTextureData(textureData);
        result.Apply();
        imageView.sprite = Sprite.Create(result, new Rect(0, 0, N, N), Vector2.zero);

        timeText.text = $"T : {elapsedTime.ElapsedMilliseconds} ms";

        generateButton.interactable = true;
        _isRunning = false;
    }

    int SizeIndexToN(int index)
    {
        switch (index)
        {
            case (int) ImageSize.N120:
                return 120;
            case (int) ImageSize.N1000:
                return 1000;
            case (int) ImageSize.N4000:
                return 4000;
        }

        return 0;
    }

    void DecodePbm(ArraySegment<byte> pbmData, ref byte[] textureData)
    {
        var bPerRow = (int) Math.Ceiling(N / 8.0);

        for (var i = N - 1; i >= 0; --i)
        {
            var ri = 0;
            for (var j = 0; j < bPerRow; ++j)
            {
                var pByte = pbmData[(N - i - 1) * bPerRow + j];
                for (var k = 0; k < 8; ++k)
                {
                    if (ri == N) break;
                    if (((pByte << k) & 0x80) != 0x80)
                    {
                        textureData[(i * N + (j * 8 + k)) * 3] = 0xff;
                        textureData[(i * N + (j * 8 + k)) * 3 + 1] = 0xff;
                        textureData[(i * N + (j * 8 + k)) * 3 + 2] = 0xff;
                    }

                    ri++;
                }
            }
        }
    }

    IEnumerator InitCodes(Action callback)
    {
        // Load lua code
#if !UNITY_EDITOR && UNITY_ANDROID
        var luaRequest = UnityWebRequest.Get(
            Path.Combine(Application.streamingAssetsPath, "Lua", "main.lua")
        );
        yield return luaRequest.SendWebRequest();
        _luaScript = luaRequest.downloadHandler.text;
#else
        _luaScript = File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Lua", "main.lua"));
#endif

        // Load wasmer byte code
#if !UNITY_EDITOR && UNITY_ANDROID
        var wasmRequest = UnityWebRequest.Get(
            Path.Combine(Application.streamingAssetsPath, "Rust", "Common", "mandelbrot.wasm")
        );
        yield return wasmRequest.SendWebRequest();
        _wasmCode = wasmRequest.downloadHandler.data;
#elif !UNITY_IOS
        _wasmCode = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "Rust", "Common", "mandelbrot.wasm"));
#endif

        // Load wasmer native code
#if UNITY_EDITOR
        _wasiCode = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "Rust", "arm64", "mandelbrot.arm64.wasmu"));
#elif !UNITY_EDITOR && UNITY_IOS
        _wasiCode = File.ReadAllBytes($"{Application.streamingAssetsPath}/Rust/iOS/mandelbrot.ios.wasmu");
#elif !UNITY_EDITOR && UNITY_ANDROID
        var wasmuRequest = UnityWebRequest.Get(
            Path.Combine(Application.streamingAssetsPath, "Rust", "Android", "mandelbrot.android-arm64.wasmu")
        );
        yield return wasmuRequest.SendWebRequest();
        _wasiCode = wasmuRequest.downloadHandler.data;
#endif

        callback.Invoke();
        yield break;
    }
}
