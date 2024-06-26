﻿using HarmonyLib;
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
        public static bool CheckClickableComponents(Dictionary<string, ClickableComponent> components, int topX, int topY, int x, int y)
        {
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
            // components.Sort(delegate (ClickableComponent a, ClickableComponent b)
            // {
            //     return (a.bounds.Height * a.bounds.Width).CompareTo(b.bounds.Height * b.bounds.Width);
            // });
            foreach (var c in components)
            { 
                // Offset so that this works for any screen size
                // string altId = $"{c.Value.bounds.X - topX}.{c.Value.bounds.Y - topY}";
                // Predicate<Coordinates> findMatch = (o) => o.id == c.Value.myID || (c.Value.myID == ClickableComponent.ID_ignore && o.altId == altId);
                // Coordinates co = coordinates.coordinates.Find(findMatch);
                // if (co == null)
                // {
                //     co = addedCoordinates.coordinates.Find(findMatch);
                //     if (co == null)
                //     {
                //         if (c.Value.myID == ClickableComponent.ID_ignore)
                //         {
                //             addedCoordinates.Add(new Coordinates() { name = c.Value.name, altId = altId, enabled = false });
                //         }
                //         else
                //         {
                //             addedCoordinates.Add(new Coordinates() { name = c.Value.name, id = c.Value.myID, enabled = false });
                //         }
                //         SMonitor.Log($"Added: {{ \"name\":\"{c.Value.name}\", \"id\":{c.Value.myID}, \"altId\":\"{altId}\" }}", LogLevel.Trace);
                //         added = true;
                //     }
                //     // else check if the coordinate is enabled
                // }

                // if (c.Value.containsPoint(x, y) && co.enabled)
                // {
                //     SMonitor.Log($"Teleporting to {c.Value.name} ({(co.altId != null ? co.altId : co.id)}), {co.mapName}, {co.x},{co.y}", LogLevel.Debug);
                //     Game1.activeClickableMenu?.exitThisMenu(true);
                //     Game1.warpFarmer(co.mapName, co.x, co.y, false);
                //     found = true;
                //     break;
                // }

                if (c.Value.containsPoint(x, y))
                {
                    // Split the input string based on the slash
                    string[] parts = c.Value.name.Split('/');
                    string mapName = parts[0];
                    int coordinateID = c.Value.myID;
                    SMonitor.Log($"Teleport to MapName: {mapName}", LogLevel.Debug);
                    SMonitor.Log($"Location: {c.Value.name}", LogLevel.Debug);
                    SMonitor.Log($"ID : {coordinateID}", LogLevel.Debug);

                    CoordinatesList coordinatesList = SHelper.Data.ReadJsonFile<CoordinatesList>("assets/coordinates.json");


                    //Coordinates destination;
                    foreach (Coordinates coord in coordinatesList.coordinates) {
                        if (coord.id == coordinateID) { // Found Coordinate


                            Console.WriteLine($"Farm Type: {Game1.GetFarmTypeKey()}");
                            bool willTeleport = true;
                            if (Config.ProgressionModeEnabled) {
                                // Check all restricted areas , first Mines
                                willTeleport = CheckMinesUnlock(coord);
                            }
                            // Is event up
                            //Game1.eventUp

                            if (willTeleport) {
                                SMonitor.Log($"[Teleport to] : {coord.id} {coord.x} {coord.y}");
                                Console.WriteLine($"[Teleport to] : {coord.id} {coord.x} {coord.y}");
                                Game1.activeClickableMenu?.exitThisMenu(true);
                                Game1.warpFarmer(mapName, coord.x, coord.y, false);
                            }
                        }
                    }

                    //Game1.activeClickableMenu?.exitThisMenu(true);
                    //Game1.warpFarmer(teleportTo, destination.x, co.y, false);
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

        public static bool CheckMinesUnlock(Coordinates coord) 
        {
            // Mines/AdventureGuild/Quarry won't be accessible until 5th day of year 1
            int[] mineAreas = {1013, 1015, 1016};
            int currentYear = Game1.year;
            int currentDayOfMonth = Game1.dayOfMonth;
            return !(currentYear == 1 && currentDayOfMonth < 5 && Array.Exists(mineAreas, element => element == coord.id));
        }

        [HarmonyPatch(typeof(MapPage), nameof(MapPage.receiveLeftClick))]
        public class MapPage_receiveLeftClick_Patch
        {
            public static bool Prefix(MapPage __instance, int x, int y)
            {
                
                //bool found = CheckClickableComponents(__instance.points, __instance.xPositionOnScreen, __instance.yPositionOnScreen, x, y);
                //return !found;
                //Game1.addHUDMessage(new HUDMessage("Fuck", 3));

                string message = "This looks like a typewriter ... ^But it's not ...^It's a computer.^";
                Game1.activeClickableMenu = new DialogueBox(message);
                return false;
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
                    //found = CheckClickableComponents(__instance.allClickableComponents, 0, 0, x - __instance.xPositionOnScreen, y - __instance.yPositionOnScreen);
                }
                return !found;
            }
        }
    }
}