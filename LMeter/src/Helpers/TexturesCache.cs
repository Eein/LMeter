using Dalamud.Interface;
using Dalamud.Interface.Internal;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Lumina.Data.Files.TexFile;


namespace LMeter.Helpers;

public class TexturesCache : IDisposable
{
    private readonly Dictionary<string, Tuple<IDalamudTextureWrap, float>> _textureCache = new ();
    private readonly ICallGateSubscriber<string, string> _penumbraPathResolver;
    private readonly IDataManager _dataManager;
    private readonly UiBuilder _uiBuilder;

    public TexturesCache(IDataManager dataManager, DalamudPluginInterface pluginInterface)
    {
        _penumbraPathResolver = pluginInterface.GetIpcSubscriber<string, string>("Penumbra.ResolveDefaultPath");
        _dataManager = dataManager;
        _uiBuilder = pluginInterface.UiBuilder;
    }

    public IDalamudTextureWrap? GetTextureFromIconId
    (
        uint iconId,
        uint stackCount = 0,
        bool hdIcon = true,
        bool greyScale = false,
        float opacity = 1f
    )
    {
        var key = $"{iconId}{(greyScale ? "_g" : string.Empty)}{(opacity != 1f ? "_t" : string.Empty)}";
        if (_textureCache.TryGetValue(key, out var tuple))
        {
            var (texture, cachedOpacity) = tuple;
            if (cachedOpacity == opacity) return texture;

            _textureCache.Remove(key);
        }

        var newTexture = this.LoadTexture(iconId + stackCount, hdIcon, greyScale, opacity);
        if (newTexture == null) return null;

        _textureCache.Add(key, new Tuple<IDalamudTextureWrap, float>(newTexture, opacity));
        return newTexture;
    }

    private IDalamudTextureWrap? LoadTexture(uint id, bool hdIcon, bool greyScale, float opacity = 1f)
    {
        var path = $"ui/icon/{id / 1000 * 1000:000000}/{id:000000}{(hdIcon ? "_hr1" : string.Empty)}.tex";

        try
        {
            var resolvedPath = _penumbraPathResolver.InvokeFunc(path);

            if (!string.IsNullOrEmpty(resolvedPath) && !resolvedPath.Equals(path))
            {
                return this.LoadPenumbraTexture(resolvedPath);
            }
        }
        catch { }

        try
        {
            var iconFile = _dataManager.GetFile<TexFile>(path);
            if (iconFile is null)
            {
                return null;
            }

            return GetTextureWrap(iconFile, greyScale, opacity);
        }
        catch (Exception ex)
        {
            LMeterLogger.Logger?.Warning(ex.ToString());
        }

        return null;
    }

    private IDalamudTextureWrap? LoadPenumbraTexture(string path)
    {
        try
        {
            var fileStream = new FileStream(path, FileMode.Open);
            var reader = new BinaryReader(fileStream);

            // read header
            int headerSize = Unsafe.SizeOf<TexHeader>();
            var headerData = reader.ReadBytes(headerSize).AsSpan();
            var header = MemoryMarshal.Read<TexHeader>(headerData);

            // read image data
            var rawImageData = reader.ReadBytes((int)fileStream.Length - headerSize);
            var imageData = new byte[header.Width * header.Height * 4];

            if (!ProcessTexture(header.Format, rawImageData, imageData, header.Width, header.Height))
                return null;

            return _uiBuilder.LoadImageRaw(GetRgbaImageData(imageData), header.Width, header.Height, 4);
        }
        catch (Exception ex)
        {
            LMeterLogger.Logger?.Error($"Error loading texture: {path} {ex}");
        }

        return null;
    }

    private static byte[] GetRgbaImageData(byte[] imageData)
    {
        var dst = new byte[imageData.Length];

        for (var i = 0; i < dst.Length; i += 4)
        {
            dst[i] = imageData[i + 2];
            dst[i + 1] = imageData[i + 1];
            dst[i + 2] = imageData[i];
            dst[i + 3] = imageData[i + 3];
        }

        return dst;
    }

    private static bool ProcessTexture(TextureFormat format, byte[] src, byte[] dst, int width, int height)
    {
        switch (format)
        {
            case TextureFormat.DXT1:
            {
                Decompress(SquishOptions.DXT1, src, dst, width, height);
                return true;
            }
            case TextureFormat.DXT3:
            {
                Decompress(SquishOptions.DXT3, src, dst, width, height);
                return true;
            }
            case TextureFormat.DXT5:
            {
                Decompress(SquishOptions.DXT5, src, dst, width, height);
                return true;
            }
            case TextureFormat.B5G5R5A1:
            {
                ProcessB5G5R5A1(src, dst, width, height);
                return true;
            }
            case TextureFormat.B4G4R4A4:
            {
                ProcessB4G4R4A4(src, dst, width, height);
                return true;
            }
            case TextureFormat.L8:
            {
                ProcessR3G3B2(src, dst, width, height);
                return true;
            }
            case TextureFormat.B8G8R8A8:
            {
                Array.Copy(src, dst, dst.Length);
                return true;
            }
        }

        return false;
    }

    private static void Decompress(SquishOptions squishOptions, byte[] src, byte[] dst, int width, int height) =>
        Array.Copy(Squish.DecompressImage(src, width, height, squishOptions), dst, dst.Length);

    private static void ProcessB5G5R5A1(Span<byte> src, byte[] dst, int width, int height)
    {
        for (var i = 0; (i + 2) <= 2 * width * height; i += 2)
        {
            var v = BitConverter.ToUInt16(src.Slice(i, sizeof(UInt16)).ToArray(), 0);

            var a = (uint) (v & 0x8000);
            var r = (uint) (v & 0x7C00);
            var g = (uint) (v & 0x03E0);
            var b = (uint) (v & 0x001F);

            var rgb = ((r << 9) | (g << 6) | (b << 3));
            var argbValue = (a * 0x1FE00 | rgb | ((rgb >> 5) & 0x070707));

            for (var j = 0; j < 4; ++j)
            {
                dst[i * 2 + j] = (byte) (argbValue >> (8 * j));
            }
        }
    }

    private static void ProcessB4G4R4A4(Span<byte> src, byte[] dst, int width, int height)
    {
        for (var i = 0; (i + 2) <= 2 * width * height; i += 2)
        {
            var v = BitConverter.ToUInt16(src.Slice(i, sizeof(UInt16)).ToArray(), 0);

            for (var j = 0; j < 4; ++j)
            {
                dst[i * 2 + j] = (byte)(((v >> (4 * j)) & 0x0F) << 4);
            }
        }
    }

    private static void ProcessR3G3B2(Span<byte> src, byte[] dst, int width, int height)
    {
        for (var i = 0; i < width * height; ++i)
        {
            var r = (uint) (src[i] & 0xE0);
            var g = (uint) (src[i] & 0x1C);
            var b = (uint) (src[i] & 0x03);

            dst[i * 4 + 0] = (byte) (b | (b << 2) | (b << 4) | (b << 6));
            dst[i * 4 + 1] = (byte) (g | (g << 3) | (g << 6));
            dst[i * 4 + 2] = (byte) (r | (r << 3) | (r << 6));
            dst[i * 4 + 3] = 0xFF;
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var tuple in _textureCache.Values)
            {
                tuple.Item1.Dispose();
            }

            _textureCache.Clear();
        }
    }

    private IDalamudTextureWrap GetTextureWrap(TexFile tex, bool greyScale, float opacity)
    {
        var bytes = tex.GetRgbaImageData();

        if (greyScale || opacity < 1f) ConvertBytes(ref bytes, greyScale, opacity);

        return _uiBuilder.LoadImageRaw(bytes, tex.Header.Width, tex.Header.Height, 4);
    }

    private static void ConvertBytes(ref byte[] bytes, bool greyScale, float opacity)
    {
        if (bytes.Length % 4 != 0 || opacity > 1 || opacity < 0) return;

        for (var i = 0; i < bytes.Length; i += 4)
        {
            if (greyScale)
            {
                int r = bytes[i] >> 2;
                int g = bytes[i + 1] >> 1;
                int b = bytes[i + 2] >> 3;
                byte lum = (byte) (r + g + b);

                bytes[i] = lum;
                bytes[i + 1] = lum;
                bytes[i + 2] = lum;
            }

            if (opacity != 1)
            {
                bytes[i + 3] = (byte) (bytes[i + 3] * opacity);
            }
        }
    }
}
