using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace PirateSlop.Customization
{
    public static class SailImageLoader
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        class OpenFileName
        {
            public int structSize = 0;
            public IntPtr dlgOwner = IntPtr.Zero;
            public IntPtr instance = IntPtr.Zero;
            public string filter = null;
            public string customFilter = null;
            public int maxCustFilter = 0;
            public int filterIndex = 0;
            public string file = null;
            public int maxFile = 0;
            public string fileTitle = null;
            public int maxFileTitle = 0;
            public string initialDir = null;
            public string title = null;
            public int flags = 0;
            public short fileOffset = 0;
            public short fileExtension = 0;
            public string defExt = null;
            public IntPtr custData = IntPtr.Zero;
            public IntPtr hook = IntPtr.Zero;
            public string templateName = null;
            public IntPtr reservedPtr = IntPtr.Zero;
            public int reservedInt = 0;
            public int flagsEx = 0;
        }

        [DllImport("comdlg32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint RegisterClipboardFormat(string lpszFormat);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, uint cch);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UIntPtr GlobalSize(IntPtr hMem);

        const uint CF_DIB = 8;
        const uint CF_HDROP = 15;

        public const int MaxImageDimension = 512;

        static readonly Dictionary<string, Texture2D> memoryTextureCache = new Dictionary<string, Texture2D>();

        public static string CacheDirectory
        {
            get
            {
                string dir = Path.Combine(Application.persistentDataPath, "SailCustomization", "Cache");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string PromptOpenFile()
        {
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.filter = "Изображения (*.png;*.jpg;*.jpeg)\0*.png;*.jpg;*.jpeg\0Все файлы (*.*)\0*.*\0\0";
            ofn.file = new string(new char[512]);
            ofn.maxFile = ofn.file.Length;
            ofn.fileTitle = new string(new char[256]);
            ofn.maxFileTitle = ofn.fileTitle.Length;
            ofn.title = "Выберите изображение";
            ofn.flags = 0x00080000 | 0x00001000 | 0x00000800;

            if (GetOpenFileName(ofn))
            {
                return ofn.file.TrimEnd('\0');
            }
            return null;
        }

        public static Texture2D LoadImageFromFile(string path, int maxDimension = MaxImageDimension)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(bytes))
                {
                    var scaled = ScaleTexture(tex, maxDimension);
                    CacheTextureIfValid(scaled);
                    return scaled;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SailImageLoader] Error loading image from {path}: {ex.Message}");
            }
            return null;
        }

        public static Texture2D LoadImageFromClipboard(int maxDimension = MaxImageDimension)
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;

            try
            {
                uint pngFormat = RegisterClipboardFormat("PNG");
                if (pngFormat != 0 && IsClipboardFormatAvailable(pngFormat))
                {
                    IntPtr handle = GetClipboardData(pngFormat);
                    if (handle != IntPtr.Zero)
                    {
                        IntPtr ptr = GlobalLock(handle);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                int size = (int)GlobalSize(handle).ToUInt32();
                                byte[] bytes = new byte[size];
                                Marshal.Copy(ptr, bytes, 0, size);
                                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                                if (tex.LoadImage(bytes))
                                {
                                    var scaled = ScaleTexture(tex, maxDimension);
                                    CacheTextureIfValid(scaled);
                                    return scaled;
                                }
                            }
                            finally
                            {
                                GlobalUnlock(handle);
                            }
                        }
                    }
                }

                if (IsClipboardFormatAvailable(CF_HDROP))
                {
                    IntPtr handle = GetClipboardData(CF_HDROP);
                    if (handle != IntPtr.Zero)
                    {
                        var sb = new StringBuilder(512);
                        if (DragQueryFile(handle, 0, sb, (uint)sb.Capacity) > 0)
                        {
                            string path = sb.ToString();
                            string ext = Path.GetExtension(path).ToLowerInvariant();
                            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                            {
                                return LoadImageFromFile(path, maxDimension);
                            }
                        }
                    }
                }

                if (IsClipboardFormatAvailable(CF_DIB))
                {
                    IntPtr handle = GetClipboardData(CF_DIB);
                    if (handle != IntPtr.Zero)
                    {
                        IntPtr ptr = GlobalLock(handle);
                        if (ptr != IntPtr.Zero)
                        {
                            try
                            {
                                int biSize = Marshal.ReadInt32(ptr, 0);
                                int biWidth = Marshal.ReadInt32(ptr, 4);
                                int biHeight = Marshal.ReadInt32(ptr, 8);
                                short biBitCount = Marshal.ReadInt16(ptr, 14);
                                int biCompression = Marshal.ReadInt32(ptr, 16);

                                bool topDown = biHeight < 0;
                                int width = biWidth;
                                int height = Math.Abs(biHeight);

                                if ((biBitCount == 24 || biBitCount == 32) && (biCompression == 0 || biCompression == 3))
                                {
                                    int pixelOffset = biSize;
                                    if (biCompression == 3) pixelOffset += 12;

                                    IntPtr pixelPtr = new IntPtr(ptr.ToInt64() + pixelOffset);
                                    int rowStride = ((width * biBitCount + 31) / 32) * 4;

                                    Color32[] pixels = new Color32[width * height];
                                    byte[] rowBuffer = new byte[rowStride];

                                    for (int y = 0; y < height; y++)
                                    {
                                        int targetY = topDown ? (height - 1 - y) : y;
                                        Marshal.Copy(new IntPtr(pixelPtr.ToInt64() + y * rowStride), rowBuffer, 0, rowStride);

                                        int byteIdx = 0;
                                        for (int x = 0; x < width; x++)
                                        {
                                            byte b = rowBuffer[byteIdx++];
                                            byte g = rowBuffer[byteIdx++];
                                            byte r = rowBuffer[byteIdx++];
                                            byte a = (biBitCount == 32) ? rowBuffer[byteIdx++] : (byte)255;
                                            pixels[targetY * width + x] = new Color32(r, g, b, a);
                                        }
                                    }

                                    var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                                    tex.SetPixels32(pixels);
                                    tex.Apply();
                                    var scaled = ScaleTexture(tex, maxDimension);
                                    CacheTextureIfValid(scaled);
                                    return scaled;
                                }
                            }
                            finally
                            {
                                GlobalUnlock(handle);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SailImageLoader] Error reading clipboard: {ex.Message}");
            }
            finally
            {
                CloseClipboard();
            }

            return null;
        }

        public static Texture2D ScaleTexture(Texture2D source, int maxDimension = MaxImageDimension)
        {
            if (source == null) return null;
            int w = source.width;
            int h = source.height;
            if (w <= maxDimension && h <= maxDimension) return source;

            float ratio = Mathf.Min((float)maxDimension / w, (float)maxDimension / h);
            int newW = Mathf.Max(1, Mathf.RoundToInt(w * ratio));
            int newH = Mathf.Max(1, Mathf.RoundToInt(h * ratio));

            RenderTexture rt = RenderTexture.GetTemporary(newW, newH, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Graphics.Blit(source, rt);
            var result = new Texture2D(newW, newH, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, newW, newH), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        public static byte[] CompressImageToBytes(Texture2D texture)
        {
            if (texture == null) return null;

            bool hasAlpha = false;
            try
            {
                Color32[] pixels = texture.GetPixels32();
                int stride = Mathf.Max(1, pixels.Length / 100);
                for (int i = 0; i < pixels.Length; i += stride)
                {
                    if (pixels[i].a < 250)
                    {
                        hasAlpha = true;
                        break;
                    }
                }
            }
            catch
            {
                hasAlpha = true;
            }

            if (!hasAlpha)
            {
                return texture.EncodeToJPG(80);
            }
            return texture.EncodeToPNG();
        }

        public static string ComputeHash(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            using (var sha = SHA256.Create())
            {
                byte[] hashBytes = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hashBytes.Length * 2);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static void CacheTexture(string hash, byte[] bytes, Texture2D texture)
        {
            if (string.IsNullOrEmpty(hash)) return;

            if (texture != null && !memoryTextureCache.ContainsKey(hash))
            {
                memoryTextureCache[hash] = texture;
            }

            if (bytes != null && bytes.Length > 0)
            {
                string path = Path.Combine(CacheDirectory, $"{hash}.dat");
                if (!File.Exists(path))
                {
                    try { File.WriteAllBytes(path, bytes); } catch { }
                }
            }
        }

        public static void CacheTextureIfValid(Texture2D texture)
        {
            if (texture == null) return;
            byte[] bytes = CompressImageToBytes(texture);
            if (bytes != null)
            {
                string hash = ComputeHash(bytes);
                CacheTexture(hash, bytes, texture);
            }
        }

        public static bool TryGetFromCache(string hash, out Texture2D texture)
        {
            texture = null;
            if (string.IsNullOrEmpty(hash)) return false;

            if (memoryTextureCache.TryGetValue(hash, out texture) && texture != null)
            {
                return true;
            }

            string path = Path.Combine(CacheDirectory, $"{hash}.dat");
            if (File.Exists(path))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        memoryTextureCache[hash] = tex;
                        texture = tex;
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        public static bool TryGetBytesFromCache(string hash, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(hash)) return false;

            string path = Path.Combine(CacheDirectory, $"{hash}.dat");
            if (File.Exists(path))
            {
                try
                {
                    bytes = File.ReadAllBytes(path);
                    return true;
                }
                catch { }
            }
            return false;
        }
    }
}
