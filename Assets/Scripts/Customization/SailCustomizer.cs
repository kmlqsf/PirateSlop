using System.Collections.Generic;
using UnityEngine;

namespace PirateSlop.Customization
{
    [DefaultExecutionOrder(-5)]
    public class SailCustomizer : MonoBehaviour
    {
        public const int TotalParts = ShipCustomizationData.TotalParts;
        public const int TotalLayers = ShipCustomizationData.TotalLayers; // 2

        public static readonly Vector4[] PartUVBoundsFront = new Vector4[]
        {
            new Vector4(0.5888f, 0.0658f, 0.4014f, 0.2011f),  // 0: F2_SailBack (Front)
            new Vector4(0.2542f, 0.4021f, 0.3275f, 0.2832f),  // 1: F2_SailFront
            new Vector4(0.6157f, 0.6629f, 0.3708f, 0.3220f),  // 2: F2_SailMid1
            new Vector4(0.1827f, 0.2304f, 0.1534f, 0.1435f),  // 3: F2_SailMid2
            new Vector4(0.6511f, 0.0094f, 0.1135f, 0.0598f),  // 4: F2_MainFlag
            new Vector4(0.1598f, 0.7221f, 0.0391f, 0.1232f),  // 5: F2_Flag1
            new Vector4(0.2011f, 0.7217f, 0.0391f, 0.1232f)   // 6: F2_Flag2
        };

        public static readonly Vector4[] PartUVBoundsBack = new Vector4[]
        {
            new Vector4(0.9237f, 0.2659f, -0.4073f, -0.1820f), // 0: F2_SailBack (Back)
            new Vector4(0.2568f, 0.7023f, 0.3275f, 0.2832f),   // 1: F2_SailFront
            new Vector4(0.6167f, 0.3262f, 0.3708f, 0.3220f),   // 2: F2_SailMid1
            new Vector4(0.3573f, 0.2298f, 0.1534f, 0.1435f),   // 3: F2_SailMid2
            new Vector4(0.7838f, 0.0095f, 0.1135f, 0.0598f),   // 4: F2_MainFlag
            new Vector4(0.1999f, 0.8587f, 0.0391f, 0.1232f),   // 5: F2_Flag1
            new Vector4(0.1604f, 0.8615f, 0.0391f, 0.1232f)    // 6: F2_Flag2
        };

        public static readonly string[] PartDisplayNames = new string[]
        {
            "Бизань (Задний парус)",
            "Фок (Передний парус)",
            "Грот 1 (Главный нижний)",
            "Грот 2 (Главный верхний)",
            "Веселый Роджер (Главный флаг)",
            "Вымпел 1 (Фок-мачта)",
            "Архивный вымпел",
            "Фор-марсель (Передний верхний)"
        };

        static readonly string[] CurrentPartNames =
        {
            "V3_Transfer_Outfit_Sail_Mizzen", "V3_Transfer_Outfit_Sail_Fore_Course",
            "V3_Transfer_Outfit_Sail_Main_Course", "V3_Transfer_Outfit_Sail_Main_Topsail",
            "V17_Main_Pirate_Flag_Cloth", "V17_Fore_Pirate_Flag_Cloth", "",
            "V3_Transfer_Outfit_Sail_Fore_Topsail"
        };

        static readonly string[] LegacyPartNames =
        {
            "F2_SailBack", "F2_SailFront", "F2_SailMid1", "F2_SailMid2",
            "F2_MainFlag", "F2_Flag1", "F2_Flag2", ""
        };

        public static bool StreamerMode
        {
            get => PlayerPrefs.GetInt("SailStreamerMode", 0) == 1;
            set
            {
                PlayerPrefs.SetInt("SailStreamerMode", value ? 1 : 0);
                PlayerPrefs.Save();
                RefreshAllInstances();
            }
        }

        public static readonly List<SailCustomizer> ActiveInstances = new List<SailCustomizer>();

        [SerializeField] Renderer[] partRenderers;
        [SerializeField] Material sailBaseMaterial;

        Material[] partMaterials;
        MeshCollider[] partColliders;
        Mesh[] selectionMeshes;
        Mesh mizzenSourceMesh, mizzenDecalMesh;
        Vector4[] decalCanvasScales;
        bool currentModel;
        Texture2D[,] currentTextures = new Texture2D[TotalParts, TotalLayers];
        ShipCustomizationData currentData = new ShipCustomizationData();
        int highlightedPart = -1;
        int selectedPart = 0;

        public ShipCustomizationData CurrentData => currentData;
        public Texture2D[,] CurrentTextures => currentTextures;
        public Renderer[] PartRenderers => partRenderers;
        public MeshCollider[] PartColliders => partColliders;
        public int SelectedPart => selectedPart;
        public bool HasPart(int index) => partRenderers != null && index >= 0 && index < partRenderers.Length && partRenderers[index] != null;
        public bool IsFlag(int index) => index >= 4 && index <= 6;

        public string GetPartName(int index)
        {
            if (currentModel && index == 5) return "Флаг фок-мачты";
            return index >= 0 && index < PartDisplayNames.Length ? PartDisplayNames[index] : "";
        }

        public static void RefreshAllInstances()
        {
            foreach (var inst in ActiveInstances)
            {
                if (inst != null) inst.UpdateVisuals();
            }
        }

        void OnEnable()
        {
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
        }

        void OnDisable()
        {
            SetSelectionEnabled(false);
            ActiveInstances.Remove(this);
        }

        void Awake()
        {
            InitializeSails();
        }

        void Start()
        {
            InitializeSails();
            if (GetComponent<SailNetworkSync>() == null && SailCustomizationStorage.Load(out var savedData, out var savedTextures))
            {
                ApplyCustomization(savedData, savedTextures, false);
            }
        }

        public void InitializeSails()
        {
            if (partRenderers == null || partRenderers.Length < TotalParts || partRenderers[0] == null)
            {
                partRenderers = new Renderer[TotalParts];
                var renderers = GetComponentsInChildren<Renderer>(true);
                currentModel = System.Array.Exists(renderers, renderer => renderer.name == CurrentPartNames[0]);
                var names = currentModel ? CurrentPartNames : LegacyPartNames;
                for (int i = 0; i < TotalParts; i++)
                {
                    if (names[i].Length > 0) partRenderers[i] = System.Array.Find(renderers, renderer => renderer.name == names[i]);
                }
            }
            else currentModel = partRenderers[0].name == CurrentPartNames[0];

            if (partMaterials == null || partMaterials.Length != TotalParts || partColliders == null || partColliders.Length != TotalParts)
            {
                partMaterials = new Material[TotalParts];
                partColliders = new MeshCollider[TotalParts];
                selectionMeshes = new Mesh[TotalParts];
                decalCanvasScales = new Vector4[TotalParts];

                if (sailBaseMaterial == null)
                {
                    sailBaseMaterial = Resources.Load<Material>("SailCustom");
                    if (sailBaseMaterial == null)
                    {
                        var shader = Shader.Find("PirateSlop/Sail");
                        if (shader != null) sailBaseMaterial = new Material(shader);
                    }
                }

                for (int i = 0; i < TotalParts; i++)
                {
                    var r = partRenderers[i];
                    if (r != null)
                    {
                        if (partMaterials[i] == null)
                        {
                            partMaterials[i] = new Material(sailBaseMaterial != null ? sailBaseMaterial : r.sharedMaterial);
                            partMaterials[i].name = $"PartMat_{r.name}_{i}";
                            if (currentModel && r.sharedMaterial != null)
                            {
                                var source = r.sharedMaterial;
                                partMaterials[i].SetColor("_FabricTint", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white);
                                if (source.HasProperty("_OcclusionMap") && source.GetTexture("_OcclusionMap") != null)
                                    partMaterials[i].SetTexture("_OcclusionMap", source.GetTexture("_OcclusionMap"));
                            }
                            partMaterials[i].SetFloat("_UseFaceOrientation", currentModel ? 1f : 0f);
                            partMaterials[i].SetFloat("_PreserveDecalColor", currentModel ? 1f : 0f);
                            decalCanvasScales[i] = currentModel ? PrepareDecalCoordinates(r, i == 0) : new Vector4(1f, 1f, 0f, 0f);
                            partMaterials[i].SetFloat("_UseDecalUV2", currentModel && i == 0 && mizzenDecalMesh != null ? 1f : 0f);
                            partMaterials[i].SetVector("_DecalCanvasScale", decalCanvasScales[i]);
                        }
                        r.sharedMaterial = partMaterials[i];
                    }
                }
            }
        }

        Vector4 PrepareDecalCoordinates(Renderer renderer, bool mizzen)
        {
            var skin = renderer as SkinnedMeshRenderer;
            var filter = renderer.GetComponent<MeshFilter>();
            var source = skin != null ? skin.sharedMesh : filter != null ? filter.sharedMesh : null;
            if (source == null || !source.isReadable) return new Vector4(1f, 1f, 0f, 0f);
            var vertices = source.vertices;
            var matrix = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var positions = new Vector3[vertices.Length];
            var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < vertices.Length; i++)
            {
                positions[i] = matrix.MultiplyPoint3x4(vertices[i]);
                minimum = Vector3.Min(minimum, positions[i]);
                maximum = Vector3.Max(maximum, positions[i]);
            }
            var dimensions = maximum - minimum;
            float width = Mathf.Max(dimensions.x, dimensions.z);
            float height = dimensions.y;
            float extent = Mathf.Max(width, height, .0001f);
            if (!mizzen) return new Vector4(width / extent, height / extent, 0f, 0f);
            var center = (maximum + minimum) * .5f;
            var decalUV = new Vector2[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                decalUV[i] = new Vector2((positions[i].z - center.z) / extent + .5f, (positions[i].y - center.y) / extent + .5f);
            mizzenSourceMesh = source;
            mizzenDecalMesh = Instantiate(source);
            mizzenDecalMesh.name = source.name + "_CustomizationUV";
            mizzenDecalMesh.uv2 = decalUV;
            if (skin != null) skin.sharedMesh = mizzenDecalMesh;
            else if (filter != null) filter.sharedMesh = mizzenDecalMesh;
            return new Vector4(1f, 1f, 0f, 0f);
        }

        public void SetSelectionEnabled(bool enabled)
        {
            if (enabled) InitializeSails();
            if (partColliders == null) return;
            for (int i = 0; i < TotalParts; i++)
            {
                if (enabled && HasPart(i))
                {
                    var renderer = partRenderers[i];
                    var col = partColliders[i];
                    if (col == null)
                    {
                        col = new GameObject("CustomizationSelection").AddComponent<MeshCollider>();
                        col.transform.SetParent(renderer.transform, false);
                        col.gameObject.layer = renderer.gameObject.layer;
                        partColliders[i] = col;
                    }
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        selectionMeshes[i] ??= new Mesh { name = "CustomizationSelection_" + i };
                        skin.BakeMesh(selectionMeshes[i]);
                        col.sharedMesh = selectionMeshes[i];
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        col.sharedMesh = filter != null ? filter.sharedMesh : null;
                    }
                }
                if (partColliders[i] != null) partColliders[i].enabled = enabled;
            }
        }

        void OnDestroy()
        {
            ActiveInstances.Remove(this);
            if (partMaterials != null) foreach (var material in partMaterials) if (material != null) Destroy(material);
            if (selectionMeshes != null) foreach (var mesh in selectionMeshes) if (mesh != null) Destroy(mesh);
            if (mizzenDecalMesh != null)
            {
                if (HasPart(0))
                {
                    var skin = partRenderers[0] as SkinnedMeshRenderer;
                    var filter = partRenderers[0].GetComponent<MeshFilter>();
                    if (skin != null && skin.sharedMesh == mizzenDecalMesh) skin.sharedMesh = mizzenSourceMesh;
                    else if (filter != null && filter.sharedMesh == mizzenDecalMesh) filter.sharedMesh = mizzenSourceMesh;
                }
                Destroy(mizzenDecalMesh);
            }
        }

        public void ApplyCustomization(ShipCustomizationData data, Texture2D[,] textures, bool syncToNetwork)
        {
            InitializeSails();
            currentData = data.Clone();

            for (int p = 0; p < TotalParts; p++)
            {
                for (int l = 0; l < TotalLayers; l++)
                {
                    if (textures != null && p < textures.GetLength(0) && l < textures.GetLength(1) && textures[p, l] != null)
                    {
                        currentTextures[p, l] = textures[p, l];
                    }
                    else if (!currentData.sails[p].GetHasDecal(l))
                    {
                        currentTextures[p, l] = null;
                    }
                }
            }

            UpdateVisuals();

            if (syncToNetwork)
            {
                var netSync = GetComponent<SailNetworkSync>();
                if (netSync != null)
                {
                    netSync.UploadCustomization(currentData, currentTextures);
                }
            }
        }

        public void SetSelectedPart(int index)
        {
            selectedPart = index;
            UpdateVisuals();
        }

        public void SetHighlight(int partIndex, bool highlight)
        {
            if (highlight)
            {
                highlightedPart = partIndex;
            }
            else
            {
                if (partIndex == -1 || highlightedPart == partIndex)
                {
                    highlightedPart = -1;
                }
            }

            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            InitializeSails();
            if (partMaterials == null || partRenderers == null) return;

            var nameplate = GetComponent<ShipNameplate>();
            if (nameplate != null) nameplate.SetName(currentData.shipName);

            bool hideDecals = StreamerMode;

            for (int i = 0; i < TotalParts; i++)
            {
                var mat = partMaterials[i];
                if (mat == null) continue;
                if (partRenderers[i] != null && partRenderers[i].sharedMaterial != mat)
                {
                    partRenderers[i].sharedMaterial = mat;
                }

                currentData.EnsureCapacity();
                var sail = currentData.sails[i];
                mat.SetColor("_BaseColor", sail.baseColor);
                mat.SetFloat("_Weathering", sail.weathering);
                mat.SetFloat("_Grime", sail.grime);

                var frontBounds = !currentModel && i < PartUVBoundsFront.Length ? PartUVBoundsFront[i] : new Vector4(0, 0, 1, 1);
                var backBounds = !currentModel && i < PartUVBoundsBack.Length ? PartUVBoundsBack[i] : new Vector4(0, 0, 1, 1);
                mat.SetVector("_SailUVBoundsFront", frontBounds);
                mat.SetVector("_SailUVBoundsBack", backBounds);
                mat.SetFloat("_MirrorBack", 1f);

                // Layer 1
                mat.SetFloat("_BlendMode", sail.blendMode);
                if (!hideDecals && sail.hasDecal && currentTextures[i, 0] != null)
                {
                    currentTextures[i, 0].wrapMode = TextureWrapMode.Clamp;
                    mat.SetTexture("_DecalTex", currentTextures[i, 0]);
                    mat.SetVector("_DecalImageScale", ImageScale(currentTextures[i, 0]));
                    mat.SetVector("_DecalTransform", new Vector4(sail.scale.x, sail.scale.y, sail.offset.x, sail.offset.y));
                    mat.SetFloat("_DecalRotation", sail.rotation);
                    mat.SetColor("_DecalColor", sail.decalColor);
                }
                else
                {
                    mat.SetTexture("_DecalTex", Texture2D.blackTexture);
                    mat.SetColor("_DecalColor", Color.clear);
                }

                // Layer 2
                mat.SetFloat("_BlendMode2", sail.blendMode2);
                if (!hideDecals && sail.hasDecal2 && currentTextures[i, 1] != null)
                {
                    currentTextures[i, 1].wrapMode = TextureWrapMode.Clamp;
                    mat.SetTexture("_DecalTex2", currentTextures[i, 1]);
                    mat.SetVector("_DecalImageScale2", ImageScale(currentTextures[i, 1]));
                    mat.SetVector("_DecalTransform2", new Vector4(sail.scale2.x, sail.scale2.y, sail.offset2.x, sail.offset2.y));
                    mat.SetFloat("_DecalRotation2", sail.rotation2);
                    mat.SetColor("_DecalColor2", sail.decalColor2);
                }
                else
                {
                    mat.SetTexture("_DecalTex2", Texture2D.blackTexture);
                    mat.SetColor("_DecalColor2", Color.clear);
                }

                Color highlight = Color.black;
                if (highlightedPart == i && selectedPart != i)
                {
                    highlight = new Color(0.18f, 0.15f, 0.05f, 1f);
                }
                mat.SetColor("_HighlightColor", highlight);
            }
        }

        Vector4 ImageScale(Texture2D texture)
        {
            if (!currentModel) return new Vector4(1f, 1f, 0f, 0f);
            float extent = Mathf.Max(texture.width, texture.height);
            return new Vector4(texture.width / extent, texture.height / extent, 0f, 0f);
        }
    }
}
