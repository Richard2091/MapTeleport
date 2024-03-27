using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Generic;
using System.IO;

namespace MapTeleport
{
    public partial class ModEntry
    {
        protected static CoordinatesList addedCoordinates;
        public static bool CheckClickableComponents(List<ClickableComponent> components, int topX, int topY, int x, int y)
        {
            SMonitor.Log($"clicked x:{x} y:{y}", LogLevel.Debug);
            if (!Config.ModEnabled)
                return false;
            if (addedCoordinates == null)
            {
                addedCoordinates = SHelper.Data.ReadJsonFile<CoordinatesList>("coordinates.json");
                if (addedCoordinates == null) addedCoordinates = new CoordinatesList();
            }
            var coordinates = SHelper.GameContent.Load<CoordinatesList>(dictPath);
            bool added = false;
            bool found = false;
            // Sort boundries so that the function will warp to the smallest overlapping area
            components.Sort(delegate (ClickableComponent a, ClickableComponent b)
            {
                return (a.bounds.Height * a.bounds.Width).CompareTo(b.bounds.Height * b.bounds.Width);
            });
            foreach (ClickableComponent area in components)
            { 
                // 计算偏移适配不同屏幕
                string altId = $"{area.bounds.X - topX}.{area.bounds.Y - topY}";
                // 匹配算法 从配置中读取当前点击区域的传送点
                Predicate<Coordinates> findMatch = (o) => o.id == area.myID || (area.myID == ClickableComponent.ID_ignore && o.altId == altId);
                Coordinates teleportCoordinate = coordinates.coordinates.Find(findMatch);
                // 如果没有匹配到当前区域内的传送点
                if (teleportCoordinate == null)
                {
                    teleportCoordinate = addedCoordinates.coordinates.Find(findMatch);
                    if (teleportCoordinate == null)
                    {
                        if (area.myID == ClickableComponent.ID_ignore)
                        {
                            addedCoordinates.Add(new Coordinates() { name = area.name, altId = altId, enabled = false });
                        }
                        else
                        {
                            addedCoordinates.Add(new Coordinates() { name = area.name, id = area.myID, enabled = false });
                        }
                        SMonitor.Log($"Added: {{ \"name\":\"{area.name}\", \"id\":{area.myID}, \"altId\":\"{altId}\" }}", LogLevel.Trace);
                        added = true;
                    }
                    // else check if the coordinate is enabled
                }
                
                //SMonitor.Log($"now check area id:{area.myID} name:{area.name} label:{area.label} bounds.X:{area.bounds.X} bounds.Y{area.bounds.Y}", LogLevel.Debug);
                //SMonitor.Log($"teleport coordinate x:{teleportCoordinate.x} y:{teleportCoordinate.y}", LogLevel.Debug);

                if (area.containsPoint(x, y) && teleportCoordinate.enabled)
                {
                    SMonitor.Log($"Teleporting to {area.name} ({(teleportCoordinate.altId != null ? teleportCoordinate.altId : teleportCoordinate.id)}), {teleportCoordinate.mapName}, {teleportCoordinate.x},{teleportCoordinate.y}", LogLevel.Debug);
                    Game1.activeClickableMenu?.exitThisMenu(true);
                    Game1.warpFarmer(teleportCoordinate.mapName, teleportCoordinate.x, teleportCoordinate.y, false);
                    found = true;
                    break;
                }
            }
            if (added)
            {
                SHelper.Data.WriteJsonFile("coordinates.json", addedCoordinates);
            }
            return found;

        }

        [HarmonyPatch(typeof(MapPage), nameof(MapPage.receiveLeftClick))]
        public class MapPage_receiveLeftClick_Patch
        {
            public static bool Prefix(MapPage __instance, int x, int y)
            {
                List<ClickableComponent> clickableComponents = new List<ClickableComponent>(__instance.points.Values);
                bool found = CheckClickableComponents(clickableComponents, __instance.xPositionOnScreen, __instance.yPositionOnScreen, x, y);
                return !found;
                //SMonitor.Log($"clicked x:{x} y:{y}", LogLevel.Debug);
                //return false;
            }
        }

        [HarmonyPatch(typeof(IClickableMenu), nameof(IClickableMenu.receiveLeftClick))]
        public class RSVMapPage_receiveLeftClick_Patch
        {
            public static bool Prefix(IClickableMenu __instance, int x, int y)
            {

                bool found = false;
                if (__instance.allClickableComponents != null && __instance.GetType().Name == "RSVWorldMap")
                {
                    // RSV uses component x,y's that are not offset, however they need to be offset to check for the mouse position
                    found = CheckClickableComponents(__instance.allClickableComponents, 0, 0, x - __instance.xPositionOnScreen, y - __instance.yPositionOnScreen);
                }
                return !found;
            }
        }
    }
}