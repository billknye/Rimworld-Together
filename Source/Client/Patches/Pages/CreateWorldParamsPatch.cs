﻿using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimworldTogether.GameClient.Dialogs;
using RimworldTogether.GameClient.Managers;
using RimworldTogether.GameClient.Managers.Actions;
using RimworldTogether.GameClient.Misc;
using RimworldTogether.GameClient.Planet;
using RimworldTogether.GameClient.Values;
using RimworldTogether.Shared.JSON;
using RimworldTogether.Shared.Misc;
using RimworldTogether.Shared.Network;
using UnityEngine;
using Verse;

namespace RimworldTogether.GameClient.Patches.Pages
{
    public class CreateWorldParamsPatch
    {
        [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
        public static class PatchWhenHost
        {
            [HarmonyPrefix]
            public static bool DoPre(Rect rect, Page_CreateWorldParams __instance, string ___seedString, float ___planetCoverage, OverallRainfall ___rainfall, OverallTemperature ___temperature, OverallPopulation ___population, List<FactionDef> ___factions, float ___pollution)
            {
                if (!ClientValues.needsToGenerateWorld) return true;
                else
                {
                    Vector2 buttonSize = new Vector2(150f, 38f);
                    Vector2 buttonLocation = new Vector2(rect.xMin, rect.yMax - buttonSize.y);
                    if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), ""))
                    {
                        __instance.Close();
                        Network.Network.DisconnectFromServer();
                    }

                    buttonLocation = new Vector2(rect.xMax - buttonSize.x, rect.yMax - buttonSize.y);
                    if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), ""))
                    {
                        PassArgumentsToServer(___seedString, ___planetCoverage, ___rainfall, ___temperature, ___population, 
                            ___factions, ___pollution);

                        __instance.Close();
                    }

                    return true;
                }
            }

            [HarmonyPostfix]
            public static void DoPost(Rect rect)
            {
                if (!ClientValues.needsToGenerateWorld) return;
                else
                {
                    Text.Font = GameFont.Small;
                    Vector2 buttonSize = new Vector2(150f, 38f);
                    Vector2 buttonLocation = new Vector2(rect.xMin, rect.yMax - buttonSize.y);
                    if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), "Disconnect")) { }

                    buttonLocation = new Vector2(rect.xMax - buttonSize.x, rect.yMax - buttonSize.y);
                    if (Widgets.ButtonText(new Rect(buttonLocation.x, buttonLocation.y, buttonSize.x, buttonSize.y), "Generate")) { }
                }
            }

            public static void PassArgumentsToServer(string seedString, float planetCoverage, OverallRainfall rainfall, 
                OverallTemperature temperature, OverallPopulation population, List<FactionDef> factions, float pollution)
            {
                WorldDetailsJSON worldDetailsJSON = new WorldDetailsJSON();
                worldDetailsJSON.worldStepMode = ((int)WorldManager.WorldStepMode.Required).ToString();
                worldDetailsJSON.SeedString = seedString;
                worldDetailsJSON.PlanetCoverage = planetCoverage;
                worldDetailsJSON.Rainfall = (float)rainfall;
                worldDetailsJSON.Temperature = (float)temperature;
                worldDetailsJSON.Population = (float)population;
                worldDetailsJSON.Pollution = pollution;

                foreach (FactionDef def in factions) worldDetailsJSON.Factions.Add(def.defName.ToString());

                PlanetFactions.SetPlayerFactionDefs();
                worldDetailsJSON.Factions.Add(PlanetFactions.neutralPlayerDef.defName);
                worldDetailsJSON.Factions.Add(PlanetFactions.allyPlayerDef.defName);
                worldDetailsJSON.Factions.Add(PlanetFactions.enemyPlayerDef.defName);
                worldDetailsJSON.Factions.Add(PlanetFactions.yourOnlineFactionDef.defName);

                DialogManager.PushNewDialog(new RT_Dialog_Wait("Waiting for server to accept world"));

                string[] contents = new string[] { Serializer.SerializeToString(worldDetailsJSON) };
                Packet packet = new Packet("WorldPacket", contents);
                Network.Network.SendData(packet);
            }
        }

        [HarmonyPatch(typeof(Page_CreateWorldParams), "PostOpen")]
        public static class PatchWhenPlayer
        {
            [HarmonyPrefix]
            public static bool DoPre(Page_CreateWorldParams __instance)
            {
                if (!ClientValues.isLoadingPrefabWorld) return true;
                else
                {
                    __instance.Close();
                    WorldGeneratorManager.GeneratePatchedWorld();
                    return false;
                }
            }
        }
    }
}
