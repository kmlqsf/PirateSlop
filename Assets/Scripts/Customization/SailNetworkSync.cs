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
        int customizationClientId = -1;
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
            var netShip = GetComponent<PirateSlop.Networking.NetworkShip>();
            PirateSlop.Networking.NetworkPlayer localPlayer = null;
            float timeout = Time.unscaledTime + 10f;
            while (localPlayer == null && Time.unscaledTime < timeout)
            {
                foreach (var p in PirateSlop.Networking.NetworkPlayer.Active)
                {
                    if (p != null && p.IsOwner && !p.IsBot.Value)
                    {
                        localPlayer = p;
                        break;
                    }
                }
                if (localPlayer == null) yield return null;
            }
            if (!IsClientInitialized) yield break;
            RequestShipCustomizationServerRpc();
            if (netShip != null && localPlayer != null && localPlayer.TeamId.Value == netShip.TeamId.Value)
            {
                if (SailCustomizationStorage.Load(out var savedData, out var savedTextures))
                {
                    if (customizer != null)
                    {
                        customizer.ApplyCustomization(savedData, savedTextures, true);
                    }
                }
            }
        }

        public void UploadCustomization(ShipCustomizationData data, Texture2D[,] textures)
        {
            if (!IsClientInitialized && !IsServerInitialized) return;

            data.EnsureCapacity();

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
                        }
                    }
                }
            }
            UploadSailConfigServerRpc(JsonUtility.ToJson(data));
            for (int p = 0; p < SailCustomizer.TotalParts; p++)
                for (int l = 0; l < SailCustomizer.TotalLayers; l++)
                    if (data.sails[p].GetHasDecal(l) && SailImageLoader.TryGetBytesFromCache(data.sails[p].GetImageHash(l), out var bytes))
                        StartCoroutine(UploadImageRoutine(p, l, data.sails[p].GetImageHash(l), bytes));
        }

        bool CanCustomize(NetworkConnection sender)
        {
            if (sender == null) return false;
            if (customizationClientId >= 0 && customizationClientId != sender.ClientId) return false;
            var ship = GetComponent<PirateSlop.Networking.NetworkShip>();
            foreach (var player in PirateSlop.Networking.NetworkPlayer.Active)
                if (player != null && !player.IsBot.Value && player.Owner == sender && ship != null && player.TeamId.Value == ship.TeamId.Value)
                {
                    foreach (var teammate in PirateSlop.Networking.NetworkPlayer.Active)
                        if (teammate != null && !teammate.IsBot.Value && teammate.TeamId.Value == player.TeamId.Value && teammate.ParticipantId.Value < player.ParticipantId.Value)
                            return false;
                    return true;
                }
            return false;
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
            SendCustomization(sender);
        }

        void SendCustomization(NetworkConnection sender)
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
            if (!CanCustomize(sender))
            {
                if (sender != null) SendCustomization(sender);
                return;
            }
            ShipCustomizationData data;
            try { data = JsonUtility.FromJson<ShipCustomizationData>(configJson); }
            catch { return; }
            if (data == null) return;
            data.EnsureCapacity();
            customizationClientId = sender.ClientId;
            serverCachedConfigJson = JsonUtility.ToJson(data);
            serverCachedImages.Clear();
            serverAssemblyBuffers.Clear();
            ApplyParsedConfig(serverCachedConfigJson);
            SyncSailConfigObserversRpc(serverCachedConfigJson);
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
                        else textures[p, l] = null;
                    }
                }

                customizer.ApplyCustomization(data, textures, false);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        void StartImageUploadServerRpc(int slot, string hash, int totalBytes, int chunkCount, NetworkConnection sender = null)
        {
            if (!CanCustomize(sender) || !ValidSlot(slot) || totalBytes <= 0 || totalBytes > 1048576 || chunkCount != Mathf.CeilToInt((float)totalBytes / ChunkSize)) return;
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
            if (!CanCustomize(sender) || chunkData == null || chunkData.Length > ChunkSize) return;
            if (!serverAssemblyBuffers.TryGetValue(slot, out var buf)) return;
            if (chunkIndex < 0 || chunkIndex >= buf.ChunkCount) return;
            if (chunkData.Length != Mathf.Min(ChunkSize, buf.TotalBytes - chunkIndex * ChunkSize)) return;

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
                ApplyImage(slot, buf.Hash, full);
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
            if (!ValidSlot(slot)) return;
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
                ApplyImage(slot, buf.Hash, fullBytes);
                clientAssemblyBuffers.Remove(slot);
            }
        }

        static bool ValidSlot(int slot)
        {
            UnpackSlot(slot, out int p, out int l);
            return p >= 0 && p < SailCustomizer.TotalParts && l < SailCustomizer.TotalLayers;
        }

        void ApplyImage(int slot, string hash, byte[] bytes)
        {
            if (!ValidSlot(slot) || customizer == null) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes)) { Destroy(tex); return; }
            tex.wrapMode = TextureWrapMode.Clamp;
            UnpackSlot(slot, out int p, out int l);
            customizer.CurrentTextures[p, l] = tex;
            customizer.CurrentData.sails[p].SetHasDecal(l, true);
            customizer.CurrentData.sails[p].SetImageHash(l, hash);
            SailImageLoader.CacheTexture(hash, bytes, tex);
            customizer.UpdateVisuals();
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
