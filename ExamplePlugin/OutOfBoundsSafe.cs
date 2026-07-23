using BepInEx;
using RoR2;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine.SceneManagement;
using TMPro;
using RoR2.UI;
using System;
using UnityEngine.UI;

namespace OutOfBoundsSafe
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class OutOfBoundsSafe : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "bouncyshield";
        public const string PluginName = "OutOfBoundsSafe";
        public const string PluginVersion = "1.1.0";

        public void Awake()
        {
            Log.Init(Logger);

            Configs();

            Hooks();

            Assets.Init();
        }

        private void Hooks()
        {
            On.RoR2.MapZone.TryZoneStart += HitBound;
            Stage.onStageStartGlobal += OnStageStartGlobal;

            On.RoR2.SurvivorPodController.OnPassengerEnter += (On.RoR2.SurvivorPodController.orig_OnPassengerEnter orig, SurvivorPodController a, GameObject b) => { inPod = true; orig(a, b); };
            On.RoR2.SurvivorPodController.OnPassengerExit += (On.RoR2.SurvivorPodController.orig_OnPassengerExit orig, SurvivorPodController a, GameObject b) => { inPod = false; orig(a, b); };

            On.RoR2.Run.Awake += (On.RoR2.Run.orig_Awake orig, Run self) => { tooltipShown = disableTooltip.Value; orig(self); };
        }

        private void OnStageStartGlobal(Stage stage)
        {
            ClearBasket(stage);
            GetBound(stage);
        }

        private bool inPod = false;
        private List<Coroutine> Basket = new();
        private void HitBound(On.RoR2.MapZone.orig_TryZoneStart orig, MapZone self, Collider other)
        {
            if (inPod) { Log.Debug($"SurvivorPod passed through {self.gameObject.name}"); return; }

            string n = self.gameObject.name.ToLower();
            string m = SceneManager.GetActiveScene().name;

            bool a = (m == "limbo" || m == "mysteryworld" || m.Contains("artifactworld")) 
                || !(n.Contains("orb") | n.Contains("altar") | (n.Contains("map") && !n.Contains("holder")) | n.Contains("kickout") | n.Contains("fightstart"));
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

        private void ClearBasket(Stage x)
        {
            Basket.Clear();
        }

        private List<Collider> tethered = new();
        IEnumerator Tether(On.RoR2.MapZone.orig_TryZoneStart orig, MapZone self, Collider other)
        {
            if (!tethered.Contains(other))
            {
                tethered.Add(other);
                CheckTooltip();

                string tetherName = $"{other.name} to {self.name}";
                Log.Info($"Tether {tetherName} created");

                string breakReason;
                while (true)
                {
                    if (self == null)
                    {
                        breakReason = "MapZone null";
                        break;
                    }
                    else if (other == null)
                    {
                        breakReason = "Collider null";
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
                    
                    if (Assets.Tooltip)
                    {
                        tooltipTimer++;
                        if (tooltipTimer >= 30) { Assets.Tooltip.SetActive(false); }
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                }

                if (Assets.Tooltip && Assets.Tooltip.activeInHierarchy) { Assets.Tooltip.SetActive(false); Snake(); }
                tethered.Remove(other);

                Log.Info($"Tether {tetherName} broke ({breakReason})");
            }
        }

        private float yBound = float.MaxValue;
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

        private bool imscared = false;
        public void Update()
        {
            if (Input.GetKeyDown(breakKey.Value) && tethered.Count > 0)
            {
                imscared = true;
            }
        }

        public static bool tooltipShown;
        private int tooltipTimer = 0;
        private void CheckTooltip()
        {
            if (!tooltipShown && Assets.Tooltip != null)
            {
                Assets.Tooltip.SetActive(true);
                Assets.Tooltip.GetComponentInChildren<HGTextMeshProUGUI>().text = $"Press {OutOfBoundsSafe.breakKey.Value} to return in bounds!"; // only works after activation for some reason

                tooltipShown = true;
            }
        }

        private void Snake()
        {
            if (Assets.SnakeIcon != null)
            {
                Assets.SnakeIcon.SetActive(true);
                StartCoroutine(Fade());
            }
        }

        private float alpha;
        private IEnumerator Fade()
        {
            alpha = 1;
            Assets.SnakeImage.color = new Color(1, 1, 1, alpha);
            while (alpha >= 0)
            {
                yield return new WaitForSeconds(0.04f);
                alpha -= 0.01f * (2 - alpha);
                try { Assets.SnakeImage.color = new Color(1, 1, 1, alpha); } catch { } // but what if you quit to menu before fade completes
            }
        }

        public static ConfigEntry<KeyCode> breakKey;
        public static ConfigEntry<float> buffer;
        public static ConfigEntry<bool> disableTooltip;

        private void Configs()
        {
            breakKey = Config.Bind("OOBSafe", "Panic Button", KeyCode.B, "Key to manually return in bounds");
            buffer = Config.Bind("OOBSafe", "Fall Buffer", 600f, "How far you can fall out of the map (in y coords) before you are automatically returned in bounds.");
            disableTooltip = Config.Bind("OOBSafe", "Disable Tooltip", false, "Disables the 15 second tooltip shown on first boundary break per run");
        }
    }
}