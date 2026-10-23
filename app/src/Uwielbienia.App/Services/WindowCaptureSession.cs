using System.Runtime.InteropServices;

namespace Uwielbienia.App.Services;

/// <summary>
/// Przechwytywanie jednego okna przez Windows Graphics Capture (Windows 10 2004+) — tak jak „Przechwytywanie okna” w OBS:
/// działa także dla okna zasłoniętego innymi, sprzętowo (Direct3D 11). Wywołania WinRT i Direct3D wprost przez tablice
/// metod COM, żeby projekt został przy zwykłym <c>net10.0</c> (ten sam dla Linuksa). Wszystkie obiekty są wolnowątkowe;
/// sesja należy do jednego wątku (<see cref="AppWindowCapture"/>).
/// </summary>
internal sealed unsafe class WindowCaptureSession : IDisposable
{
    /// <summary><c>DirectXPixelFormat.B8G8R8A8UIntNormalized</c> — układ bajtów <c>Bgra8888</c> Avalonii.</summary>
    private const int Bgra8 = 87;
    private const int Buffers = 2;

    private nint _device;        // ID3D11Device
    private nint _context;       // ID3D11DeviceContext
    private nint _winrtDevice;   // IDirect3DDevice
    private nint _item;          // IGraphicsCaptureItem
    private nint _pool;          // IDirect3D11CaptureFramePool
    private nint _session;       // IGraphicsCaptureSession
    private nint _staging;       // ID3D11Texture2D do odczytu przez procesor
    private Texture2DDesc _stagingDesc;
    private SizeInt32 _poolSize;

    /// <exception cref="COMException">Okna nie da się przechwycić (np. zamknięte w międzyczasie, brak Direct3D 11).</exception>
    public WindowCaptureSession(nint window)
    {
        try
        {
            Check(D3D11CreateDevice(0, DriverTypeHardware, 0, CreateDeviceBgraSupport, 0, 0, SdkVersion,
                out _device, out _, out _context));
            var dxgi = Query(_device, Iid.DxgiDevice);
            try
            {
                Check(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));
                try
                {
                    _winrtDevice = Query(inspectable, Iid.Direct3DDevice);
                }
                finally
                {
                    Release(inspectable);
                }
            }
            finally
            {
                Release(dxgi);
            }

            var interop = Factory("Windows.Graphics.Capture.GraphicsCaptureItem", Iid.GraphicsCaptureItemInterop);
            try
            {
                var iid = Iid.GraphicsCaptureItem;
                nint item;
                Check(((delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)Method(interop, 3))(interop, window, &iid, &item));
                _item = item;
            }
            finally
            {
                Release(interop);
            }

            SizeInt32 size;
            Check(((delegate* unmanaged[Stdcall]<nint, SizeInt32*, int>)Method(_item, 7))(_item, &size));
            _poolSize = size;

            var statics = Factory("Windows.Graphics.Capture.Direct3D11CaptureFramePool", Iid.FramePoolStatics2);
            try
            {
                nint pool;
                Check(((delegate* unmanaged[Stdcall]<nint, nint, int, int, SizeInt32, nint*, int>)Method(statics, 6))(
                    statics, _winrtDevice, Bgra8, Buffers, size, &pool));
                _pool = pool;
            }
            finally
            {
                Release(statics);
            }

            nint session;
            Check(((delegate* unmanaged[Stdcall]<nint, nint, nint*, int>)Method(_pool, 10))(_pool, _item, &session));
            _session = session;
            // Bez kursora operatora i (Windows 11) bez żółtej ramki wokół przechwytywanego okna — gdy system pozwala.
            SetFlag(Iid.CaptureSession2, false);
            SetFlag(Iid.CaptureSession3, false);
            Check(((delegate* unmanaged[Stdcall]<nint, int>)Method(_session, 6))(_session));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Piksele klatki (BGRA, wiersze co <paramref name="pitch"/> bajtów) — ważne tylko w trakcie wywołania.</summary>
    public delegate void FrameReader(byte* pixels, int pitch, int width, int height);

    /// <summary>Następna klatka, jeśli okno coś narysowało od poprzedniej; <c>false</c> = nic nowego.</summary>
    public bool TryRead(FrameReader read)
    {
        nint frame;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(_pool, 7))(_pool, &frame));
        if (frame == 0)
            return false;
        try
        {
            SizeInt32 content;
            Check(((delegate* unmanaged[Stdcall]<nint, SizeInt32*, int>)Method(frame, 8))(frame, &content));
            nint surface;
            Check(((delegate* unmanaged[Stdcall]<nint, nint*, int>)Method(frame, 6))(frame, &surface));
            try
            {
                var texture = Texture(surface);
                try
                {
                    Copy(texture, content, read);
                }
                finally
                {
                    Release(texture);
                }
            }
            finally
            {
                Release(surface);
            }

            // Okno zmieniło wielkość — bufory w nowym rozmiarze od następnej klatki.
            if (content.Width > 0 && content.Height > 0 && (content.Width != _poolSize.Width || content.Height != _poolSize.Height))
            {
                Check(((delegate* unmanaged[Stdcall]<nint, nint, int, int, SizeInt32, int>)Method(_pool, 6))(
                    _pool, _winrtDevice, Bgra8, Buffers, content));
                _poolSize = content;
            }
            return true;
        }
        finally
        {
            Close(frame);
            Release(frame);
        }
    }

    private void Copy(nint texture, SizeInt32 content, FrameReader read)
    {
        Texture2DDesc desc;
        ((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, void>)Method(texture, 10))(texture, &desc);
        EnsureStaging(desc);
        ((delegate* unmanaged[Stdcall]<nint, nint, nint, void>)Method(_context, 47))(_context, _staging, texture);
        MappedSubresource mapped;
        Check(((delegate* unmanaged[Stdcall]<nint, nint, uint, int, uint, MappedSubresource*, int>)Method(_context, 14))(
            _context, _staging, 0, MapRead, 0, &mapped));
        try
        {
            var width = Math.Min(content.Width, (int)desc.Width);
            var height = Math.Min(content.Height, (int)desc.Height);
            if (width > 0 && height > 0)
                read((byte*)mapped.Data, (int)mapped.RowPitch, width, height);
        }
        finally
        {
            ((delegate* unmanaged[Stdcall]<nint, nint, uint, void>)Method(_context, 15))(_context, _staging, 0);
        }
    }

    private void EnsureStaging(Texture2DDesc source)
    {
        if (_staging != 0 && _stagingDesc.Width == source.Width && _stagingDesc.Height == source.Height)
            return;
        Release(_staging);
        _staging = 0;
        var desc = source with
        {
            MipLevels = 1,
            ArraySize = 1,
            SampleCount = 1,
            SampleQuality = 0,
            Usage = UsageStaging,
            BindFlags = 0,
            CpuAccessFlags = CpuAccessRead,
            MiscFlags = 0,
        };
        nint staging;
        Check(((delegate* unmanaged[Stdcall]<nint, Texture2DDesc*, nint, nint*, int>)Method(_device, 5))(_device, &desc, 0, &staging));
        _staging = staging;
        _stagingDesc = desc;
    }

    private static nint Texture(nint surface)
    {
        var access = Query(surface, Iid.DxgiInterfaceAccess);
        try
        {
            var iid = Iid.Texture2D;
            nint texture;
            Check(((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)Method(access, 3))(access, &iid, &texture));
            return texture;
        }
        finally
        {
            Release(access);
        }
    }

    private void SetFlag(Guid iid, bool value)
    {
        if (Marshal.QueryInterface(_session, in iid, out var session) < 0)
            return; // starszy Windows — bez tej opcji
        try
        {
            // put_IsCursorCaptureEnabled / put_IsBorderRequired; odmowa systemu nie przerywa pokazywania
            _ = ((delegate* unmanaged[Stdcall]<nint, byte, int>)Method(session, 7))(session, value ? (byte)1 : (byte)0);
        }
        finally
        {
            Release(session);
        }
    }

    /// <summary>Oddaje bufor klatki do puli (<c>IClosable.Close</c>).</summary>
    private static void Close(nint frame)
    {
        if (Marshal.QueryInterface(frame, in Iid.Closable, out var closable) < 0)
            return;
        _ = ((delegate* unmanaged[Stdcall]<nint, int>)Method(closable, 6))(closable);
        Release(closable);
    }

    public void Dispose()
    {
        foreach (var closable in new[] { _session, _pool })
            if (closable != 0)
                Close(closable);
        Release(_staging);
        Release(_session);
        Release(_pool);
        Release(_item);
        Release(_winrtDevice);
        Release(_context);
        Release(_device);
        _staging = _session = _pool = _item = _winrtDevice = _context = _device = 0;
    }

    private static nint Method(nint instance, int slot) => (*(nint**)instance)[slot];

    private static nint Query(nint instance, Guid iid)
    {
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(instance, in iid, out var result));
        return result;
    }

    private static void Release(nint instance)
    {
        if (instance != 0)
            Marshal.Release(instance);
    }

    private static void Check(int hr) => Marshal.ThrowExceptionForHR(hr);

    private static nint Factory(string runtimeClass, Guid iid)
    {
        Check(WindowsCreateString(runtimeClass, runtimeClass.Length, out var name));
        try
        {
            Check(RoGetActivationFactory(name, in iid, out var factory));
            return factory;
        }
        finally
        {
            WindowsDeleteString(name);
        }
    }

    private const int DriverTypeHardware = 1;
    private const uint CreateDeviceBgraSupport = 0x20;
    private const uint SdkVersion = 7;
    private const uint UsageStaging = 3;
    private const uint CpuAccessRead = 0x20000;
    private const int MapRead = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeInt32
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private record struct Texture2DDesc(
        uint Width,
        uint Height,
        uint MipLevels,
        uint ArraySize,
        int Format,
        uint SampleCount,
        uint SampleQuality,
        uint Usage,
        uint BindFlags,
        uint CpuAccessFlags,
        uint MiscFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct MappedSubresource
    {
        public nint Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    private static class Iid
    {
        public static readonly Guid DxgiDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        public static readonly Guid Direct3DDevice = new("a37624ab-8d5f-4650-9d3e-9eae3d9bc670");
        public static readonly Guid GraphicsCaptureItemInterop = new("3628e81b-3cac-4c60-b7f4-23ce0e0c3356");
        public static readonly Guid GraphicsCaptureItem = new("79c3f95b-31f7-4ec2-a464-632ef5d30760");
        public static readonly Guid FramePoolStatics2 = new("589b103f-6bbc-5df5-a991-02e28b3b66d5");
        public static readonly Guid CaptureSession2 = new("2c39ae40-7d2e-5044-804e-8b6799d4cf9e");
        public static readonly Guid CaptureSession3 = new("f2cdd966-22ae-5ea1-9596-3a289344c3be");
        public static readonly Guid DxgiInterfaceAccess = new("a9b3d012-3df2-4ee3-b8d1-8695f457d3c1");
        public static readonly Guid Texture2D = new("6f15aaf2-d208-4e89-9ab4-489535d34f9c");
        public static readonly Guid Closable = new("30d5a829-7fa4-4026-83bb-d75bae4ea99e");
    }

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        nint adapter, int driverType, nint software, uint flags, nint featureLevels, uint featureLevelCount, uint sdkVersion,
        out nint device, out int featureLevel, out nint context);

    [DllImport("d3d11.dll")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    [DllImport("combase.dll", CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint hstring);

    [DllImport("combase.dll")]
    private static extern int WindowsDeleteString(nint hstring);

    [DllImport("combase.dll")]
    private static extern int RoGetActivationFactory(nint activatableClassId, in Guid iid, out nint factory);
}
