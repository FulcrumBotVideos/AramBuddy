using System;
using System.Linq;
using AramBuddy.MainCore.Common;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using SharpDX;

namespace AramBuddy.Plugins.Champions.Teemo
{
    public class Teemo : Base
    {
        static Teemo()
        {
            /*Messages.OnMessage += delegate (Messages.WindowMessage message) //helpful to extract positions for shrooms
            {
                if (message.Message == WindowMessages.LeftButtonDoubleClick)
                    Console.WriteLine($"            new Location(new Vector3({Game.CursorPos.X}f, {Game.CursorPos.Y}f, {Game.CursorPos.Z}f)),");
            };*/


            MenuIni = MainMenu.AddMenu(MenuName, MenuName);
            AutoMenu = MenuIni.AddSubMenu("Auto");
            ComboMenu = MenuIni.AddSubMenu("Combo");
            HarassMenu = MenuIni.AddSubMenu("Harass");
            LaneClearMenu = MenuIni.AddSubMenu("LaneClear");
            KillStealMenu = MenuIni.AddSubMenu("KillSteal");

            foreach (var spell in SpellList.Where(s => s == Q))
            {
                ComboMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                HarassMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                HarassMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                LaneClearMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot, false);
                LaneClearMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                KillStealMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
            }

            ComboMenu.CreateCheckBox(SpellSlot.R, "Use R");

            AutoMenu.CreateCheckBox("autoR", "Auto Place Shrooms");

            R.SetSkillshot().Width = 300;

            GameObject.OnCreate += GameObject_OnCreate;
            GameObject.OnDelete += GameObject_OnDelete;
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            var shroom = sender as Obj_AI_Minion;
            if (shroom == null || !shroom.BaseSkinName.Equals("TeemoMushroom"))
                return;

            setTrap(shroom.ServerPosition, true);
        }

        private static void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            var shroom = sender as Obj_AI_Minion;
            if (shroom == null || !shroom.BaseSkinName.Equals("TeemoMushroom"))
                return;

            setTrap(shroom.ServerPosition, false);
        }

        private static float lastCheck;
        private static bool CantPlaceShrooms;
        public override void Active()
        {
            if(Core.GameTickCount - lastCheck < 7500 || !R.IsReady())
                return;

            CantPlaceShrooms = false;
            lastCheck = Core.GameTickCount;
            R.Range = (uint)(R.Level * 250 + 150);

            if(!AutoMenu.CheckBoxValue("autoR"))
                return;

            foreach (var location in trapLocations) // verify shrooms
                location.Placed = ObjectManager.Get<Obj_AI_Minion>().Any(m => m.IsValidTarget() && m.BaseSkinName == "TeemoMushroom" && m.IsInRange(location.Position, 100));

            var placeLocation = traPosition;
            if (traPosition.HasValue)
            {
                R.Cast(placeLocation.GetValueOrDefault().Random(25));
                setTrap(placeLocation.GetValueOrDefault(), true);
                return;
            }

            CantPlaceShrooms = true;
        }
        
        public override void Combo()
        {
            if (ComboMenu.CheckBoxValue(SpellSlot.Q) && Q.IsReady())
            {
                var qtarget = Q.GetTarget();
                if (qtarget != null)
                    Q.Cast(qtarget);
            }

            if (CantPlaceShrooms && ComboMenu.CheckBoxValue(SpellSlot.R) && R.IsReady())
            {
                R.CastAOE(3);
            }
        }

        public override void Flee()
        {

        }

        public override void Harass()
        {

        }

        public override void LaneClear()
        {

        }

        public override void KillSteal()
        {
            var qt = Q.GetKillStealTarget();
            if (qt != null && KillStealMenu.CheckBoxValue(SpellSlot.Q) && Q.IsReady())
                Q.Cast(qt);
        }

        private static Location[] trapLocations =
            {
            new Location(new Vector3(4784.763f, 3951.439f, -178.3094f)),
            new Location(new Vector3(3972.736f, 4963.316f, -178.3094f)),
            new Location(new Vector3(4360.358f, 5348.081f, -178.3093f)),
            new Location(new Vector3(5927.629f, 5201.447f, -178.3096f)),
            new Location(new Vector3(5380.614f, 6171.993f, -178.3406f)),
            new Location(new Vector3(6003.221f, 6498.971f, -178.3094f)),
            new Location(new Vector3(6353.156f, 6817.717f, -178.3094f)),
            new Location(new Vector3(6720.055f, 7577.299f, -178.3094f)),
            new Location(new Vector3(7573.925f, 6787.135f, -178.3094f)),
            new Location(new Vector3(8880.307f, 7884.902f, -178.3094f)),
            new Location(new Vector3(7584.301f, 8418.354f, -178.3093f)),
            new Location(new Vector3(7920.655f, 8757.627f, -178.3093f)),
            };

        private class Location
        {
            public Location(Vector3 loc)
            {
                this.Position = loc;
            }
            public Vector3 Position;
            public bool Placed;
        }

        private static void setTrap(Vector3 location, bool value)
        {
            var trap = trapLocations.OrderBy(x => x.Position.Distance(location)).FirstOrDefault(x => x.Position.IsInRange(location, 100));
            if (trap != null)
                trap.Placed = value;
        }

        private static Vector3? traPosition => trapLocations.OrderBy(x => x.Position.Distance(user)).FirstOrDefault(x => !x.Placed && R.IsInRange(x.Position))?.Position;
    }
}
