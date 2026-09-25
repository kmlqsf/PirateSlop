using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace PirateSlop.Customization
{
    [DefaultExecutionOrder(-4)]
    [RequireComponent(typeof(SailCustomizer))]
    public class SailNetworkSync : NetworkBehaviour
    {
        const int ChunkSize = 2048;

        SailCustomizer customizer;
        string serverCachedConfigJson = "";
        readonly Dictionary<int, byte[]> serverCachedImages = new Dictionary<int, byte[]>();
        readonly Dictionary<string, byte[]> serverCachedImagesByHash = new Dictionary<string, byte[]>();

        class ChunkAssemblyBuffer
        {
            public int TotalBytes;
            public int ChunkCount;
            public byte[][] Chunks;
            public int ReceivedCount;
            public string Hash;
        }
        readonly Dictionary<int, ChunkAssemblyBuffer> serverAssemblyBuffers = new Dictionary<int, ChunkAssemblyBuffer>();
        readonly Dictionary<int, ChunkAssemblyBuffer> clientAssemblyBuffers = new Dictionary<int, ChunkAssemblyBuffer>();

        void Awake()
        {
            customizer = GetComponent<SailCustomizer>();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (customizer == null) customizer = GetComponent<SailCustomizer>();
            if (customizer != null) customizer.InitializeSails();
            StartCoroutine(CheckInitialApplyRoutine());
        }

        IEnumerator CheckInitialApplyRoutine()
        {
            yield return new WaitForSeconds(0.2f);

            var netShip = GetComponent<PirateSlop.Networking.NetworkShip>();
            bool isOurShip = false;

            if (netShip != null)
            {
                foreach (var p in PirateSlop.Networking.NetworkPlayer.Active)
                {
                    if (p != null && p.IsOwner && p.TeamId.Value == netShip.TeamId.Value)
                    {
                        isOurShip = true;
                        break;
                    }
                }
            }

            if (isOurShip || IsServerInitialized)
            {
                if (SailCustomizationStorage.Load(out var savedData, out var savedTextures))
                {
                    if (customizer != null)
                    {
                        customizer.ApplyCustomization(savedData, savedTextures, true);
                    }
                }
            }
            else if (IsClientInitialized && !IsServerInitialized)
            {
                RequestShipCustomizationServerRpc();
            }
        }

        public void UploadCustomization(ShipCustomizationData data, Texture2D[,] textures)
        {
            if (!IsClientInitialized && !IsServerInitialized) return;

            string json = JsonUtility.ToJson(data);
            UploadSailConfigServerRpc(json);

            for (int p = 0; p < SailCustomizer.TotalParts; p++)
            {
                for (int l = 0; l < SailCustomizer.TotalLayers; l++)
                {
                    if (data.sails[p].GetHasDecal(l) && textures != null && p < textures.GetLength(0) && l < textures.GetLength(1) && textures[p, l] != null)
                    {
                        byte[] bytes = SailImageLoader.CompressImageToBytes(textures[p, l]);
                        if (bytes != null && bytes.Length > 0)
                        {
                            string hash = SailImageLoader.ComputeHash(bytes);
                            data.sails[p].SetImageHash(l, hash);
                            SailImageLoader.CacheTexture(hash, bytes, textures[p, l]);
                            StartCoroutine(UploadImageRoutine(p, l, hash, bytes));
                        }
                    }
                }
            }
        }

        static int PackSlot(int partIndex, int layerIndex) => (partIndex << 2) | (layerIndex & 3);
        static void UnpackSlot(int slot, out int partIndex, out int layerIndex)
        {
            partIndex = slot >> 2;
            layerIndex = slot & 3;
        }

        IEnumerator UploadImageRoutine(int partIndex, int layerIndex, string hash, byte[] bytes)
        {
            int slot = PackSlot(partIndex, layerIndex);
            int totalBytes = bytes.Length;
            int chunkCount = Mathf.CeilToInt((float)totalBytes / ChunkSize);

            StartImageUploadServerRpc(slot, hash, totalBytes, chunkCount);

            for (int chunkIdx = 0; chunkIdx < chunkCount; chunkIdx++)
            {
                int offset = chunkIdx * ChunkSize;
                int length = Mathf.Min(ChunkSize, totalBytes - offset);
                byte[] chunk = new byte[length];
                System.Buffer.BlockCopy(bytes, offset, chunk, 0, length);

                UploadImageChunkServerRpc(slot, chunkIdx, chunk);
                yield return null;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestShipCustomizationServerRpc(NetworkConnection sender = null)
        {
            if (sender == null) return;

            if (!string.IsNullOrEmpty(serverCachedConfigJson))
            {
                SyncSailConfigTargetRpc(sender, serverCachedConfigJson);

                foreach (var kvp in serverCachedImages)
                {
                    int slot = kvp.Key;
                    byte[] bytes = kvp.Value;
                    if (bytes != null && bytes.Length > 0 && customizer != null)
                    {
                        UnpackSlot(slot, out int p, out int l);
                        string hash = customizer.CurrentData.sails[p].GetImageHash(l);
                        StartCoroutine(StreamImageToClientRoutine(sender, slot, hash, bytes));
                    }
                }
            }
        }

        IEnumerator StreamImageToClientRoutine(NetworkConnection target, int slot, string hash, byte[] bytes)
        {
            int totalBytes = bytes.Length;
            int chunkCount = Mathf.CeilToInt((float)totalBytes / ChunkSize);

            StartImageDownloadTargetRpc(target, slot, hash, totalBytes, chunkCount);

            for (int chunkIdx = 0; chunkIdx < chunkCount; chunkIdx++)
            {
                int offset = chunkIdx * ChunkSize;
                int length = Mathf.Min(ChunkSize, totalBytes - offset);
                byte[] chunk = new byte[length];
                System.Buffer.BlockCopy(bytes, offset, chunk, 0, length);

                DownloadImageChunkTargetRpc(target, slot, chunkIdx, chunk);
                yield return null;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void UploadSailConfigServerRpc(string configJson, NetworkConnection sender = null)
        {
            serverCachedConfigJson = configJson;
            SyncSailConfigObserversRpc(configJson);
        }

        [ObserversRpc(RunLocally = true)]
        void SyncSailConfigObserversRpc(string configJson)
        {
            ApplyParsedConfig(configJson);
        }

        [TargetRpc]
        void SyncSailConfigTargetRpc(NetworkConnection target, string configJson)
        {
            ApplyParsedConfig(configJson);
        }

        void ApplyParsedConfig(string configJson)
        {
            if (string.IsNullOrEmpty(configJson) || customizer == null) return;
            var data = JsonUtility.FromJson<ShipCustomizationData>(configJson);
            if (data != null && data.sails != null)
            {
                data.EnsureCapacity();

                var textures = customizer.CurrentTextures;
                for (int p = 0; p < SailCustomizer.TotalParts; p++)
                {
                    for (int l = 0; l < SailCustomizer.TotalLayers; l++)
                    {
                        string hash = data.sails[p].GetImageHash(l);
                        if (!string.IsNullOrEmpty(hash) && SailImageLoader.TryGetFromCache(hash, out var tex))
                        {
                            textures[p, l] = tex;
                        }
                    }
                }

                customizer.ApplyCustomization(data, textures, false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void StartImageUploadServerRpc(int slot, string hash, int totalBytes, int chunkCount, NetworkConnection sender = null)
        {
            serverAssemblyBuffers[slot] = new ChunkAssemblyBuffer
            {
                TotalBytes = totalBytes,
                ChunkCount = chunkCount,
                Chunks = new byte[chunkCount][],
                ReceivedCount = 0,
                Hash = hash
            };

            StartImageDownloadObserversRpc(slot, hash, totalBytes, chunkCount);
        }

        [ServerRpc(RequireOwnership = false)]
        void UploadImageChunkServerRpc(int slot, int chunkIndex, byte[] chunkData, NetworkConnection sender = null)
        {
            if (!serverAssemblyBuffers.TryGetValue(slot, out var buf)) return;
            if (chunkIndex < 0 || chunkIndex >= buf.ChunkCount) return;

            if (buf.Chunks[chunkIndex] == null)
            {
                buf.Chunks[chunkIndex] = chunkData;
                buf.ReceivedCount++;
            }

            DownloadImageChunkObserversRpc(slot, chunkIndex, chunkData);

            if (buf.ReceivedCount >= buf.ChunkCount)
            {
                byte[] full = ReassembleBuffer(buf);
                serverCachedImages[slot] = full;
                if (!string.IsNullOrEmpty(buf.Hash))
                {
                    serverCachedImagesByHash[buf.Hash] = full;
                }
                serverAssemblyBuffers.Remove(slot);
            }
        }

        [ObserversRpc(RunLocally = false)]
        void StartImageDownloadObserversRpc(int slot, string hash, int totalBytes, int chunkCount)
        {
            HandleStartDownload(slot, hash, totalBytes, chunkCount);
        }

        [TargetRpc]
        void StartImageDownloadTargetRpc(NetworkConnection target, int slot, string hash, int totalBytes, int chunkCount)
        {
            HandleStartDownload(slot, hash, totalBytes, chunkCount);
        }

        void HandleStartDownload(int slot, string hash, int totalBytes, int chunkCount)
        {
            if (!string.IsNullOrEmpty(hash) && SailImageLoader.TryGetFromCache(hash, out var cachedTex) && customizer != null)
            {
                UnpackSlot(slot, out int p, out int l);
                customizer.CurrentTextures[p, l] = cachedTex;
                customizer.CurrentData.sails[p].SetHasDecal(l, true);
                customizer.CurrentData.sails[p].SetImageHash(l, hash);
                customizer.UpdateVisuals();
                return;
            }

            clientAssemblyBuffers[slot] = new ChunkAssemblyBuffer
            {
                TotalBytes = totalBytes,
                ChunkCount = chunkCount,
                Chunks = new byte[chunkCount][],
                ReceivedCount = 0,
                Hash = hash
            };
        }

        [ObserversRpc(RunLocally = false)]
        void DownloadImageChunkObserversRpc(int slot, int chunkIndex, byte[] chunkData)
        {
            ProcessClientChunk(slot, chunkIndex, chunkData);
        }

        [TargetRpc]
        void DownloadImageChunkTargetRpc(NetworkConnection target, int slot, int chunkIndex, byte[] chunkData)
        {
            ProcessClientChunk(slot, chunkIndex, chunkData);
        }

        void ProcessClientChunk(int slot, int chunkIndex, byte[] chunkData)
        {
            if (!clientAssemblyBuffers.TryGetValue(slot, out var buf)) return;
            if (chunkIndex < 0 || chunkIndex >= buf.ChunkCount) return;

            if (buf.Chunks[chunkIndex] == null)
            {
                buf.Chunks[chunkIndex] = chunkData;
                buf.ReceivedCount++;
            }

            if (buf.ReceivedCount >= buf.ChunkCount)
            {
                byte[] fullBytes = ReassembleBuffer(buf);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (tex.LoadImage(fullBytes) && customizer != null)
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    UnpackSlot(slot, out int p, out int l);
                    customizer.CurrentTextures[p, l] = tex;
                    customizer.CurrentData.sails[p].SetHasDecal(l, true);
                    customizer.CurrentData.sails[p].SetImageHash(l, buf.Hash);
                    SailImageLoader.CacheTexture(buf.Hash, fullBytes, tex);
                    customizer.UpdateVisuals();
                }
                clientAssemblyBuffers.Remove(slot);
            }
        }

        byte[] ReassembleBuffer(ChunkAssemblyBuffer buf)
        {
            byte[] full = new byte[buf.TotalBytes];
            int offset = 0;
            for (int i = 0; i < buf.ChunkCount; i++)
            {
                if (buf.Chunks[i] != null)
                {
                    System.Buffer.BlockCopy(buf.Chunks[i], 0, full, offset, buf.Chunks[i].Length);
                    offset += buf.Chunks[i].Length;
                }
            }
            return full;
        }
    }
}
