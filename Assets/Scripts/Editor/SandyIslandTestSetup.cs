using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PirateSlop.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace PirateSlop.Editor
{
    public static class SandyIslandTestSetup
    {
        public const string Prefabs = "Assets/Prefabs/World/SandyIslands";
        const string Models = "Assets/Models/World/SandyIslands";
        const string Settings = "Assets/Settings/World/SandyIslands";
        const string Materials = "Assets/Materials/SandyIslands";
        const string Gallery = "Assets/Resources/EnvironmentTest/Gallery.prefab";
        const float Floor = -230f;
        static readonly string[] Names = { "SmallSandyIsland", "TwoLandingIsland", "SingleLandingRockIsland" };
        static readonly string[] Textures = { "coast_sand_01", "leafy_grass", "brown_mud_03", "coast_sand_rocks_02" };
        static readonly float[] TileMetres = { 15f, 2f, 1.3f, 15f };
        static readonly Vector2[] Groves = { new(-.34f, .28f), new(.34f, .33f), new(.38f, -.27f), new(-.42f, -.24f) };

        struct Shape
        {
            public int Kind;
            public bool Excavation;
            public float Radius, Size, Land, Wall, Gap;
            public Vector2 Axis;
        }

        public static string Revision => string.Join("|", Names.Concat(new[]{"BalanceSandyIsland"}).Select(n => AssetDatabase.GetAssetDependencyHash(Prefabs + "/" + n + ".prefab").ToString()));

        [MenuItem("PirateSlop/Art/Prepare Sandy Island Test Row")]
        public static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before preparing islands.");
            foreach (string path in new[] { Prefabs, Models, Settings, Materials }) Directory.CreateDirectory(path);
            AssetDatabase.Refresh();
            var layers = PrepareLayers();
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader == null) throw new InvalidOperationException("URP Terrain Lit is missing.");
            var ground = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/SandyGround.mat");
            if (ground == null) { ground = new Material(terrainShader); AssetDatabase.CreateAsset(ground, Materials + "/SandyGround.mat"); }
            ground.enableInstancing = true;
            ground.EnableKeyword("_NORMALMAP");
            ground.EnableKeyword("_MASKMAP");
            EditorUtility.SetDirty(ground);
            var rock = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CoastalEnvironment/CoastalRock.mat");
            if (rock == null) throw new InvalidOperationException("Coastal rock material is missing.");
            SavePrepared(layers,ground,rock);
        }

        public static void RebuildGeometry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            SavePrepared(Textures.Select(n=>AssetDatabase.LoadAssetAtPath<TerrainLayer>(Materials+"/"+n+".terrainlayer")).ToArray(),AssetDatabase.LoadAssetAtPath<Material>(Materials+"/SandyGround.mat"),AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/CoastalEnvironment/CoastalRock.mat"));
        }

        static void SavePrepared(TerrainLayer[] layers,Material ground,Material rock)
        {
            float barrier = ShipNestFloor() + 2f;
            var shapes = new[]
            {
                new Shape { Kind=0, Radius=28, Size=128, Land=3.4f, Axis=new Vector2(1.08f,.9f) },
                new Shape { Kind=1, Radius=59, Size=240, Land=8.2f, Wall=12, Gap=16, Axis=new Vector2(1.12f,.94f) },
                new Shape { Kind=2, Radius=78, Size=320, Land=4.8f, Wall=barrier, Gap=8, Axis=new Vector2(1.1f,.94f) }
            };
            for (int i=0; i<shapes.Length; i++) BuildIsland(Names[i], shapes[i], layers, ground, rock);
            var eventShape=shapes[0];eventShape.Excavation=true;
            BuildIsland("BalanceSandyIsland",eventShape,layers,ground,rock);
            var gallery = PrefabUtility.LoadPrefabContents(Gallery);
            try { Attach(gallery); PrefabUtility.SaveAsPrefabAsset(gallery, Gallery); }
            finally { PrefabUtility.UnloadPrefabContents(gallery); }
            CoastalEnvironmentSetup.RefreshCatalogVersions();
            AssetDatabase.SaveAssets();
            Debug.Log("Saved three sandy test islands. Single-landing rock crest maximum: " + barrier.ToString("F2") + " metres above sea level.");
        }

        public static void Attach(GameObject gallery)
        {
            if (Names.Any(n => AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + n + ".prefab") == null)) return;
            var component = gallery.GetComponent<EnvironmentTestGallery>();
            var previous = gallery.transform.Find("SandyIslandTestRow");
            var labels = component.Labels.Where(t => t != null && (previous == null || !t.IsChildOf(previous))).ToList();
            if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var row = new GameObject("SandyIslandTestRow").transform;
            row.SetParent(gallery.transform, false);
            float[] positions = { -520, -130, 340 };
            string[] titles = { "1 - Small Sandy Island", "2 - Two Landings", "3 - One Landing / Rock Ring" };
            for (int i=0; i<Names.Length; i++)
            {
                var island = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "/" + Names[i] + ".prefab"), row);
                island.transform.localPosition = new Vector3(positions[i], 0, 700);
                var label = new GameObject(titles[i]).transform;
                label.SetParent(row, false);
                label.localPosition = new Vector3(positions[i], i==2 ? 34 : 23, 700);
                labels.Add(label);
            }
            var oldEvent=gallery.transform.Find("BalanceEventIsland");
            if(oldEvent!=null) UnityEngine.Object.DestroyImmediate(oldEvent.gameObject);
            var eventPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs+"/BalanceSandyIsland.prefab");
            if(eventPrefab!=null)
            {
                var eventIsland=(GameObject)PrefabUtility.InstantiatePrefab(eventPrefab,gallery.transform);
                eventIsland.name="BalanceEventIsland";
                eventIsland.transform.localPosition=EnvironmentEventTestRow.Point(7,0);
            }
            component.Labels = labels.ToArray();
        }

        static TerrainLayer[] PrepareLayers()
        {
            var result = new TerrainLayer[Textures.Length];
            for (int i=0; i<Textures.Length; i++)
            {
                string folder = Models + "/Textures/" + Textures[i] + "/";
                foreach (string file in new[] { "Colour.jpg", "Normal.png", "Roughness.jpg" })
                {
                    var importer = AssetImporter.GetAtPath(folder + file) as TextureImporter;
                    if (importer == null) throw new InvalidOperationException("Missing island texture " + folder + file);
                    importer.textureType = file == "Normal.png" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = file == "Colour.jpg";
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear;
                    importer.anisoLevel = 8;
                    importer.maxTextureSize = 2048;
                    importer.mipmapEnabled = importer.streamingMipmaps = true;
                    importer.isReadable = file == "Roughness.jpg";
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
                var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Roughness.jpg");
                var packed = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
                try
                {
                    packed.SetPixels(rough.GetPixels().Select(p => new Color(0,1,.5f,1-p.r)).ToArray());
                    packed.Apply();
                    File.WriteAllBytes(folder + "Mask.png", packed.EncodeToPNG());
                    AssetDatabase.ImportAsset(folder + "Mask.png", ImportAssetOptions.ForceSynchronousImport);
                }
                finally { UnityEngine.Object.DestroyImmediate(packed); }
                var maskImporter = (TextureImporter)AssetImporter.GetAtPath(folder + "Mask.png");
                maskImporter.sRGBTexture = false;
                maskImporter.wrapMode = TextureWrapMode.Repeat;
                maskImporter.filterMode = FilterMode.Trilinear;
                maskImporter.anisoLevel = 8;
                maskImporter.mipmapEnabled = maskImporter.streamingMipmaps = true;
                maskImporter.textureCompression = TextureImporterCompression.CompressedHQ;
                maskImporter.SaveAndReimport();
                var roughImporter = (TextureImporter)AssetImporter.GetAtPath(folder + "Roughness.jpg");
                roughImporter.isReadable = false;
                roughImporter.SaveAndReimport();
                string path = Materials + "/" + Textures[i] + ".terrainlayer";
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
                layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Colour.jpg");
                layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Normal.png");
                layer.maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "Mask.png");
                layer.tileSize = Vector2.one * TileMetres[i];
                layer.normalScale = i==0 ? .6f : .8f;
                layer.metallic = 0;
                layer.smoothness = .1f;
                EditorUtility.SetDirty(layer);
                result[i] = layer;
            }
            return result;
        }

        static void BuildIsland(string name, Shape shape, TerrainLayer[] layers, Material ground, Material rock)
        {
            var root = new GameObject(name);
            try
            {
                int worldLayer = LayerMask.NameToLayer("WorldStatic");
                if (worldLayer < 0) throw new InvalidOperationException("WorldStatic layer is missing.");
                root.layer = worldLayer;
                string dataPath = Settings + "/" + name + ".asset";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
                if (data == null) { data = new TerrainData { name=name }; AssetDatabase.CreateAsset(data, dataPath); }
                data.heightmapResolution = 513;
                data.alphamapResolution = 256;
                data.baseMapResolution = 1024;
                data.size = new Vector3(shape.Size, 310, shape.Size);
                data.terrainLayers = layers;
                var heights = new float[513,513];
                for (int z=0; z<513; z++) for (int x=0; x<513; x++)
                {
                    var p = new Vector2(x/512f*shape.Size-shape.Size*.5f, z/512f*shape.Size-shape.Size*.5f);
                    heights[z,x] = (Height(shape,p)-Floor)/data.size.y;
                }
                data.SetHeights(0,0,heights);
                var holes = new bool[data.holesResolution,data.holesResolution];
                for (int z=0; z<data.holesResolution; z++) for (int x=0; x<data.holesResolution; x++)
                {
                    float px=(x+.5f)/data.holesResolution*shape.Size-shape.Size*.5f;
                    float pz=(z+.5f)/data.holesResolution*shape.Size-shape.Size*.5f;
                    holes[z,x]=!shape.Excavation || Mathf.Pow((px+.27f)/1.46f,4)+Mathf.Pow(pz/1.08f,4)>1;
                }
                data.SetHoles(0,0,holes);
                var maps = new float[256,256,4];
                for (int z=0; z<256; z++) for (int x=0; x<256; x++)
                {
                    var p = new Vector2(x/255f*shape.Size-shape.Size*.5f, z/255f*shape.Size-shape.Size*.5f);
                    float h = Height(shape,p);
                    float grove = GroveCoverage(shape,p);
                    float grass = h>2 ? grove*.72f : 0;
                    float mud = grass*.24f*(.4f+.6f*Mathf.PerlinNoise(p.x*.2f+31,p.y*.2f+22));
                    float rocky = h>1 && h<shape.Land*.72f && EntryWeight(shape,Angle(shape,p))<.15f && shape.Kind>0 ? .3f : 0;
                    float sand = Mathf.Max(.12f,1-grass-mud-rocky);
                    float sum = sand+grass+mud+rocky;
                    maps[z,x,0]=sand/sum; maps[z,x,1]=grass/sum; maps[z,x,2]=mud/sum; maps[z,x,3]=rocky/sum;
                }
                data.SetAlphamaps(0,0,maps);
                EditorUtility.SetDirty(data);
                var terrainObject = Terrain.CreateTerrainGameObject(data);
                terrainObject.name = "EditableSandyGround";
                terrainObject.transform.SetParent(root.transform,false);
                terrainObject.transform.localPosition = new Vector3(-shape.Size*.5f,Floor,-shape.Size*.5f);
                terrainObject.layer = worldLayer;
                var terrain = terrainObject.GetComponent<Terrain>();
                terrain.materialTemplate = ground;
                terrain.drawInstanced = true;
                terrain.heightmapPixelError = 2;
                terrain.basemapDistance = 700;
                terrain.shadowCastingMode = ShadowCastingMode.On;
                if (shape.Kind>0) BuildRockRim(root, name, shape, rock);
                else if(shape.Excavation) BuildExcavation(root,shape);
                AddDecor(root,shape);
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer=worldLayer;
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static float Angle(Shape shape, Vector2 p) => Mathf.Atan2(p.y/shape.Axis.y,p.x/shape.Axis.x)*Mathf.Rad2Deg;
        static float Edge(Shape shape, float radians) => shape.Radius*(1+.055f*Mathf.Sin(radians*3+.6f)+.028f*Mathf.Cos(radians*5+1.1f)+.017f*Mathf.Sin(radians*7));
        static float EntryWeight(Shape shape, float degrees)
        {
            if (shape.Kind==0) return 1;
            float distance = Mathf.Abs(Mathf.DeltaAngle(degrees,-90));
            if (shape.Kind==1) distance = Mathf.Min(distance,Mathf.Abs(Mathf.DeltaAngle(degrees,90)));
            return 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(shape.Gap-4,shape.Gap,distance));
        }

        static float Height(Shape shape, Vector2 p)
        {
            var scaled = new Vector2(p.x/shape.Axis.x,p.y/shape.Axis.y);
            float angle = Mathf.Atan2(scaled.y,scaled.x);
            float r = scaled.magnitude/Edge(shape,angle);
            float relief = (Mathf.PerlinNoise(p.x*.045f+17,p.y*.045f+31)-.5f)*.9f;
            float h;
            if (shape.Kind==0) h = Mathf.Lerp(shape.Land+relief,-5,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,1.45f,r)));
            else
            {
                float coast = Mathf.Lerp(shape.Land+relief,-5,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.91f,1.12f,r)));
                float ramp = Mathf.Lerp(shape.Land+relief,-5,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.72f,1.5f,r)));
                h = Mathf.Lerp(coast,ramp,EntryWeight(shape,angle*Mathf.Rad2Deg));
            }
            if (r>1.5f) h = Mathf.Lerp(-5,Floor,Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.5f,1.94f,r)));
            return Mathf.Max(Floor,h);
        }

        static float GroveCoverage(Shape shape, Vector2 p)
        {
            float amount=0;
            foreach (var grove in Groves)
            {
                float distance=Vector2.Distance(p,grove*shape.Radius);
                amount=Mathf.Max(amount,1-Mathf.SmoothStep(0,1,distance/(shape.Kind==0 ? 7 : 14)));
            }
            return amount*(.7f+.3f*Mathf.PerlinNoise(p.x*.16f+70,p.y*.16f+26));
        }

        static void BuildRockRim(GameObject root, string name, Shape shape, Material material)
        {
            var rim = new GameObject("NaturalRockRim").transform;
            rim.SetParent(root.transform, false);
            var random = new System.Random(9107 + shape.Kind);
            float step = shape.Kind == 2 ? 16f : 7.5f;
            int count = Mathf.CeilToInt(Mathf.PI * 2 * shape.Radius / step);
            for (int i = 0; i < count; i++)
            {
                float degrees = -90 + 360f * (i + .5f) / count;
                float opening = Mathf.Abs(Mathf.DeltaAngle(degrees, -90));
                if (shape.Kind == 1) opening = Mathf.Min(opening, Mathf.Abs(Mathf.DeltaAngle(degrees, 90)));
                float halfWidthDegrees = (step * .72f / shape.Radius) * Mathf.Rad2Deg;
                if (opening < shape.Gap + halfWidthDegrees) continue;
                string source = "CliffWall" + new[] { "A", "B", "C" }[random.Next(3)];
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/StarterIsland/" + source + ".prefab");
                var part = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rim);
                part.name = "CoastalCliff_" + i.ToString("00");
                var sand = part.transform.Find("CoastalSand");
                if (sand != null) sand.gameObject.SetActive(false);
                var visual = part.transform.Find("CoastalVisual/LOD0");
                var bounds = new Bounds(); bool first = true;
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                {
                    if (r.sharedMaterial.shader.name.Contains("Foliage")) continue;
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                }
                float crown = shape.Wall - (float)random.NextDouble() * (shape.Kind == 2 ? .65f : 1.7f);
                if (shape.Kind == 2 && i == count / 2) crown = shape.Wall;
                float baseY = -1.1f - (float)random.NextDouble() * .5f;
                float scale = (crown - baseY) / bounds.size.y;
                part.transform.localScale = Vector3.one * scale;
                float angle = degrees * Mathf.Deg2Rad;
                float radius = Edge(shape, angle) * (shape.Kind == 2 ? 1.02f : 1.005f);
                part.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius * shape.Axis.x, baseY - bounds.min.y * scale, Mathf.Sin(angle) * radius * shape.Axis.y);
                part.transform.localRotation = Quaternion.Euler(0, 90 - degrees + ((float)random.NextDouble() - .5f) * 14, 0);
            }
        }

        static void BuildExcavation(GameObject root, Shape shape)
        {
            const int sections=96;
            var v=new List<Vector3>(); var uv=new List<Vector2>(); var t=new List<int>();
            float ground=Height(shape,Vector2.zero);
            float[] radius={2f,1.42f,1.36f,1.28f,1.24f};
            float[] levels={.012f,.035f,-.12f,-2.6f,-2.95f};
            for(int ring=0;ring<radius.Length;ring++) for(int i=0;i<sections;i++)
            {
                float a=i*Mathf.PI*2/sections;
                float rough=1+.035f*Mathf.Sin(a*7)+.022f*Mathf.Cos(a*11);
                float ca=Mathf.Cos(a),sa=Mathf.Sin(a);
                float x=Mathf.Sign(ca)*Mathf.Sqrt(Mathf.Abs(ca))*radius[ring]*rough,z=Mathf.Sign(sa)*Mathf.Sqrt(Mathf.Abs(sa))*radius[ring]*.74f*rough;
                float y=ring==0 ? Height(shape,new Vector2(x-.27f,z))+.015f : ground+levels[ring]+.035f*Mathf.Sin(a*13+ring);
                v.Add(new Vector3(x-.27f,y,z)); uv.Add(new Vector2(x/2.8f,z/2.8f+y*.21f));
                if(ring==0) continue;
                int outer=(ring-1)*sections+i,next=(i+1)%sections;
                int outerNext=(ring-1)*sections+next,inner=ring*sections+i,innerNext=ring*sections+next;
                t.Add(outer);t.Add(outerNext);t.Add(inner);t.Add(outerNext);t.Add(innerNext);t.Add(inner);
            }
            int centre=v.Count;v.Add(new Vector3(-.27f,ground-2.97f,0));uv.Add(Vector2.zero);
            for(int i=0;i<sections;i++) {t.Add(centre);t.Add(4*sections+i);t.Add(4*sections+(i+1)%sections);}
            for(int i=0;i<t.Count;i+=3) {int swap=t[i+1];t[i+1]=t[i+2];t[i+2]=swap;}
            var mesh=new Mesh {name="ExcavatedSandPit"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            string path=Models+"/ExcavatedSandPit.asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null) AssetDatabase.CreateAsset(mesh,path); else {EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;EditorUtility.SetDirty(old);}
            string materialPath=Materials+"/ExcavatedSand.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null) {mat=new Material(Shader.Find("PirateSlop/ExcavatedSand"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.shader=Shader.Find("PirateSlop/ExcavatedSand");mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_TextureScale",1/15f);mat.SetFloat("_NormalScale",.6f);
            string folder=Models+"/Textures/coast_sand_01/";
            mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Colour.jpg"));
            mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Normal.png"));mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.6f);mat.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(mat);
            var pit=new GameObject("DugPitWithCutSandWalls");pit.transform.SetParent(root.transform,false);
            pit.AddComponent<MeshFilter>().sharedMesh=mesh;pit.AddComponent<MeshRenderer>().sharedMaterial=mat;pit.AddComponent<MeshCollider>().sharedMesh=mesh;
        }

        static void AddDecor(GameObject root, Shape shape)
        {
            var random=new System.Random(7301+shape.Kind*71);
            int palms=shape.Kind==0 ? 5 : shape.Kind==1 ? 16 : 22;
            int bushes=shape.Kind==0 ? 12 : shape.Kind==1 ? 36 : 48;
            int ferns=shape.Kind==0 ? 10 : shape.Kind==1 ? 25 : 32;
            int rocks=shape.Kind==0 ? 4 : 9;
            int count=palms+bushes+ferns+rocks;
            var placed=new List<(Vector2 point,float clearance)>();
            for (int i=0; i<count; i++)
            {
                bool palm=i<palms, stone=i>=palms+bushes+ferns;
                string source=palm ? new[]{"Palm","PalmBent","PalmYoung"}[i%3] : stone ? (i%2==0 ? "RockLarge" : "RockMedium") : i<palms+bushes ? "Bush" : "Fern";
                float clearance=palm ? 2.4f : stone ? 1.6f : .7f;
                Vector2 p=default;
                bool found=false;
                for (int attempt=0; attempt<180; attempt++)
                {
                    var grove=Groves[random.Next(Groves.Length)]*shape.Radius;
                    float spread=shape.Kind==0 ? 7 : 14;
                    p=grove+new Vector2((float)random.NextDouble()*2-1,(float)random.NextDouble()*2-1)*spread;
                    if (p.magnitude>shape.Radius*.78f || p.magnitude<shape.Radius*.14f || Height(shape,p)<2) continue;
                    if (shape.Excavation && p.x>-3 && p.x<11 && Mathf.Abs(p.y)<3) continue;
                    if (shape.Kind>0 && Mathf.Abs(p.x)<8 && (p.y<-shape.Radius*.25f || shape.Kind==1 && p.y>shape.Radius*.25f)) continue;
                    if (placed.Any(v=>Vector2.Distance(v.point,p)<v.clearance+clearance)) continue;
                    found=true; break;
                }
                if (!found) continue;
                placed.Add((p,clearance));
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/World/StarterIsland/"+source+".prefab");
                var plant=(GameObject)PrefabUtility.InstantiatePrefab(prefab,root.transform);
                plant.name=source+"_"+i.ToString("D3");
                float scale=stone ? .4f+(float)random.NextDouble()*.4f : .75f+(float)random.NextDouble()*.45f;
                plant.transform.localPosition=new Vector3(p.x,Height(shape,p)-.035f,p.y);
                plant.transform.localRotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                plant.transform.localScale=Vector3.one*scale;
                var sand=plant.transform.Find("CoastalSand");
                if (sand!=null) sand.gameObject.SetActive(false);
                if (!stone)
                {
                    var wind=plant.AddComponent<SandyIslandFoliageWind>();
                    wind.Height=(source=="Palm" ? 7.38f : source=="PalmBent" ? 6.35f : source=="PalmYoung" ? 4.8f : source=="Bush" ? 1.33f : .76f)*scale;
                    wind.Strength=palm ? .2f : .065f;
                    wind.Phase=(float)random.NextDouble()*Mathf.PI*2;
                    wind.Apply();
                }
            }
        }

        static float ShipNestFloor()
        {
            var ship=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Ships/ShipV3Test.prefab");
            if (ship==null) throw new InvalidOperationException("Test ship is missing.");
            return ship.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("Nest_Stand_")).Max(t=>t.position.y-ship.transform.position.y);
        }
    }
}
