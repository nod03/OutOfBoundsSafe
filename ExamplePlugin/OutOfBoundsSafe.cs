using BepInEx;
using RoR2;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace OutOfBoundsSafe
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class OutOfBoundsSafe : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "bouncyshield";
        public const string PluginName = "OutOfBoundsSafe";
        public const string PluginVersion = "1.0.4";

        public void Awake()
        {
            Log.Init(Logger);

            Configs();

            On.RoR2.MapZone.TryZoneStart += HitBound;
            Stage.onStageStartGlobal += ClearBasket;
            Stage.onStageStartGlobal += GetBound;
        }

        private List<Coroutine> Basket = new();
        private void HitBound(On.RoR2.MapZone.orig_TryZoneStart orig, MapZone self, Collider other)
        {
            string n = self.gameObject.name.ToLower();

            bool a = !(n.Contains("orb") | n.Contains("altar") | (n.Contains("map") && !n.Contains("holder")) | n.Contains("kickout"));
            bool b = other.GetComponent<CharacterBody>()?.isPlayerControlled ?? false;

            if (a & b)
            {
                Basket.Add(StartCoroutine(Tether(orig, self, other)));
            }
            else if (!a & b)
            {
                Log.Debug($"Allowed {self.gameObject.name} to warp {other.name}");
                orig(self, other);
            }
            else
            {
                orig(self, other);
            }
        }

        private List<Collider> tethered = new();
        private float yBound = float.MaxValue;
        private bool imscared = false;
        IEnumerator Tether(On.RoR2.MapZone.orig_TryZoneStart orig, MapZone self, Collider other)
        {
            if (!tethered.Contains(other))
            {
                tethered.Add(other);

                string tetherName = $"{other.name} to {self.name}";
                Log.Info($"Tether {tetherName} created");

                string breakReason;
                while (true)
                {
                    if (self == null)
                    {
                        breakReason = "stage complete";
                        break;
                    }
                    else if (other == null)
                    {
                        breakReason = "died";
                        break;
                    }
                    else if (other.transform.localPosition.y < yBound)
                    {
                        breakReason = "fell OOB";
                        orig(self, other);
                        break;
                    }
                    else if (imscared)
                    {
                        breakReason = "EEK!";
                        orig(self, other);
                        imscared = false;
                        break;
                    }
                    yield return new WaitForSeconds(1);
                }
                
                tethered.Remove(other);

                Log.Info($"Tether {tetherName} broke ({breakReason})");
            }
        }
        
        private void ClearBasket(Stage x)
        {
            Basket.Clear();
        }

        private void GetBound(Stage stage)
        {
            float wauce = float.MaxValue;

            var nodes = SceneInfo.instance?.groundNodes;
            if (nodes != null)
            {
                foreach (var node in nodes.nodes)
                {
                    if (node.position.y < wauce)
                    {
                        wauce = node.position.y;
                    }
                }
            }

            if (wauce == float.MaxValue)
            {
                Log.Warning("Could not find a lower bound for the scene, OOB mechanics will not work");
            }
            else { Log.Debug($"Lower bound at y = {wauce} (- {buffer.Value})"); }

            yBound = wauce - buffer.Value;
        }

        internal static ConfigEntry<KeyCode> breakKey;
        internal static ConfigEntry<float> buffer;
        private void Configs()
        {
            breakKey = Config.Bind("OOBSafe", "Panic Button", KeyCode.B, "Key to instantly return to in bounds!");
            buffer = Config.Bind("OOBSafe", "Fall Buffer", 600f, "How far you can fall out of the map (in y coords) before you are returned in bounds.");
        }

        public void Update()
        {
            if (Input.GetKeyDown(breakKey.Value) && tethered.Count > 0)
            {
                imscared = true;
            }
        }
    }
}