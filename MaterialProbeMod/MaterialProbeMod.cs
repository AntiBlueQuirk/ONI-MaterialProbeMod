using Harmony;
using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using System.Collections.Generic;
using System.Reflection.Emit;
using Newtonsoft.Json.Serialization;

namespace MaterialProbeMod
{

    public static class MaterialProbeModMain
    {
        public const string ModName = "MaterialProbeMod";
        public static readonly string ModPath = ("Mods/" + ModName + "/").Replace('/', Path.DirectorySeparatorChar);
        public static readonly string ModConfig = ModPath + "config.json";

        //Called by ModLoader
        public static void OnLoad()
        {
            //ReadConfigOrReset();
        }

        ////Dead simple config handling.
        //public static PriorExperienceModConfig config;
        //
        //public static void ReadConfigOrReset()
        //{
        //    config = new PriorExperienceModConfig();
        //    string path = ("Mods/"+ModName+".json").Replace('/', Path.DirectorySeparatorChar);
        //    JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings { Formatting = Formatting.Indented });
        //    if (File.Exists(path))
        //    {
        //        try
        //        {
        //            PriorExperienceModConfig new_config;
        //            using (StreamReader streamReader = new StreamReader(path))
        //            {
        //                using (JsonTextReader jsonTextReader = new JsonTextReader(streamReader))
        //                {
        //                    new_config = serializer.Deserialize<PriorExperienceModConfig>(jsonTextReader);
        //                    config = new_config;
        //                }
        //            }
        //        }
        //        catch (IOException ex)
        //        {
        //            Debug.Log(ModName + ": Couldn't read config, using defaults: " + ex.ToString());
        //        }
        //        catch (JsonException ex)
        //        {
        //            Debug.Log(ModName + ": Invalid config, using defaults: " + ex.ToString());
        //        }
        //    } else {
        //        Debug.Log(ModName + ": Config not found, writing defaults.");
        //        using (StreamWriter streamWriter = new StreamWriter(path))
        //        {
        //            using (JsonTextWriter jsonTextWriter = new JsonTextWriter(streamWriter))
        //            {
        //                serializer.Serialize(jsonTextWriter, config);
        //            }
        //        }
        //    }
        //}
        //
        //[JsonObject(MemberSerialization.OptIn)]
        //public class PriorExperienceModConfig
        //{
        //    [JsonProperty]
        //    public float StepDownSpeedMultiplier { get; set; } = 1.3f; //30% faster by default
        //}
    }

    public enum OverlayMode
    {
        MASS,
        TEMPERATURE,
        GERMS,
        BIOME,
    }
    public static class PatchCommon
    {
        //Actions are used by the game for key-bindings. Unfortunately, the game relies pretty heavily
        //on the actual count of Actions in a few areas. Additionally, while we can avoid collisions with
        //the game easily enough, there's no way to check for collisions against other mods, at least
        //without some unified registration system. (Maybe a Modloader feature?)
        //I'd rather not go into that now, and I don't think a keybinding is particularly important,
        //so there's a few patches below to make this action invisible to the game.
        public static readonly Action Action_MaterialProbeMap;
        public static int colorPalette = 0;
        public static string[] colorPaletteNames =
        {
            "Default",
            "Flat",
            "Substance",
            "UI",
            "Conduit",
            "Hash"
        };
        public static Dictionary<SimHashes, Color> colorOverride = new Dictionary<SimHashes, Color>();

        static PatchCommon() {
            Action_MaterialProbeMap = (Action)Hash.SDBMLower("MaterialProbe");
            if ((uint)Action_MaterialProbeMap < (uint)Action.NumActions) throw new InvalidOperationException();

            colorOverride[SimHashes.Vacuum] = new Color(0.4f, 0.4f, 0.4f);
            colorOverride[SimHashes.CarbonDioxide] = new Color(0.5f, 0.2f, 0.2f);
            colorOverride[SimHashes.Oxygen] = new Color(-1, 0, 0, 0); //use substance color
            colorOverride[SimHashes.ContaminatedOxygen] = new Color(-1, 0, 0, 0);
            colorOverride[SimHashes.Snow] = new Color32(153, 192, 255, 255);
            colorOverride[SimHashes.Granite] = new Color32(158, 94, 131, 255);
            colorOverride[SimHashes.IgneousRock] = new Color32(127, 76, 90, 255);
            colorOverride[SimHashes.Diamond] = new Color32(174, 162, 232, 255);
            colorOverride[SimHashes.Carbon] = new Color32(181, 125, 209, 255);
            colorOverride[SimHashes.Obsidian] = new Color32(89, 96, 127, 255);
            colorOverride[SimHashes.Katairite] = new Color32(82, 70, 127, 255);
        }

        public static Sprite LoadSpriteFromFile(string path)
        {
            Texture2D image;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                image = new Texture2D(2, 2, TextureFormat.RGB24, false); //size and format don't matter, LoadImage overrides them
                ImageConversion.LoadImage(image, bytes);
            } catch (IOException ex) {
                Debug.Log(string.Format("Failed to load sprite from file \"{0}\": {1}", path, ex.ToString()));
                image = new Texture2D(8, 8, TextureFormat.RGB24, false);
            }
            image.filterMode = FilterMode.Trilinear;

            return Sprite.Create(image, new Rect(0, 0, image.width, image.height), new Vector2(0.5f, 0.0f), 1.0f);
        }
        //Gets an already loaded assembly
        public static Assembly GetLoadedAssembly(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == name)
                    return asm;
            }
            return null;
        }
        static bool modsCheck = false;
        static Assembly mod_MaterialColor;
        static Type mod_MaterialColor_SimHashesExtension;
        static MethodInfo mod_MaterialColor_SimHashesExtension_GetMaterialColorForType;
        public static void DoModsCheck()
        {
            if (modsCheck) return;

            mod_MaterialColor = GetLoadedAssembly("MaterialColor");
            if (mod_MaterialColor != null)
                mod_MaterialColor_SimHashesExtension = mod_MaterialColor.GetType("SimHashesExtension", false);
            if (mod_MaterialColor_SimHashesExtension != null)
                mod_MaterialColor_SimHashesExtension_GetMaterialColorForType = AccessTools.Method(mod_MaterialColor_SimHashesExtension, "GetMaterialColorForType", new Type[] { typeof(SimHashes), typeof(string) });

            modsCheck = true;
        }

        public static Color GetColorForElement(Element e)
        {
            Color color;
            switch (colorPalette)
            {
                default:
                case 0: {
                        if (colorOverride.TryGetValue(e.id, out color))
                        {
                            if (color.r < 0)
                                goto case 2;
                            if (color.g < 0)
                                goto case 3;
                            if (color.b < 0)
                                goto case 4;
                            if (color.a < 0)
                                goto case 5;
                            //else just use whatever the color was.
                        }
                        else
                        {
                            color = e.substance.uiColour;
                        }
                    } break;
                case 1: color = Color.white; break;
                case 2: color = e.substance.colour; break;
                case 3: color = e.substance.uiColour; break;
                case 4: color = e.substance.conduitColour; break;
                case 5:
                    {
                        int hash = Hash.SDBMLower(e.name);
                        color = new Color32((byte)(hash & 0xFF), (byte)((hash >> 8) & 0xFF), (byte)((hash >> 16) & 0xFF), 0xFF);
                    } break;
            }

            //if (mod_MaterialColor_SimHashesExtension != null)
            //{
            //    color = (Color)mod_MaterialColor_SimHashesExtension_GetMaterialColorForType
            //        .Invoke(null, new object[] { e.id, "IceSculpture" });
            //}

            return color;
        }

        public static Color GetColorForBiome(ProcGen.SubWorld.ZoneType type)
        {
            int hash = Hash.SDBMLower(type.ToString());
            return new Color32((byte)(hash & 0xFF), (byte)((hash >> 8) & 0xFF), (byte)((hash >> 16) & 0xFF), 0xFF);
        }

        public static string GetFormattedMoles(float mass, GameUtil.TimeSlice timeSlice = GameUtil.TimeSlice.None, GameUtil.MetricMassFormat massFormat = GameUtil.MetricMassFormat.UseThreshold, bool includeSuffix = true, string floatFormat = "{0:0.#}")
        {
            string result;
            if (mass == -3.40282347E+38f)
            {
                result = STRINGS.UI.CALCULATING;
            }
            else
            {
                mass = GameUtil.ApplyTimeSlice(mass, timeSlice);
                string str;
                str = " Mmol";

                float num = Mathf.Abs(mass);
                if (0f < num)
                {
                    if (num < .005f)
                    {
                        mass *= 1000000f;
                        str = STRINGS.MATERIAL_PROBE.MOLE.MICRO;
                    }
                    else if (num < 5f)
                    {
                        mass *= 1000f;
                        str = STRINGS.MATERIAL_PROBE.MOLE.MILLI;
                    }
                    else if (Mathf.Abs(mass) < 5000f)
                    {
                        str = STRINGS.MATERIAL_PROBE.MOLE.UNIT;
                    }
                    else if (Mathf.Abs(mass) < 5000000f)
                    {
                        mass /= 1000f;
                        str = STRINGS.MATERIAL_PROBE.MOLE.KILO;
                    }
                    else
                    {
                        mass /= 1000000f;
                        str = STRINGS.MATERIAL_PROBE.MOLE.MEGA;
                    }
                }
                else
                {
                    str = STRINGS.MATERIAL_PROBE.MOLE.UNIT;
                }
                if (!includeSuffix)
                {
                    str = "";
                    timeSlice = GameUtil.TimeSlice.None;
                }
                result = GameUtil.AddTimeSliceText(string.Format(floatFormat, mass) + str, timeSlice);
            }
            return result;
        }

        static MethodInfo m_GameUtil_AddTemperatureUnitSuffix = AccessTools.Method(typeof(GameUtil), "AddTemperatureUnitSuffix", new Type[] { typeof(string) });
        public static string FormatTemp(double temp)
        {
            temp = GameUtil.GetConvertedTemperature((float)temp, false);
            string text = GameUtil.FloatToString((float)temp, "##0.000");
            return (string) m_GameUtil_AddTemperatureUnitSuffix.Invoke(null, new object[] { text });
        }

        public static string FormatMass(double mass)
        {
            return GameUtil.GetFormattedMass((float)mass, GameUtil.TimeSlice.None, GameUtil.MetricMassFormat.UseThreshold, true, mass < 5E-06f ? "{0:0.###}" : "{0:0.000}");
        }
    }

    #region JSON Stuff
    //Helper functions for JSON.Net
    public static class JsonExtensions
    {
        public static void KeepProperties(this JsonPropertyCollection props, Func<JsonProperty, Boolean> pred)
        {
            List<string> toRemove = new List<string>();
            foreach (var prop in props)
            {
                if (!pred(prop))
                    toRemove.Add(prop.PropertyName);
            }
            foreach (var propname in toRemove)
                props.Remove(propname);
        }
        public static void RemoveProperties(this JsonPropertyCollection props, Func<JsonProperty, Boolean> pred)
        {
            KeepProperties(props, prop => !pred(prop));
        }
        public static void KeepPropertiesByName(this JsonPropertyCollection props, Func<string, Boolean> pred)
        {
            KeepProperties(props, prop => pred(prop.UnderlyingName));
        }
        public static void RemovePropertiesByName(this JsonPropertyCollection props, Func<string, Boolean> pred)
        {
            KeepProperties(props, prop => !pred(prop.UnderlyingName));
        }
        public static void LiftProperties(this JsonPropertyCollection props, Func<JsonProperty, Boolean> pred)
        {
            List<JsonProperty> liftedProps = new List<JsonProperty>();
            List<JsonProperty> normalProps = new List<JsonProperty>();
            foreach (var prop in props)
                if (pred(prop))
                    liftedProps.Add(prop);
                else
                    normalProps.Add(prop);
            props.Clear();
            foreach (var prop in liftedProps) props.AddProperty(prop);
            foreach (var prop in normalProps) props.AddProperty(prop);
        }
        public static void AddPropertyAtStart(this JsonPropertyCollection props, JsonProperty newprop)
        {
            List<JsonProperty> savedProps = new List<JsonProperty>(props);
            props.Clear();
            props.AddProperty(newprop);
            foreach (var prop in savedProps) props.AddProperty(prop);
        }

        public static bool IsInstanceOfGenericType(this Type checkType, Type expectedType)
        {
            Type type = checkType;
            while (type != null)
            {
                if (type.IsGenericType &&
                    type.GetGenericTypeDefinition() == expectedType)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }
    //This class is for helping JSON.net serialize Unity/ONI objects.
    //(It's *super* useful for debugging to just dump a gameObject.)
    public class ONIJSONContractResolver : DefaultContractResolver
    {
        public class ConstValueProvider<PT> : IValueProvider
        {
            private PT value;

            public ConstValueProvider(PT value)
            {
                this.value = value;
            }

            public object GetValue(object target) { return value; }
            public void SetValue(object target, object value) { throw new NotImplementedException(); }

            public static JsonProperty CreateProperty(Type declaringType, string name, PT value)
            {
                JsonProperty prop = new JsonProperty();
                prop.PropertyName = name;
                prop.PropertyType = typeof(PT);
                prop.DeclaringType = declaringType;
                prop.ValueProvider = new ConstValueProvider<PT>(value);
                prop.Readable = true;
                prop.Writable = false;
                return prop;
            }
        }
        public class PredicateValueProvider<UT, PT> : IValueProvider
        {
            private Func<UT, PT> predicate;

            public PredicateValueProvider(Func<UT, PT> predicate)
            {
                this.predicate = predicate;
            }

            public object GetValue(object target) { return predicate((UT)target); }
            public void SetValue(object target, object value) { throw new NotImplementedException(); }

            public static JsonProperty CreateProperty(string name, Func<UT, PT> predicate)
            {
                JsonProperty prop = new JsonProperty();
                prop.PropertyName = name;
                prop.PropertyType = typeof(PT);
                prop.DeclaringType = typeof(UT);
                prop.ValueProvider = new PredicateValueProvider<UT, PT>(predicate);
                prop.Readable = true;
                prop.Writable = false;
                return prop;
            }
        }

        protected JsonContract ErrorContract(Type objectType, string err = "Serialization suppressed")
        {
            JsonObjectContract contract = new JsonObjectContract(objectType);

            contract.Properties.AddProperty(ConstValueProvider<string>.CreateProperty(objectType, "$typename", objectType.FullName));
            contract.Properties.AddProperty(ConstValueProvider<string>.CreateProperty(objectType, "$error", err));

            return contract;
        }
        protected override JsonContract CreateContract(Type objectType)
        {
            JsonContract contract = null;
            //if (objectType.Assembly.GetName().Name.StartsWith("Assembly-CSharp"))
            //{
            if (objectType.GetMethod("OnDeserialized", BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { }, null) != null
             || objectType.GetMethod("OnSerialized", BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { }, null) != null
             || objectType.GetMethod("OnSerializingMethod", BindingFlags.Instance | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, new Type[] { }, null) != null)
                return ErrorContract(objectType, "Type has funky serialization functions");
            //}
            if (typeof(KPrefabID) == objectType  //Has weird Deserialization function we can't easily work around.
             || typeof(Prioritizable) == objectType //Ditto.
             || objectType.IsAssignableFrom(typeof(Assignable))
             || objectType.Name == "Animator" //Throw lots of warnings. I just don't want to deal with it, and don't need this info.
             || objectType.Name == "HumanPose" //Ditto.
             || objectType.FullName.StartsWith("System.Action")
             || objectType.FullName.StartsWith("System.Func")
             || objectType.FullName.StartsWith("GameStateMachine")
             || objectType.FullName.StartsWith("Phonobox")
             || typeof(PrimaryElement) == objectType //More weird serialization functions. Some of these are kinda sad, actually.
             || typeof(Klei.AI.DiseaseInstance) == objectType
             || typeof(Storage) == objectType
             || typeof(MinionResume) == objectType
             || typeof(OccupyArea) == objectType
             || typeof(Klei.AI.Effects) == objectType
             || objectType.FullName.StartsWith("KAnim") //KAnim.Anim.Frame has a buggy Equals function, and I don't need it right now
             || typeof(Pickupable) == objectType //These throw NullReferenceExceptions, probably due to uninitialized state. They could probably be easily fixed.
             || typeof(MaterialSelectionPanel) == objectType
             || typeof(BuildingHP) == objectType
             || typeof(SimTemperatureTransfer) == objectType
             ) return ErrorContract(objectType);

            if (typeof(UnityEngine.Object).IsAssignableFrom(objectType))
            {
                //If we let base.CreateContract do this, it'll turn things like Transform
                //into arrays because they have IEnumerable
                contract = CreateObjectContract(objectType);
                contract.IsReference = true;
            }

            if (contract == null)
                contract = base.CreateContract(objectType);

            if (contract is JsonObjectContract)
            {
                var obcon = (JsonObjectContract)contract;
                bool includeTypename = true; //for some simple, struct-like types we suppress the typename

                if (typeof(UnityEngine.GameObject).IsAssignableFrom(objectType))
                {
                    obcon.Properties.Remove("gameObject");
                    obcon.Properties.AddPropertyAtStart(PredicateValueProvider<GameObject, List<Component>>.CreateProperty("components", delegate (GameObject go) {
                        List<Component> comps = new List<Component>();
                        foreach (Component comp in go.GetComponents(typeof(Component)))
                            if (!(comp is Transform))
                                comps.Add(comp);
                        return comps;
                    }));
                    obcon.Properties.AddPropertyAtStart(PredicateValueProvider<GameObject, List<GameObject>>.CreateProperty("children", delegate (GameObject go) {
                        List<GameObject> children = new List<GameObject>();
                        Transform trans = go.transform;
                        for (int i = 0; i < trans.childCount; i++)
                            children.Add(trans.GetChild(i).gameObject);
                        return children;
                    }));
                }
                if (typeof(Component).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "gameObject"
                     || name == "transform");
                }
                if (typeof(Transform).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "parent"
                     || name == "root"
                     || name == "forward"
                     || name == "right"
                     || name == "up"
                     || name == "location"
                     || name == "rotation"
                     || name == "eulerAngles"
                     || name == "lossyScale"
                     || name == "worldToLocalMatrix"
                     || name == "localToWorldMatrix"
                     || name == "hierarchyCapacity"
                     || name == "hierarchyCount"
                     || name == "childCount");
                    obcon.Properties.AddPropertyAtStart(PredicateValueProvider<Transform, List<GameObject>>.CreateProperty("children", delegate (Transform trans) {
                        List<GameObject> children = new List<GameObject>();
                        for (int i = 0; i < trans.childCount; i++)
                            children.Add(trans.GetChild(i).gameObject);
                        return children;
                    }));
                }
                if (typeof(RectTransform).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "anchoredPosition3D");
                }
                if (typeof(Sprite).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "vertices"
                     || name == "triangles"
                     || name == "uv");
                }
                if (typeof(Mesh).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "bindposes"
                     || name == "boneWeights"
                     || name == "colors"
                     || name == "colors32"
                     || name == "normals"
                     || name == "tangents"
                     || name == "triangles"
                     || name == "uv"
                     || name == "uv2"
                     || name == "uv3"
                     || name == "uv4"
                     || name == "uv5"
                     || name == "uv6"
                     || name == "uv7"
                     || name == "uv8"
                     || name == "vertices");
                }
                if (typeof(Canvas).IsAssignableFrom(objectType))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "rootCanvas");
                }

                if (objectType.FullName.StartsWith("UnityEngine.Vector") || objectType == typeof(Quaternion))
                {
                    obcon.Properties.KeepPropertiesByName(name =>
                        name == "x"
                     || name == "y"
                     || name == "z"
                     || name == "w");
                    includeTypename = false;
                }
                if (objectType.FullName.StartsWith("UnityEngine.Color"))
                {
                    obcon.Properties.KeepPropertiesByName(name =>
                        name == "r"
                     || name == "g"
                     || name == "b"
                     || name == "a");
                    includeTypename = false;
                }
                else if (objectType == typeof(Bounds))
                {
                    obcon.Properties.KeepPropertiesByName(name =>
                        name == "center"
                     || name == "size");
                    includeTypename = false;
                }
                else if (objectType == typeof(Rect))
                {
                    obcon.Properties.KeepPropertiesByName(name =>
                        name == "position"
                     || name == "size");
                    includeTypename = false;
                }
                else if (objectType == typeof(Matrix4x4))
                {
                    obcon.Properties.KeepPropertiesByName(name => name.Length == 3 && name[0] == 'm');
                    includeTypename = false;
                }
                //else if (objectType == typeof(TMPro.TMP_FontAsset))
                //{
                //    obcon.Properties.RemovePropertiesByName(name =>
                //        name == "atlas"
                //     || name == "fallbackFontAssets"
                //     || name == "fontWeights"
                //     || name == "characterDictionary"
                //     || name == "kerningDictionary"
                //     || name == "material"
                //     || name == "characterInfo"
                //     || name == "wordInfo"
                //     || name == "linkInfo"
                //     || name == "lineInfo"
                //     || name == "pageInfo"
                //     || name == "meshInfo"
                //     || name == "kerningInfo");
                //}
                //else if (objectType == typeof(TMPro.TMP_TextInfo))
                //{
                //    obcon.Properties.RemovePropertiesByName(name =>
                //        name == "characterInfo"
                //     || name == "wordInfo"
                //     || name == "linkInfo"
                //     || name == "lineInfo"
                //     || name == "pageInfo"
                //     || name == "meshInfo");
                //}
                else if (objectType == typeof(LocText))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "canvasRenderer"
                     || name == "materialForRendering"
                     || name == "fontSharedMaterial"
                     || name == "fontSharedMaterials"
                     || name == "fontMaterial"
                     || name == "fontMaterials"
                     || name == "defaultMaterial"
                     || name == "material"
                     || name == "mainTexture");
                }
                else if (objectType == typeof(Material))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "color" //this property is just a shortcut for GetColor("_Color"), but not all materials have this
                     || name == "mainTexture" //basically the same bit with these.
                     || name == "mainTextureOffset"
                     || name == "mainTextureScale");

                }
                else if (objectType == typeof(Element))
                {
                    contract.IsReference = true;
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "lowTempTransition" //just a reference to the same thing as lowTempTransitionTarget
                     || name == "highTempTransition"
                     || name == "HasTransitionUp");
                    obcon.Properties.AddProperty(PredicateValueProvider<Element, int>.CreateProperty("lowTempTransitionIndex", delegate (Element v) {
                        return v.lowTempTransition != null ? SimMessages.GetElementIndex(v.lowTempTransition.id) : -1;
                    }));
                    obcon.Properties.AddProperty(PredicateValueProvider<Element, int>.CreateProperty("highTempTransitionIndex", delegate (Element v) {
                        return v.highTempTransition != null ? SimMessages.GetElementIndex(v.highTempTransition.id) : -1;
                    }));
                }
                else if (objectType == typeof(Klei.AI.Attribute))
                {
                    contract.IsReference = true;
                }
                else if (typeof(StateMachine.BaseState).IsAssignableFrom(objectType))
                {
                    contract.IsReference = true;
                }
                else if (objectType.IsInstanceOfType(typeof(StateMachine<,,,>.GenericInstance)))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "master");
                }
                else if (objectType == typeof(ChoreProvider))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "chores");
                }
                else if (objectType == typeof(ToolMenu.ToolInfo))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "collection");
                }
                else if (objectType == typeof(BuildingDef))
                {
                    obcon.Properties.RemovePropertiesByName(name =>
                        name == "Def");
                }

                obcon.Properties.LiftProperties(prop => prop.UnderlyingName == "name");
                if (includeTypename)
                    obcon.Properties.AddPropertyAtStart(ConstValueProvider<string>.CreateProperty(objectType, "$typename", objectType.FullName));
            }

            return contract;
        }
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            JsonProperty prop = base.CreateProperty(member, memberSerialization);

            if (prop.DeclaringType == typeof(Vector2) || prop.DeclaringType == typeof(Vector3) || prop.DeclaringType == typeof(Vector4))
            {
                prop.ShouldSerialize = i =>
                    prop.UnderlyingName == "x" ||
                    prop.UnderlyingName == "y" ||
                    prop.UnderlyingName == "z" ||
                    prop.UnderlyingName == "w";
            }
            else if (prop.DeclaringType == typeof(Color) || prop.DeclaringType == typeof(Color32))
            {
                prop.ShouldSerialize = i =>
                    prop.UnderlyingName == "r" ||
                    prop.UnderlyingName == "g" ||
                    prop.UnderlyingName == "b" ||
                    prop.UnderlyingName == "a";
            }
            else if (prop.DeclaringType == typeof(Bounds))
            {
                prop.ShouldSerialize = i =>
                    prop.UnderlyingName == "center" ||
                    prop.UnderlyingName == "size";
            }
            else if (prop.DeclaringType == typeof(Rect))
            {
                prop.ShouldSerialize = i =>
                    prop.UnderlyingName == "position" ||
                    prop.UnderlyingName == "size";
            }

            return prop;
        }

        public static string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { ContractResolver = new ONIJSONContractResolver() });
        }
    }
    #endregion

    //I'm just going to come out and say it: this class is sloppy.
    public static class MaterialProber
    {
        public static OverlayMode mode = OverlayMode.MASS;
        public static bool matchElement = false;
        public static bool matchPhase = true;
        public static bool matchConstructed = false;
        public static bool matchBiome = false;
        public static bool ignoreUnusual = true;
        public static int range = 25;

        public static int originCell;
        public static HashSet<int> touchedCells = new HashSet<int>();
        public static Dictionary<uint, float> valueByElement = new Dictionary<uint, float>();
        public static List<float> valueList = new List<float>();
        //public static HashSet<int> spaceCells = new HashSet<int>();
        public static Element touchedElement = null;
        public static byte touchedDisease = 0xFF;
        public static ProcGen.SubWorld.ZoneType touchedBiome = ProcGen.SubWorld.ZoneType.Space;
        public static bool blankedElement;
        public static bool negligbleRange;
        public static int cellCount;
        public static double valueAvg, valueTotal;
        public static double true_min, true_max;
        public static double vari, sdv, sdv_n2, sdv_p2, sdv_n1, sdv_p1;

        public static double totalThermalEnergy;

        public static int rangeMode = 0;
        public static double Min { get {
            return rangeMode == 0 ? true_min :
                   rangeMode == 1 ? sdv_n2   :
                                    sdv_n1   ;
        } }
        public static double Max { get {
            return rangeMode == 0 ? true_max :
                   rangeMode == 1 ? sdv_p2   :
                                    sdv_p1   ;
        } }
        public static double Range { get {
            return rangeMode == 0 ? true_max - true_min :
                   rangeMode == 1 ? sdv_p2   - sdv_n2   :
                                    sdv_p1   - sdv_n1   ;
        } }
        public static LocString RangeModeName { get {
            return rangeMode == 0 ? STRINGS.MATERIAL_PROBE.RANGEMODE_TRUE :
                   rangeMode == 1 ? STRINGS.MATERIAL_PROBE.RANGEMODE_SDV2 :
                                    STRINGS.MATERIAL_PROBE.RANGEMODE_SDV1;
        } }

        public static void DoProbe(int originCell)
        {
            Clear();
            MaterialProber.originCell = originCell;

            touchedElement = Grid.Element[originCell];
            touchedBiome = Game.Instance.world.zoneRenderData.GetSubWorldZoneType(originCell);
            //A blanked element is one we don't show statistics for
            blankedElement = mode != OverlayMode.GERMS && (touchedElement.IsVacuum || touchedElement.id == SimHashes.Unobtanium);
            
            HashSet<int> visited_cells = new HashSet<int>();
            
            if (mode == OverlayMode.GERMS)
            {
                if (Grid.DiseaseCount[originCell] > 0)
                {
                    bool org_solid = Grid.Solid[originCell];
                    bool org_gas = touchedElement.IsGas;
                    touchedDisease = Grid.DiseaseIdx[originCell];
                    GameUtil.FloodFillConditional(originCell, (
                        (int fcell) =>
                            Grid.IsValidCell(fcell) &&
                            Grid.IsVisible(fcell) &&
                            Grid.DiseaseCount[fcell] > 0 &&
                            Grid.DiseaseIdx[fcell] != (byte)0xFF &&
                            (!matchElement || Grid.DiseaseIdx[fcell] == touchedDisease) &&
                            (!matchPhase || Grid.Solid[fcell] == org_solid) &&
                            (!matchPhase || Grid.IsGas(fcell) == org_gas) &&
                            (!matchBiome || Game.Instance.world.zoneRenderData.GetSubWorldZoneType(fcell) == touchedBiome) &&
                            Grid.GetCellRange(fcell, originCell) < range
                    ), visited_cells, touchedCells);
                }
            }
            else if (mode == OverlayMode.MASS || mode == OverlayMode.TEMPERATURE)
            {
                bool org_solid = Grid.Solid[originCell];
                bool org_gas = touchedElement.IsGas || touchedElement.IsVacuum;
                bool org_foundation = Grid.Foundation[originCell];
                GameUtil.FloodFillConditional(originCell, (
                    (int fcell) =>
                        Grid.IsValidCell(fcell) &&
                        Grid.IsVisible(fcell) &&
                        (!matchElement || Grid.Element[fcell] == touchedElement) &&
                        (!matchPhase || (
                            Grid.Solid[fcell] == org_solid &&
                            (Grid.IsGas(fcell) == org_gas || (Grid.Element[fcell].IsVacuum && org_gas))
                        )) &&
                        (!matchBiome || Game.Instance.world.zoneRenderData.GetSubWorldZoneType(fcell) == touchedBiome) &&
                        (!ignoreUnusual || (
                            Grid.Element[fcell].id != SimHashes.Katairite &&
                            Grid.Element[fcell].id != SimHashes.Unobtanium
                        )) &&
                        //Foundation cells are things like basic tiles and airflow tiles.
                        //For solids, we'd like to consider foundation and non-foundation tiles separately.
                        //This allows you to probe natural tiles separately from built ones.
                        //For gases and liquids, we don't care.
                        (!matchConstructed || !touchedElement.IsSolid || Grid.Foundation[fcell] == org_foundation) &&
                        Grid.GetCellRange(fcell, originCell) < range
                ), visited_cells, touchedCells);
            }
            else if (mode == OverlayMode.BIOME)
            {
                //GameUtil.FloodFillConditional(originCell, (
                //    (int fcell) =>
                //        Grid.IsValidCell(fcell) &&
                //        Grid.IsVisible(fcell) &&
                //        Grid.GetCellRange(fcell, originCell) < range
                //), visited_cells, touchedCells);
            }

            if (touchedCells.Count > 0)
            {
                //float totalTemp = 0;

                foreach (var cellI in touchedCells)
                {
                    cellCount++;
                    if (mode == OverlayMode.GERMS)
                    {
                        var celldis = Grid.DiseaseIdx[cellI];
                        var cellgerms = Grid.DiseaseCount[cellI];
                        
                        if (valueByElement.ContainsKey(celldis))
                            valueByElement[celldis] = valueByElement[celldis] + cellgerms;
                        else
                            valueByElement[celldis] = cellgerms;
                        
                        valueList.Add(cellgerms);
                    }
                    else
                    {
                        var cellelem = Grid.Element[cellI];
                        var cellidx = SimMessages.GetElementIndex(cellelem.id);
                        var cellmass = Grid.Mass[cellI];
                        var celltemp = Grid.Temperature[cellI];
                        var cellvalue = mode == OverlayMode.MASS ? cellmass : celltemp;

                        if ((!cellelem.IsVacuum) && cellelem.id != SimHashes.Unobtanium)
                        {
                            if (valueByElement.ContainsKey((uint)cellidx))
                                valueByElement[(uint)cellidx] = valueByElement[(uint)cellidx] + cellvalue;
                            else
                                valueByElement[(uint)cellidx] = cellvalue;

                            totalThermalEnergy += (double)celltemp * (double)cellmass * (double)cellelem.specificHeatCapacity;

                            valueList.Add(cellvalue);
                        }
                    }


                    //if (Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cellI) == ProcGen.SubWorld.ZoneType.Space)
                    //    spaceCells.Add(cellI);
                }

                if (valueList.Count > 0)
                {
                    for (int i = 0; i < valueList.Count; i++)
                    {
                        float v = valueList[i];
                        valueTotal += v;

                        if (i == 0)
                        {
                            true_min = true_max = v;
                        }
                        else
                        {
                            if (v < true_min) true_min = v;
                            if (v > true_max) true_max = v;
                        }
                    }
                    valueAvg = valueTotal / valueList.Count;

                    double vsdvAccum = 0f;
                    for (int i = 0; i < valueList.Count; i++)
                    {
                        double v = valueList[i] - valueAvg;
                        vsdvAccum += v*v;
                    }
                    vari = vsdvAccum/valueList.Count;
                    sdv = Math.Sqrt(vari);
                    sdv_n1 = valueAvg - sdv;
                    sdv_p1 = valueAvg + sdv;
                    sdv_n2 = valueAvg - sdv * 2;
                    sdv_p2 = valueAvg + sdv * 2;

                    negligbleRange = (true_max - true_min) / valueAvg < 0.005f;
                }
                else
                {
                    negligbleRange = true;
                }
                
            }
        }

        internal static void Clear()
        {
            touchedCells.Clear();
            valueByElement.Clear();
            valueList.Clear();
            //spaceCells.Clear();
            touchedDisease = (byte)0xFF;
            touchedElement = null;
            touchedBiome = ProcGen.SubWorld.ZoneType.Space;
            cellCount = 0;

            valueAvg = valueTotal = 0;
            true_min = true_max = 0;
            vari = sdv = sdv_n2 = sdv_p2 = sdv_n1 = sdv_p1 = 0;
            totalThermalEnergy = 0;
        }
    }

    ////Hide our weird Action: it doesn't have a binding string
    //[HarmonyPatch(typeof(GameUtil), "GetActionString")]
    //public static class GameUtil_GetActionString_Patch
    //{
    //    public static bool Prefix(Action action, ref string __result)
    //    {
    //        if (action == PatchCommon.Action_MaterialProbeMap)
    //        {
    //            __result = string.Empty;
    //            return false;
    //        }
    //        return true;
    //    }
    //}
    ////Hide our weird Action: it can't be consumed
    //[HarmonyPatch(typeof(KButtonEvent), "TryConsume")]
    //public static class KButtonEvent_TryConsume_Patch
    //{
    //    public static bool Prefix(KButtonEvent __instance, Action action, ref bool __result)
    //    {
    //        if (action == PatchCommon.Action_MaterialProbeMap)
    //        {
    //            //__instance.Consumed = true;
    //            __result = __instance.Consumed;
    //            return false;
    //        }
    //        return true;
    //    }
    //}

    //Add our icon to the overlay menu
    [HarmonyPatch(typeof(OverlayMenu), "InitializeToggles")]
    public static class OverlayMenu_InitializeToggles_Patch
    {
        public static void Postfix(OverlayMenu __instance, List<KIconToggleMenu.ToggleInfo> ___overlayToggleInfos)
        {
            Type oti = AccessTools.Inner(typeof(OverlayMenu), "OverlayToggleInfo");

            ConstructorInfo ctor = AccessTools.Constructor(oti, new Type[] { typeof(string), typeof(string), typeof(HashedString), typeof(string), typeof(global::Action), typeof(string), typeof(string) });
            var menu = (KIconToggleMenu.ToggleInfo)ctor.Invoke(new object[] {
                        (string)STRINGS.MATERIAL_PROBE.BUTTON, //text
                        "overlay_materialprobe", //icon name
                        MaterialProbeMode.ID, //sim_view id
                        string.Empty, //No required tech
                        global::Action.NumActions, //action
                        (string)STRINGS.MATERIAL_PROBE.OVERLAYSTRING, //tooltip
                        (string)STRINGS.MATERIAL_PROBE.BUTTON //tooltip header
                });
            menu.getSpriteCB = GetUISprite;

            ___overlayToggleInfos.Add(menu);
        }

        private static Sprite GetUISprite()
        {
            return Assets.GetSprite("sample");//PatchCommon.LoadSpriteFromFile(MaterialProbeModMain.ModPath + "icon.png");
        }
    }

    //Register the new overlay mode
    [HarmonyPatch(typeof(OverlayScreen), "RegisterModes")]
    public static class OverlayScreen_RegisterModes_Patch
    {
        public static void Postfix(OverlayScreen __instance)
        {
            OverlayModes.Mode mode = new MaterialProbeMode();
            MethodInfo mi = AccessTools.Method(typeof(OverlayScreen), "RegisterMode", new Type[] { typeof(OverlayModes.Mode) });
            mi.Invoke(__instance, new object[] { mode });
        }
    }
    public class MaterialProbeMode : OverlayModes.Mode
    {
        public override HashedString ViewMode() { return ID; }
        public override string GetSoundName() { return "Rooms"; }
        public static readonly HashedString ID = "MaterialProbeMap";
    }

    public class MaterialProbeDiagram : MonoBehaviour
    {
        private static GameObject CreateMaterialProbeDiagram()
        {
            //Ugh, creating prefabs in code is *awful*.
            //Especially because I can't find any UI prefabs in the game. Or, at least, not the ones I need.
            //Anyway, we turn this GameObject into a simple UI here. It's just an empty box, but we can insert it into the UI tree for OnGUI to use.
            GameObject root = new GameObject("MaterialProbeDiagram");
            root.AddComponent<CanvasRenderer>();
            var root_layout = root.AddComponent<LayoutElement>();
            root_layout.minWidth = 286;
            root_layout.minHeight = 206;
            root.AddComponent<VerticalLayoutGroup>();

            GameObject bg = new GameObject("Background");
            bg.AddComponent<CanvasRenderer>();
            var bg_image = bg.AddComponent<Image>(); //just attaches a white background for now
            bg_image.color = new Color32(215, 215, 215, 0);
            bg.AddComponent<HorizontalLayoutGroup>();

            //Add this component so OnGUI works. The background is now our "anchor box".
            bg.AddComponent<MaterialProbeDiagram>();

            bg.transform.SetParent(root.transform);

            return root;
        }
        private static GameObject Prefab_;

        //to be used with Util.KInstantiateUI
        public static GameObject Prefab { get {
                if (Prefab_ == null) Prefab_ = CreateMaterialProbeDiagram();
                return Prefab_;
            } }
        
        //Gets the screen position of a RectTransform. UI objects are located in Canvas space, but OnGUI (IMGUI) uses screen space.
        public static Rect RectTransformToScreenSpace(RectTransform rxf, Canvas canvas)
        {
            var crxf = (RectTransform)canvas.transform;
            Vector2 csize = Vector2.Scale(crxf.rect.size, crxf.lossyScale);
            Vector2 size = Vector2.Scale(rxf.rect.size, rxf.lossyScale);
            Rect rect = new Rect(rxf.position.x, csize.y - rxf.position.y, size.x, size.y);
            rect.x -= (rxf.pivot.x * size.x);
            rect.y -= ((1.0f - rxf.pivot.y) * size.y);
            return rect;
        }
        //protected void OnRectTransformDimensionsChange()
        //{
        //    var rt = (RectTransform)this.transform;
        //    var canvas = GetComponentInParent<Canvas>();
        //    Rect area = RectTransformToScreenSpace(rt, canvas);
        //    Debug.Log("GUI Area:"+ONIJSONContractResolver.Serialize(area));
        //}

        //bool debugHeight = false;
        string typedRange = null;
        //Called automatically by Unity to draw an IMGUI.
        public void OnGUI()
        {
            //All parts of ONI's UI should have a Canvas as an ancestor.
            var canvas = GetComponentInParent<Canvas>();
            //Try to suppress drawing while the "anchor box" is not visible. Doesn't always seem to work.
            if (isActiveAndEnabled && gameObject.activeInHierarchy && canvas != null && !DebugHandler.HideUI)
            {
                var rt = (RectTransform)this.transform;
                //the area of the anchor box is the area we want to draw IMGUI in.
                Rect area = RectTransformToScreenSpace(rt, canvas);
                //Debug.Log("GUI Size:" + ONIJSONContractResolver.Serialize(canvas.rectTransform().rect.size));
                //Debug.Log("GUI Area:" + ONIJSONContractResolver.Serialize(area));
                //area.y += area.height;

                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                GUI.enabled = true;
                GUILayout.BeginArea(area);

                GUI.enabled = MaterialProber.mode != OverlayMode.BIOME;
                MaterialProber.matchElement     = GUILayout.Toggle(MaterialProber.matchElement    , STRINGS.MATERIAL_PROBE.MATCH_ELEMENT);
                MaterialProber.matchPhase       = GUILayout.Toggle(MaterialProber.matchPhase      , STRINGS.MATERIAL_PROBE.MATCH_PHASE);
                MaterialProber.matchConstructed = GUILayout.Toggle(MaterialProber.matchConstructed, STRINGS.MATERIAL_PROBE.MATCH_CONSTRUCTED);
                MaterialProber.matchBiome       = GUILayout.Toggle(MaterialProber.matchBiome      , STRINGS.MATERIAL_PROBE.MATCH_BIOME);
                GUI.enabled = MaterialProber.mode != OverlayMode.BIOME && MaterialProber.mode != OverlayMode.GERMS;
                MaterialProber.ignoreUnusual    = GUILayout.Toggle(MaterialProber.ignoreUnusual   , STRINGS.MATERIAL_PROBE.IGNORE_UNUSUAL);

                GUI.enabled = true;
                GUILayout.BeginHorizontal();
                    GUILayout.Label(STRINGS.MATERIAL_PROBE.RANGE_FIELD, GUILayout.ExpandWidth(false));


                    GUI.SetNextControlName("RangeTextField");
                    typedRange = GUILayout.TextField(typedRange != null ? typedRange : MaterialProber.range.ToString(), GUILayout.ExpandWidth(false), GUILayout.MinWidth(40));
                    try {
                        MaterialProber.range = int.Parse(typedRange);
                        typedRange = null;
                    } catch (FormatException) { }
                    if (GUI.GetNameOfFocusedControl() != "RangeTextField")
                        typedRange = null;
                    
                    const float adjustWidth = 16;
                    if (GUILayout.Button("-", GUILayout.Width(adjustWidth)))
                        MaterialProber.range = MaterialProber.range - (shiftHeld ? 5 : 1);
                    
                    if (MaterialProber.range < 1) MaterialProber.range = 1;

                    MaterialProber.range = (int) GUILayout.HorizontalSlider(MaterialProber.range, 1, 100, GUILayout.ExpandWidth(true));

                    if (GUILayout.Button("+", GUILayout.Width(adjustWidth)))
                        MaterialProber.range = MaterialProber.range + (shiftHeld ? 5 : 1);

                    if (MaterialProber.range < 1) MaterialProber.range = 1;
                GUILayout.EndHorizontal();

                if (GUILayout.Button(STRINGS.MATERIAL_PROBE.COLOR_PALETTE + PatchCommon.colorPaletteNames[PatchCommon.colorPalette]))
                {
                    if (shiftHeld)
                        PatchCommon.colorPalette = (PatchCommon.colorPalette + 1) % PatchCommon.colorPaletteNames.Length;
                    else
                        PatchCommon.colorPalette = (PatchCommon.colorPalette + 1) % 2;
                }
                if (GUILayout.Button(STRINGS.MATERIAL_PROBE.RANGEMODE_LABEL + MaterialProber.RangeModeName))
                {
                    MaterialProber.rangeMode = (MaterialProber.rangeMode + 1) % 3;
                }
                GUILayout.BeginHorizontal();
                    GUILayout.Label(STRINGS.MATERIAL_PROBE.MODE_LABEL, GUILayout.ExpandWidth(false));
                    if (GUILayout.Toggle(MaterialProber.mode == OverlayMode.MASS       , STRINGS.MATERIAL_PROBE.MODE_MASS , GUI.skin.button)) MaterialProber.mode = OverlayMode.MASS       ;
                    if (GUILayout.Toggle(MaterialProber.mode == OverlayMode.TEMPERATURE, STRINGS.MATERIAL_PROBE.MODE_TEMP , GUI.skin.button)) MaterialProber.mode = OverlayMode.TEMPERATURE;
                    if (GUILayout.Toggle(MaterialProber.mode == OverlayMode.GERMS      , STRINGS.MATERIAL_PROBE.MODE_GERMS, GUI.skin.button)) MaterialProber.mode = OverlayMode.GERMS      ;
                    if (GUILayout.Toggle(MaterialProber.mode == OverlayMode.BIOME      , STRINGS.MATERIAL_PROBE.MODE_BIOME, GUI.skin.button)) MaterialProber.mode = OverlayMode.BIOME      ;
                GUILayout.EndHorizontal();

                //if (Event.current.type == EventType.Repaint && !debugHeight)
                //{
                //    var r = GUILayoutUtility.GetLastRect();
                //    Debug.Log("Layout " + (r.yMax));
                //    debugHeight = true;
                //}

                GUILayout.EndArea();
            }
        }
    }

    //This patch is called when the OverlayLegend is spawned. This is where we insert our overlay button.
    [HarmonyPatch(typeof(OverlayLegend), "OnSpawn")]
    public static class OverlayLegend_OnSpawn_Patch
    {
        #region Patches for QoL2
        static CodeInstruction labelStealer0Target = new CodeInstruction(OpCodes.Ldarg_0);

        static QuickPatcher.IPatcher[] patchers = new QuickPatcher.IPatcher[] {
            new QuickPatcher.SimpleMatchInsertPatcher(
                //Match against: this.ClearLegend();
                //This should be a safe target. Match starts at 84 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OverlayLegend), "ClearLegend")),
                },
                //Insert: OverlayLegend_OnSpawn_Patch.Hook_OnSpawn_PreinfoListInit(this, this.overlayInfoList);
                new CodeInstruction[]
                {
                    labelStealer0Target,
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(OverlayLegend), "overlayInfoList")),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OverlayLegend_OnSpawn_Patch), "Hook_OnSpawn_PostInfoListLocalize")),
                },
                false //before
            ),

            new QuickPatcher.LabelStealerPatcher(
                //Match against: this.ClearLegend();
                //Yes, this is actually targetting the same code as the last patch. This should be fine, since
                //we inserted the last patch before the match. Match starts at 84 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OverlayLegend), "ClearLegend")),
                }
            ) { Target = labelStealer0Target },
        };
        
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instr)
        {
            return QuickPatcher.ApplyPatches(original, instr, patchers);
        }
        #endregion

        //Called after OnSpawn's localization pass, since it won't recognize our strings.
        public static void Hook_OnSpawn_PostInfoListLocalize(OverlayLegend legend, List<OverlayLegend.OverlayInfo> ___overlayInfoList)
        {
            //info is a struct, there is no real initialization.
            OverlayLegend.OverlayInfo info = new OverlayLegend.OverlayInfo();
            info.name = "MATERIAL PROBE";
            info.mode = MaterialProbeMode.ID;
            info.infoUnits = new List<OverlayLegend.OverlayInfoUnit>(); //Fill this with OverlayInfoUnits to make your legend! Or do it programatically!
            //info.diagrams = new List<GameObject>(); //any objects here will cloned and added to the bottom of the legend with KInstantiateUI (doesn't work if `isProgrammaticallyPopulated == true`!)
            info.isProgrammaticallyPopulated = true; //If you set true here, you have to populate the units manually in OverlayLegend.SetLegend. (A postfix should work.)

            ___overlayInfoList.Add(info);
        }

        //I do some object dumping here for debugging, but otherwise this method isn't important.
        public static void Postfix(OverlayLegend __instance, List<OverlayLegend.OverlayInfo> ___overlayInfoList)
        {
            //List<string> derp = new List<string>();
            //foreach (var prf in Assets.Prefabs)
            //    derp.Add(prf.GetDebugName());
            //Debug.Log(ONIJSONContractResolver.Serialize(derp));

            //Dump all gameobjects. This is helpful for dissecting UIs
            /*
            {
                //this is such a ridiculously huge list that serialization runs out of memory, break it up
                var allgo = Resources.FindObjectsOfTypeAll<GameObject>();
                List<GameObject> rlist = new List<GameObject>();
                foreach (var go in allgo)
                {
                    if (go.transform.parent == null)
                    {
                        rlist.Add(go);
                        if (rlist.Count >= 200)
                        {
                            Debug.Log(ONIJSONContractResolver.Serialize(rlist));
                            rlist.Clear();
                        }
                    }
                }
                if (rlist.Count > 0)
                    Debug.Log(ONIJSONContractResolver.Serialize(rlist));
            }
            */

            //Debug.Log(ONIJSONContractResolver.Serialize(___overlayInfoList)); //dump the existing overlay list to help build the new overlays
            //var f = AccessTools.Field(typeof(OverlayLegend), "toolParameterMenuPrefab");
            //GameObject gameObject = Util.KInstantiateUI((GameObject)f.GetValue(OverlayLegend.Instance), null, false);
            //
            //Debug.Log(ONIJSONContractResolver.Serialize(___overlayInfoList[11])); //dump the existing overlay list to help build the new overlays
            //Debug.Log(ONIJSONContractResolver.Serialize(___overlayInfoList[16])); //dump the existing overlay list to help build the new overlays


            //Debug.Log(ONIJSONContractResolver.Serialize(ElementLoader.elements)); //want a list of all elements and their properties in json?

            //This dumps a tab-separated list of all sprites known by Assets.GetSprite(name), with their name, texture hash, x, y, width, height.
            //The texture hash is not useful by itself, and may change on each run of the game, but it will be the same for each sprite in the
            //same sprite texture. (It's the texture's Object InstanceID.) With a little work, and by extracting the main hud textures from the
            //game, you can figure out the name of any sprite on that list. The y value returned is the *bottom* edge of the sprite.
            //var sprites = new List<Sprite>(Assets.Sprites.Values);
            //string str = "";
            //foreach (var s in sprites) str += string.Format("{0}\t{1:x8}\t{2}\t{3}\t{4}\t{5}\n",
            //    s.name, s.texture.GetInstanceID(), s.textureRect.x, s.texture.height-s.textureRect.y, s.textureRect.width, s.textureRect.height
            //);
            //Debug.Log(str);
        }
    }


    //This patch is called to set the legend for an overlay. The legend is the series of colored boxes that
    //each legend uses as a "color key". For instance, on the oxygen overlay, there are keys for "Very Breathable",
    //and "Unbreathable"
    [HarmonyPatch(typeof(OverlayLegend), "SetLegend", new Type[] { typeof(OverlayLegend.OverlayInfo) })]
    public static class OverlayLegend_SetLegend_Patch
    {
        private static FieldInfo OverlayLegend_activeUnitsParent_Field = AccessTools.Field(typeof(OverlayLegend), "activeUnitsParent");
        private static FieldInfo OverlayLegend_activeUnitObjs_Field = AccessTools.Field(typeof(OverlayLegend), "activeUnitObjs");

        //Sets up the GameObject received from OverlayLegend.GetFreeUnitObject(), and fetches out some of its components.
        public static GameObject UnitInit(GameObject unitObj, out LocText text, out Image icon, out ToolTip tooltip)
        {
            var activeUnitsParent = (GameObject)OverlayLegend_activeUnitsParent_Field.GetValue(OverlayLegend.Instance);
            text = unitObj.GetComponentInChildren<LocText>();
                text.enabled = true;
                //text.text = "No Text";
            icon = unitObj.transform.Find("Icon").GetComponentInChildren<Image>();
                icon.gameObject.SetActive(true);
                icon.sprite = Assets.instance.LegendColourBox;
                //icon.color = Color.white;
                icon.enabled = true;
                icon.type = Image.Type.Simple;
            tooltip = unitObj.GetComponent<ToolTip>();
                tooltip.enabled = true;
                //tooltip.toolTip = "No Tooltip";
            
            unitObj.SetActive(true);
            unitObj.transform.SetParent(activeUnitsParent.transform);
            return unitObj;
        }
        //Fetches some components out of a GameObject received from OverlayLegend.activeUnitObjs.
        public static GameObject UnitFetch(GameObject unitObj, out LocText text, out Image icon, out ToolTip tooltip)
        {
            text = unitObj.GetComponentInChildren<LocText>();
            icon = unitObj.transform.Find("Icon").GetComponentInChildren<Image>();
            tooltip = unitObj.GetComponent<ToolTip>();
            return unitObj;
        }
        //Removes the alpha from a color by multiplying the color by the alpha, then setting the alpha to one
        public static Color AlmulColor(Color color)
        {
            return new Color(color.r * color.a, color.g * color.a, color.b * color.a, 1.0f);
        }
        //Sets up our legend when it's first needed, i.e., initializes the legend.
        public static void DoLegendUpdate(OverlayLegend legend)
        {
            //Assuming ClearLegend has been called. Also assuming we're in the right mode
            GameObject unitObj; LocText text; Image icon; ToolTip tooltip;
            //Because resetting OverlayLegend is hard, we have to reserve the number of units we need now.
            //This could be fixed in the future, but requires some digging.
            unitObj = UnitInit(legend.GetFreeUnitObject(), out text, out icon, out tooltip);
            unitObj = UnitInit(legend.GetFreeUnitObject(), out text, out icon, out tooltip);
            QuickUpdate(legend);
        }
        //Updates our legend *after* it has been initialized. You can call this as much as you want.
        public static void QuickUpdate(OverlayLegend legend = null)
        {
            if (legend == null) legend = OverlayLegend.Instance;
            var activeUnitObjs = (List<GameObject>)OverlayLegend_activeUnitObjs_Field.GetValue(legend);
            
            GameObject unitObj; LocText text; Image icon; ToolTip tooltip; Color color;
            switch (MaterialProber.mode)
            {
                case OverlayMode.MASS:
                    if (MaterialProber.touchedElement == null)
                        return;
                    color = PatchCommon.GetColorForElement(MaterialProber.touchedElement);

                    unitObj = UnitFetch(activeUnitObjs[0], out text, out icon, out tooltip);
                    //text.text = "Greatest Density";
                    text.text = string.Format(">= {0} {1}", PatchCommon.FormatMass(MaterialProber.Max), MaterialProber.touchedElement.name);
                    icon.color = AlmulColor(Color.Lerp(color, SimDebugView_Ctor_Patch.col_dense, SimDebugView_Ctor_Patch.cint_dens));
                    tooltip.toolTip = "Cell in the highlighted region with greatest mass";

                    unitObj = UnitFetch(activeUnitObjs[1], out text, out icon, out tooltip);
                    //text.text = "Least Density";
                    text.text = string.Format("<= {0} {1}", PatchCommon.FormatMass(MaterialProber.Min), MaterialProber.touchedElement.name);
                    icon.color = AlmulColor(Color.Lerp(color, SimDebugView_Ctor_Patch.col_light, SimDebugView_Ctor_Patch.cint_dens));
                    tooltip.toolTip = "Cell in the highlighted region with least mass";
                    break;

                case OverlayMode.TEMPERATURE:
                    color = Color.white;

                    unitObj = UnitFetch(activeUnitObjs[0], out text, out icon, out tooltip);
                    //text.text = "Greatest Temperature";
                    text.text = string.Format(">= {0}", PatchCommon.FormatTemp(MaterialProber.Max));
                    icon.color = AlmulColor(Color.Lerp(color, SimDebugView_Ctor_Patch.col_hot, SimDebugView_Ctor_Patch.cint_temp));
                    tooltip.toolTip = "Cell in the highlighted region with greatest temperature";

                    unitObj = UnitFetch(activeUnitObjs[1], out text, out icon, out tooltip);
                    //text.text = "Least Temperature";
                    text.text = string.Format("<= {0}", PatchCommon.FormatTemp(MaterialProber.Min));
                    icon.color = AlmulColor(Color.Lerp(color, SimDebugView_Ctor_Patch.col_cold, SimDebugView_Ctor_Patch.cint_temp));
                    tooltip.toolTip = "Cell in the highlighted region with least temperature";
                    break;

                case OverlayMode.GERMS:
                    if (MaterialProber.touchedDisease == (byte)0xFF)
                        return;
                    Klei.AI.Disease disease = Db.Get().Diseases[(byte)MaterialProber.touchedDisease];
                    color = disease.overlayColour;

                    unitObj = UnitFetch(activeUnitObjs[0], out text, out icon, out tooltip);
                    //text.text = "Greatest Germs";
                    text.text = string.Format(">= {0} {1}", GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.Max), disease.Name);
                    icon.color = AlmulColor(new Color(color.r, color.g, color.b, SimDebugView_Ctor_Patch.high_alpha));
                    tooltip.toolTip = "Cell in the highlighted region with greatest germ count";

                    unitObj = UnitFetch(activeUnitObjs[1], out text, out icon, out tooltip);
                    //text.text = "Least Germs";
                    text.text = string.Format(">= {0} {1}", GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.Min), disease.Name);
                    icon.color = AlmulColor(new Color(color.r, color.g, color.b, SimDebugView_Ctor_Patch.low_alpha));
                    tooltip.toolTip = "Cell in the highlighted region with least germ count";
                    break;

                case OverlayMode.BIOME:
                    //For reasons mentioned above, changing the number of legend units after init is a bit difficult, so we've got to work with what we've got.
                    unitObj = UnitFetch(activeUnitObjs[0], out text, out icon, out tooltip);
                    text.text = "Exposed to Space";
                    icon.color = AlmulColor(PatchCommon.GetColorForBiome(ProcGen.SubWorld.ZoneType.Space));
                    tooltip.toolTip = "Cells without walls will be exposed to space";
                    
                    unitObj = UnitFetch(activeUnitObjs[1], out text, out icon, out tooltip);
                    text.text = "Other";
                    icon.color = Color.clear;
                    tooltip.toolTip = "Other biome";
                    break;
            }
        }

        //Called after OverlayLegend.SetLegend()
        public static void Postfix(OverlayLegend __instance, List<GameObject> ___activeDiagrams, GameObject ___diagramsParent, OverlayLegend.OverlayInfo overlayInfo)
        {
            if (overlayInfo != null && overlayInfo.mode == MaterialProbeMode.ID)
            {
                DoLegendUpdate(__instance);
                
                var go = Util.KInstantiateUI(MaterialProbeDiagram.Prefab, ___diagramsParent, false);
                ___activeDiagrams.Add(go);
                ___diagramsParent.SetActive(true);
            }
        }
    }
    
    //Tell the game what Status Items (the little red flags, "No Power", etc.) to show in our overlay.
    //We don't want any, but getting them to show up seems to be harder than just setting this to
    //StatusItem.StatusItemOverlays.PowerMap or something anyway.
    [HarmonyPatch(typeof(StatusItem), "GetStatusItemOverlayBySimViewMode")]
    public static class StatusItem_GetStatusItemOverlayBySimViewMode_Patch
    {
        //Called before StatusItem.GetStatusItemOverlayBySimViewMode()
        public static bool Prefix(HashedString mode, ref StatusItem.StatusItemOverlays __result)
        {
            if (mode == MaterialProbeMode.ID)
            {
                __result = StatusItem.StatusItemOverlays.None;
                return false;
            }
            return true;
        }
    }

    //This lets you add stuff to the overlayFilterMap. The tooltip system looks in this dictionary (in the silliest way possible)
    //when creating the tooltip for a cell. If it finds a delegate for the map mode, and the delegate returns false, the raw
    //element in the cell will not be added to the tooltip. (This is why water doesn't show a tooltip in oxygen mode.)
    //
    //But, uh, we don't actually need any of that. Just thought it was interesting.
    //
    //Last updated in Expressive Update
    //[HarmonyPatch(typeof(SelectToolHoverTextCard), "OnSpawn")]
    //public static class SelectToolHoverTextCard_OnSpawn_Patch
    //{
    //    //overlayFilterMap is a private member on SelectToolHoverTextCard.  Apparently this version of harmony doesn't support fetching private fields for us. :(
    //    public static void Postfix(SelectToolHoverTextCard __instance)
    //    {
    //        Dictionary<SimViewMode, Func<bool>> ___overlayFilterMap = (Dictionary<SimViewMode, Func<bool>>) AccessTools.Field(typeof(SelectToolHoverTextCard), "overlayFilterMap").GetValue(__instance); 
    //        ___overlayFilterMap.Add(PatchCommon.SimViewMode_MaterialProbeMap, delegate
    //        {
    //            int num = Grid.PosToCell(CameraController.Instance.baseCamera.ScreenToWorldPoint(KInputManager.GetMousePos()));
    //            return Grid.Element[num].IsGas;
    //        });
    //    }
    //}

    //This patch lets us determine what Selectables (buildings, creatures, etc.) should be selectable in our overlay.
    [HarmonyPatch(typeof(SelectToolHoverTextCard), "ShouldShowSelectableInCurrentOverlay", new Type[] { typeof(KSelectable) })]
    public static class SelectToolHoverTextCard_UShouldShowSelectableInCurrentOverlay_Patch
    {
        public static bool Prefix(SelectToolHoverTextCard __instance, KSelectable selectable, ref bool __result)
        {
            if (OverlayScreen.Instance.GetMode() == MaterialProbeMode.ID)
            {
                __result = false;
                return false;
            }
            return true;
        }
    }

    
    //This is a big complicated patch, since it targets a big function. UpdateHoverElements is responsible for creating
    //the list of tooltips when you hover over a cell or object.
    //
    //This is a *difficult* patch. We can't prefix or postfix, so our only choice is transpile. To keep things as simple
    //as possible, we're going to simply inject calls to our functions at appropriate places.
    [HarmonyPatch(typeof(SelectToolHoverTextCard), "UpdateHoverElements", new Type[] { typeof(List<KSelectable>) })]
    public static class SelectToolHoverTextCard_UpdateHoverElements_Patch
    {
        #region Patches for QoL2 Upgrade
        static CodeInstruction labelStealer0Target = new CodeInstruction(OpCodes.Ldarg_0);
        static CodeInstruction labelStealer1Target = new CodeInstruction(OpCodes.Ldarg_0);
        static QuickPatcher.LocalReference local_mode          = new QuickPatcher.LocalReference(typeof(HashedString) , 5);
        static QuickPatcher.LocalReference local_showElements  = new QuickPatcher.LocalReference(typeof(Boolean)      , 7);
        static QuickPatcher.LocalReference local_choreConsumer = new QuickPatcher.LocalReference(typeof(ChoreConsumer), 49);
        static QuickPatcher.LocalReference local_kselectable2  = new QuickPatcher.LocalReference(typeof(KSelectable)  , 51);
        static QuickPatcher.LocalReference local_text13        = new QuickPatcher.LocalReference(typeof(string)       , 101);

        static QuickPatcher.IPatcher[] patchers = new QuickPatcher.IPatcher[] {
            new QuickPatcher.SimpleMatchInsertPatcher(
                //Match against: string text = string.Empty;
                //This should be a safe target. Match starts at 111 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(string), "Empty")),
                    new CodeInstruction(OpCodes.Stloc_S, new QuickPatcher.LocalReference(typeof(string), 11)),
                },
                //Insert: SelectToolHoverTextCard_UpdateHoverElements_Patch.Hook_UpdateHoverElements_First(this, hoverTextDrawer, hoveredSelectables, mode, cell, ref showElements);
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_S, local_mode),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldloca_S, local_showElements),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SelectToolHoverTextCard_UpdateHoverElements_Patch), "Hook_UpdateHoverElements_First")),
                },
                true //after
            ),

            new QuickPatcher.SimpleMatchInsertPatcher(
                //Match against: choreConsumer.ShowHoverTextOnHoveredItem(kselectable2, hoverTextDrawer, this);
                //This should be a safe target. Match starts at 1360 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, local_choreConsumer),
                    new CodeInstruction(OpCodes.Ldloc_S, local_kselectable2),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ChoreConsumer), "ShowHoverTextOnHoveredItem", new Type[] { typeof(KSelectable), typeof(HoverTextDrawer), typeof(SelectToolHoverTextCard) })),
                },
                //Insert: SelectToolHoverTextCard_UpdateHoverElements_Patch.Hook_UpdateHoverElements_HoverItem(this, hoverTextDrawer, hoveredSelectables, mode, selectable);
                new CodeInstruction[]
                {
                    labelStealer0Target, //new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_S, local_mode),
                    new CodeInstruction(OpCodes.Ldloc_S, local_kselectable2),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SelectToolHoverTextCard_UpdateHoverElements_Patch), "Hook_UpdateHoverElements_HoverItem")),
                },
                true //after
            ),
            
            new QuickPatcher.LabelStealerPatcher(
                //Steal the label at: hoverTextDrawer.EndShadowBar();
                //This is a crazy generic target, but it should be safe since it occurs right after one of our patches. Match starts at 1365 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HoverTextDrawer), "EndShadowBar")),
                }
            ) { Target = labelStealer0Target },

            new QuickPatcher.SimpleMatchInsertPatcher(
                //Match against: hoverTextDrawer.DrawText(text13, this.Styles_BodyText.Standard);
                //This target is a little sketchy, but should be fine, probably due to the local. Match starts at 1674 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldloc_S, local_text13),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(HoverTextConfiguration), "Styles_BodyText")),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(HoverTextConfiguration.TextStylePair), "Standard")),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HoverTextDrawer), "DrawText", new Type[] { typeof(string), typeof(TextStyleSetting) })),
                },
                //Insert: SelectToolHoverTextCard_UpdateHoverElements_Patch.Hook_UpdateHoverElements_Element(this, hoverTextDrawer, hoveredSelectables, mode, cell);
                new CodeInstruction[]
                {
                    labelStealer1Target, //new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldloc_S, local_mode),
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SelectToolHoverTextCard_UpdateHoverElements_Patch), "Hook_UpdateHoverElements_Element")),
                },
                true //after
            ),

            new QuickPatcher.LabelStealerPatcher(
                //Steal the label at: hoverTextDrawer.EndShadowBar();
                //This is a crazy generic target, but it should be safe since it occurs right after one of our patches. Match starts at 1680 (QoL2)
                new CodeInstruction[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(HoverTextDrawer), "EndShadowBar")),
                }
            ) { Target = labelStealer1Target },
        };

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase original, IEnumerable<CodeInstruction> instr)
        {
            //QuickPatcher.DebugVerbosity = 2;
            
            return QuickPatcher.ApplyPatches(original, instr, patchers);
        }
        #endregion

        //Called before any other tooltip cards are generated. This is called outside of a BeginShadowBar block,
        //so you can create any number of such cards you would like.
        public static void Hook_UpdateHoverElements_First(SelectToolHoverTextCard card, HoverTextDrawer drawer, List<KSelectable> hoveredSelectables, HashedString mode, int cell, ref bool showElement)
        {
            const int lineHeight = 26;
            const int spaceHeight = 8;
            try {
            if (mode == MaterialProbeMode.ID)
            {
                if (showElement)
                {
                    //MaterialProber.germsMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    //MaterialProber.spaceMode = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                    //MaterialProber.reltempMode = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    MaterialProber.DoProbe(cell);

                    if (MaterialProber.mode == OverlayMode.BIOME)
                    {
                        var biome = Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell);
                        drawer.BeginShadowBar(false);
                        drawer.DrawText(biome.ToString(), card.Styles_Title.Standard);
                        drawer.EndShadowBar();
                    }
                    else if (MaterialProber.touchedCells.Count > 0)
                    {
                        OverlayLegend_SetLegend_Patch.QuickUpdate();

                        var iconDash = (Sprite)AccessTools.Field(typeof(SelectToolHoverTextCard), "iconDash").GetValue(card);
                        
                        drawer.BeginShadowBar(false);
                        if (MaterialProber.mode == OverlayMode.GERMS && MaterialProber.touchedDisease != (byte)0xFF)
                        {
                            Klei.AI.Disease disease = Db.Get().Diseases[(byte)MaterialProber.touchedDisease];

                            drawer.DrawText(MaterialProber.valueByElement.Count>1 ? STRINGS.MATERIAL_PROBE.MULTIPLE_TYPES.text : disease.Name.ToUpper(), card.Styles_Title.Standard);

                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.CELL_COUNT, MaterialProber.cellCount), card.Styles_Values.Property.Standard);
                                
                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_TOTAL, GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.valueTotal)),
                                card.Styles_Values.Property.Standard);
                            if (MaterialProber.valueByElement.Count > 1)
                            {

                                var elemlist = new List<KeyValuePair<uint, float>>(MaterialProber.valueByElement);
                                elemlist.Sort((KeyValuePair<uint, float> lhs, KeyValuePair<uint, float> rhs) => rhs.Value.CompareTo(lhs.Value)); //reverse sort
                                foreach(var eid in elemlist)
                                {
                                    var ldis = Db.Get().Diseases[(int)eid.Key];
                                        
                                    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_ELEM, ldis.Name, GameUtil.GetFormattedDiseaseAmount((int)eid.Value), eid.Value / MaterialProber.valueTotal * 100, eid.Key == MaterialProber.touchedDisease ? " HERE" : ""),
                                        card.Styles_Values.Property.Standard);
                                }
                            }

                            drawer.NewLine(spaceHeight);
                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_HERE, GameUtil.GetFormattedDiseaseAmount((int)Grid.DiseaseCount[cell])),
                                card.Styles_Values.Property.Standard);

                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_AVG, GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.valueAvg)),
                                card.Styles_Values.Property.Standard);

                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_MINMAX, GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.true_min), GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.true_max)),
                                card.Styles_Values.Property.Standard);
                            
                            //if (MaterialProber.rangeMode != 0)
                            //{
                            //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            //    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_RANGE, GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.Min), GameUtil.GetFormattedDiseaseAmount((int)MaterialProber.Max)),
                            //        card.Styles_Values.Property.Standard);
                            //}
                        }
                        else if (MaterialProber.mode == OverlayMode.MASS)
                        {
                            drawer.DrawText(MaterialProber.valueByElement.Count > 1 ? STRINGS.MATERIAL_PROBE.MULTIPLE_ELEMENTS.text : MaterialProber.touchedElement.nameUpperCase, card.Styles_Title.Standard);

                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText("Cells: " + MaterialProber.cellCount, card.Styles_Values.Property.Standard);

                            if (MaterialProber.valueList.Count > 0)
                            {
                                drawer.NewLine(spaceHeight);
                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_HERE, PatchCommon.FormatMass(Grid.Mass[cell])),
                                    card.Styles_Values.Property.Standard);

                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_AVG, PatchCommon.FormatMass(MaterialProber.valueAvg)),
                                    card.Styles_Values.Property.Standard);

                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_MINMAX, PatchCommon.FormatMass(MaterialProber.true_min), PatchCommon.FormatMass(MaterialProber.true_max)),
                                    card.Styles_Values.Property.Standard);
                                
                                //if (MaterialProber.rangeMode != 0)
                                //{
                                //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                //    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_RANGE, PatchCommon.FormatMass(MaterialProber.Min), PatchCommon.FormatMass(MaterialProber.Max)),
                                //        card.Styles_Values.Property.Standard);
                                //}

                                if (MaterialProber.touchedElement.IsSolid)
                                {
                                    drawer.NewLine(spaceHeight);
                                    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_HARV, PatchCommon.FormatMass(MaterialProber.valueTotal * 0.5f)), //based on magic constant in WorldDamage.OnDigComplete
                                        card.Styles_Values.Property.Standard);
                                }
                                //if (MaterialProber.touchedElement.IsGas || MaterialProber.touchedElement.IsLiquid)
                                //{
                                //    float moles = Grid.Mass[cell] / Grid.Element[cell].molarMass;
                                //    drawer.NewLine(spaceHeight);
                                //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                //    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_MOLAR, PatchCommon.GetFormattedMoles(moles)),
                                //        card.Styles_Values.Property.Standard);
                                //    float pressure = //ideal gas law: p = nRT / V
                                //            (moles * Grid.Temperature[cell] * 8.3144598f) /* /
                                //            1 */
                                //    ;
                                //    drawer.NewLine(spaceHeight);
                                //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                //    drawer.DrawText(string.Format("Pressure: {0:F3} pascals", PatchCommon.GetFormattedMoles(pressure)),
                                //        card.Styles_Values.Property.Standard);
                                //}

                                drawer.NewLine(spaceHeight);
                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_TOTAL, PatchCommon.FormatMass(MaterialProber.valueTotal)),
                                    card.Styles_Values.Property.Standard);
                                
                                if (MaterialProber.valueByElement.Count > 1)
                                {
                                    Sprite iconGas    = Assets.GetSprite("lots"); //or "little"
                                    Sprite iconLiquid = Assets.GetSprite("action_bottler_delivery");
                                    Sprite iconSolid  = Assets.GetSprite("status_item_wrong_resource_in_pipe");
                                    var elemlist = new List<KeyValuePair<uint, float>>(MaterialProber.valueByElement);
                                    elemlist.Sort((KeyValuePair<uint, float> lhs, KeyValuePair<uint, float> rhs) => rhs.Value.CompareTo(lhs.Value)); //reverse sort
                                    foreach(var eid in elemlist)
                                    {
                                        var elem = ElementLoader.elements[(int)eid.Key];
                                        
                                        drawer.NewLine(lineHeight-4);

                                        Sprite icon = iconDash;
                                        if (elem.IsGas)    icon = iconGas;
                                        if (elem.IsLiquid) icon = iconLiquid;
                                        if (elem.IsSolid)  icon = iconSolid;
                                        drawer.DrawIcon(icon, PatchCommon.GetColorForElement(elem), 18);

                                        string loc = STRINGS.MATERIAL_PROBE.STAT_ELEM;
                                        if (elem == MaterialProber.touchedElement) loc = "<b>"+loc+"</b>";
                                        drawer.DrawText(string.Format(loc, elem.name, PatchCommon.FormatMass(eid.Value), eid.Value / MaterialProber.valueTotal * 100),
                                            card.Styles_Values.Property.Standard);
                                    }
                                }
                            }
                        }
                        else if (MaterialProber.mode == OverlayMode.TEMPERATURE)
                        {
                            drawer.DrawText(MaterialProber.valueByElement.Count > 1 ? STRINGS.MATERIAL_PROBE.MULTIPLE_ELEMENTS.text : MaterialProber.touchedElement.nameUpperCase, card.Styles_Title.Standard);

                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText("Cells: " + MaterialProber.cellCount, card.Styles_Values.Property.Standard);

                            if (MaterialProber.valueList.Count > 0)
                            {
                                drawer.NewLine(spaceHeight);
                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_HERE, PatchCommon.FormatTemp(Grid.Temperature[cell])),
                                    card.Styles_Values.Property.Standard);

                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_AVG, PatchCommon.FormatTemp(MaterialProber.valueAvg)),
                                    card.Styles_Values.Property.Standard);

                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_MINMAX, PatchCommon.FormatTemp(MaterialProber.true_min), PatchCommon.FormatTemp(MaterialProber.true_max)),
                                    card.Styles_Values.Property.Standard);
                                
                                //if (MaterialProber.rangeMode != 0)
                                //{
                                //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                //    drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_RANGE, PatchCommon.FormatTemp(MaterialProber.Min), PatchCommon.FormatTemp(MaterialProber.Max)),
                                //        card.Styles_Values.Property.Standard);
                                //}
                                
                                drawer.NewLine(spaceHeight);
                                drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                                drawer.DrawText(string.Format(STRINGS.MATERIAL_PROBE.STAT_THERM, GameUtil.FloatToString((float)(MaterialProber.totalThermalEnergy/1000.0), "F3") + STRINGS.UI.UNITSUFFIXES.ELECTRICAL.KILOJOULE),
                                    card.Styles_Values.Property.Standard);
                            }
                        }

                        if (MaterialProber.negligbleRange)
                        {
                            drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                            drawer.DrawText(STRINGS.MATERIAL_PROBE.NEGLIGIBLE_RANGE,
                                card.Styles_Values.Property.Standard);
                        }

                        //if (MaterialProber.spaceCells.Count > 0)
                        //{
                        //    drawer.NewLine(spaceHeight);
                        //    drawer.NewLine(lineHeight); drawer.DrawIcon(iconDash, 18);
                        //    drawer.DrawText("May be exposed to Space", card.Styles_Values.Property.Standard);
                        //}

                        drawer.EndShadowBar();
                    }
                    showElement = false;
                }
                else
                {
                    MaterialProber.Clear();
                }
            }
            } catch (Exception ex) { Debug.LogError(ex); }
        }
        //Called for each hovered item, right before it's EndShadowBlock is called. This allows you to add information to existing cards.
        public static void Hook_UpdateHoverElements_HoverItem(SelectToolHoverTextCard card, HoverTextDrawer drawer, List<KSelectable> hoveredSelectables, HashedString mode, KSelectable selectable)
        {
            //var iconDash = (Sprite)AccessTools.Field(typeof(SelectToolHoverTextCard), "iconDash").GetValue(card);
            //drawer.NewLine(26);
            //drawer.DrawIcon(iconDash, 18);
            //drawer.DrawText("Yes!", card.Styles_Values.Property.Standard);
            //drawer.NewLine(26);
            //drawer.DrawIcon(iconDash, 18);
            //drawer.DrawText("Item!", card.Styles_BodyText.Standard);
        }
        //Called for the hovered element, right before it's EndShadowBlock is called. This allows you to add information to the element card.
        public static void Hook_UpdateHoverElements_Element(SelectToolHoverTextCard card, HoverTextDrawer drawer, List<KSelectable> hoveredSelectables, HashedString mode, int cell)
        {
            //var iconDash = (Sprite)AccessTools.Field(typeof(SelectToolHoverTextCard), "iconDash").GetValue(card);
            //drawer.NewLine(26);
            //drawer.DrawIcon(iconDash, 18);
            //drawer.DrawText("Element!", card.Styles_BodyText.Standard);
            //drawer.NewLine(26);
            //drawer.DrawIcon(iconDash, 18);
            //drawer.DrawText("Aaaaah!", card.Styles_Values.Property.Standard);
        }
    }


    //This patch sets up the coloring delegate for our overlay. This is what actually colors tiles on the map when our overlay is active.
    //Despite the name, this is how the game does all the overlay coloring. There is also a way to change the texture and filtering of
    //the overlay (why oxygen and germs overlays look different), but the default is fine for this.
    [HarmonyPatch(typeof(SimDebugView), MethodType.Constructor, new Type[] { })]
    public static class SimDebugView_Ctor_Patch
    {
        public static float outofrange_alpha = 0.35f; //This is drawn with black, so the higher this number, the dimmer the world outside the probe range is.
        public static float unprobed_alpha = 0.05f;
        public static float unprobed_germ_alpha = 0.1f;
        public static float blanked_alpha = 0.7f;
        public static float temp_alpha = 0.7f;
        public static Color col_hot = new Color(1.0f, 0.3f, 0.3f);
        public static Color col_cold = new Color(0.3f, 0.3f, 1.0f);
        public static float cint_temp = 0.8f; //maximum interpolation to hot and cold colors
        public static Color col_dense = new Color(1.0f, 1.0f, 1.0f);
        public static Color col_light = new Color(0.0f, 0.0f, 0.0f);
        public static float cint_dens = 0.5f; //maximum interpolation to dense and light colors
        public static float low_alpha = 0.3f;
        public static float high_alpha = 1.0f;
        public static Color space_color = new Color(1, 0.5f, 0.5f, 0.5f);

        //All we need to do is add a delegate to SimDebugView.getColorFuncs. It takes care of the rest.
        //If we wanted to change the filtering or texture, we would add a Action<SimDebugView, Texture> to dataUpdateFuncs.
        public static void Postfix(Dictionary<HashedString, Func<SimDebugView, int, Color>> ___getColourFuncs)
        {
            ___getColourFuncs.Add(MaterialProbeMode.ID, MaterialProbeGetColor);
        }
        public static Color MaterialProbeGetColor(SimDebugView view, int cell)
        {
            try {
            if (Grid.GetCellRange(cell, MaterialProber.originCell) < MaterialProber.range)
            {
                bool probed = MaterialProber.touchedCells.Count > 0 && MaterialProber.touchedCells.Contains(cell);
                //bool space = MaterialProber.spaceCells.Contains(cell);

                if (MaterialProber.mode == OverlayMode.BIOME)
                {
                    //bool space = Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell) == ProcGen.SubWorld.ZoneType.Space;
                    //return space ? space_color : Color.clear;
                    var biome = Game.Instance.world.zoneRenderData.GetSubWorldZoneType(cell);
                    Color color = PatchCommon.GetColorForBiome(biome);
                    color.a = 0.5f;
                    return color;
                }
                else if (MaterialProber.mode == OverlayMode.GERMS)
                {
                    var disidx = Grid.DiseaseIdx[cell];
                    if (disidx != (byte)0xFF && Grid.DiseaseCount[cell] > 0)
                    {
                        Klei.AI.Disease disease = Db.Get().Diseases[disidx];
                        Color color = disease.overlayColour;
                        if (probed)
                        {
                            float f = (float)((Grid.DiseaseCount[cell] - MaterialProber.Min) / MaterialProber.Range);
                            f = Mathf.Clamp01(f);
                            
                            color.a = low_alpha + (high_alpha-low_alpha) * f;
                        }
                        else
                        {
                            color.a = unprobed_germ_alpha;
                        }
                        return color;
                    }
                    return Color.clear;
                }
                else if (MaterialProber.mode == OverlayMode.MASS || MaterialProber.mode == OverlayMode.TEMPERATURE)
                {
                    var elem = Grid.Element[cell];
                    Color color = MaterialProber.mode == OverlayMode.MASS ? PatchCommon.GetColorForElement(elem) : new Color(1, 1, 1, 1);
                    if (probed)
                    {
                        //if (!space)
                        //{
                        //    //only probed
                        //    __result = color;
                        //}
                        //else if (!probed)
                        //{
                        //    //only space
                        //    __result = new Color(0.3f, 0.3f, 0.3f);
                        //}
                        //else
                        //{
                        //    //both
                        //    __result = Color.Lerp(color, new Color(0.9f, 1.0f, 0.9f), 0.2f);
                        //}
                        if (elem.IsVacuum || elem.id == SimHashes.Unobtanium)
                        {
                            color.a = blanked_alpha;
                        }
                        else
                        {
                            if (!MaterialProber.negligbleRange)
                            {
                                if (MaterialProber.mode == OverlayMode.TEMPERATURE)
                                {
                                    float f = (float)((Grid.Temperature[cell] - MaterialProber.Min) / MaterialProber.Range);
                                    f = Mathf.Clamp01(f);
                                    if (f < 0.5)
                                        color = Color.Lerp(color, col_cold, (0.5f - f) * 2 * cint_temp);
                                    else
                                        color = Color.Lerp(color, col_hot, (f - 0.5f) * 2 * cint_temp);
                                }
                                else
                                {
                                    float f = (float)((Grid.Mass[cell] - MaterialProber.Min) / MaterialProber.Range);
                                    f = Mathf.Clamp01(f);
                                    if (f < 0.5)
                                        color = Color.Lerp(color, col_light, (0.5f - f) * 2 * cint_dens);
                                    else
                                        color = Color.Lerp(color, col_dense, (f - 0.5f) * 2 * cint_dens);
                                    //color.a = low_alpha + (high_alpha - low_alpha) * f;
                                }
                            }
                            color.a = temp_alpha;
                        }
                    }
                    else
                    {
                        //not part of probe
                        color.a = unprobed_alpha;
                    }
                    return color;
                }
            } else {
                Color color = Color.black;
                color.a = outofrange_alpha;
                return color;
            }
            } catch (Exception ex) { Debug.LogError(ex); }
            return Color.clear;
        }
    }
    
}

//Theoretically, we may be able to hook into the game's localization system, which would allow our strings to be localized.
namespace STRINGS
{
    public class MATERIAL_PROBE
    {
        public static LocString BUTTON = "Material Probe";
        public static LocString OVERLAYSTRING = "Analyzes information for large areas";
        public static LocString MATCH_ELEMENT     = "Match Element";
        public static LocString MATCH_PHASE       = "Match Phase";
        public static LocString MATCH_CONSTRUCTED = "Match Constructed";
        public static LocString MATCH_BIOME       = "Match Biome";
        public static LocString IGNORE_UNUSUAL    = "Ignore Abysallite/Neutronium";
        public static LocString RANGE_FIELD       = "Probe Range: ";
        public static LocString RANGEMODE_LABEL    = "Range Display: ";
        public static LocString RANGEMODE_TRUE    = "True Min/Max";
        public static LocString RANGEMODE_SDV2    = "2 Std. Dev.";
        public static LocString RANGEMODE_SDV1    = "1 Std. Dev.";
        public static LocString COLOR_PALETTE     = "Color Palette: ";
        public static LocString MODE_LABEL        = "Mode: ";
        public static LocString MODE_MASS         = "Mass";
        public static LocString MODE_TEMP         = "Temp.";
        public static LocString MODE_GERMS        = "Germs";
        public static LocString MODE_BIOME        = "Biome";
        public static LocString CELL_COUNT  = "Cells: {0}";
        public static LocString STAT_HERE  = "Here: {0}";
        public static LocString STAT_AVG   = "Avg.: {0}";
        public static LocString STAT_MINMAX = "Min/Max: {0} - {1}";
        public static LocString STAT_RANGE = "Range: {0} - {1}";
        public static LocString STAT_TOTAL = "Total: {0}";
        public static LocString STAT_ELEM = "{2:f1}% {0}: {1}";
        public static LocString STAT_HARV = "Harvestable: {0}";
        public static LocString STAT_MOLAR = "Molar Mass Here: {0}";
        public static LocString STAT_THERM = "Thermal Energy: {0}";
        public static LocString MULTIPLE_TYPES = "<MULTIPLE TYPES>";
        public static LocString MULTIPLE_ELEMENTS = "<MULTIPLE ELEMENTS>";
        public static LocString NEGLIGIBLE_RANGE = "NEGLIGIBLE RANGE";

        public class MOLE
        {
            public static LocString MEGA  = " Mmol";
            public static LocString KILO  = " Kmol";
            public static LocString UNIT  = " mol";
            public static LocString MILLI = " mmol";
            public static LocString MICRO = " mcmol";
        }
    }
}
