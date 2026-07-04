using BepInEx;
using R2API;
using RoR2;
using RoR2.Stats;
using System.IO;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Reflection;
using System.Runtime.CompilerServices;
using IL.RoR2.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using R2API.Networking.Interfaces;
using R2API.Networking;
using RoR2.Networking;

namespace OutOfBoundsSafe
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class OutOfBoundsSafe : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "bouncyshield";
        public const string PluginName = "OutOfBoundsSafe";
        public const string PluginVersion = "1.0.2";

        public void Awake()
        {
            Log.Init(Logger);

            On.RoR2.MapZone.TryZoneStart += HitBound;
            Stage.onStageStartGlobal += ClearBasket;
        }

        private List<Coroutine> Basket = new();
        private void HitBound(On.RoR2.MapZone.orig_TryZoneStart orig, MapZone self, Collider other)
        {
            string n = self.gameObject.name.ToLower();

            bool a = !(n.Contains("orb") | n.Contains("altar") | n.Contains("map") | n.Contains("kickout"));
            bool b = other.GetComponent<CharacterBody>().isPlayerControlled;

            if (a & b)
            {
                Basket.Add(StartCoroutine(Tether(orig, self, other)));
            }
            else
            {
                orig(self, other);
            }
        }

        private List<Collider> tethered = new();
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
                    else if (other.transform.localPosition.y < -500)
                    {
                        breakReason = "fell OOB";
                        orig(self, other);
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
    }
}