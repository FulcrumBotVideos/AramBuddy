using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using static AramBuddy.MainCore.Common.Misc;

namespace AramBuddy.Plugins.Champions.Taric
{
    internal class Taric : Base
    {
        static Taric()
        {
            MenuIni = MainMenu.AddMenu(MenuName, MenuName);
            AutoMenu = MenuIni.AddSubMenu("Auto");
            ComboMenu = MenuIni.AddSubMenu("Combo");
            HarassMenu = MenuIni.AddSubMenu("Harass");
            LaneClearMenu = MenuIni.AddSubMenu("LaneClear");
            KillStealMenu = MenuIni.AddSubMenu("KillSteal");

            foreach (var spell in SpellList)
            {
                ComboMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                HarassMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                HarassMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                LaneClearMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                LaneClearMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                KillStealMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
            }
            AutoMenu.CreateCheckBox("AutoRteam", "Auto Ult on TeamFight");
            Interrupter.OnInterruptableSpell += Interrupter_OnInterruptableSpell;
        }

        private static void Interrupter_OnInterruptableSpell(Obj_AI_Base sender, Interrupter.InterruptableSpellEventArgs e)
        {
            if (E == null || sender == null || !sender.IsEnemy || !sender.IsKillable(E.Range) || !E.IsReady())
                return;

            E.Cast(sender.ServerPosition);
        }

        public override void Active()
        {
        }

        public override void Combo()
        {
            var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            if (target == null || !target.IsKillable(E.Range))
                return;

            foreach (var spell in SpellList.Where(s => s != null && s.IsReady() && target.IsKillable(s.Range) && ComboMenu.CheckBoxValue(s.Slot)))
            {
                spell.Cast(target);
            }
        }

        public override void Harass()
        {
            var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            if (target == null || !target.IsKillable(E.Range))
                return;

            foreach (var spell in SpellList.Where(s => s != null && s.IsReady() && target.IsKillable(s.Range) && HarassMenu.CheckBoxValue(s.Slot) && HarassMenu.CompareSlider(s.Slot + "mana", user.ManaPercent)))
            {
                spell.Cast(target);
            }
        }

        public override void LaneClear()
        {
        }

        public override void Flee()
        {
        }

        public override void KillSteal()
        {
            if (E.IsReady() && KillStealMenu.CheckBoxValue(E.Slot))
            {
                foreach (var target in EntityManager.Heroes.Enemies.Where(m => E.WillKill(m) && m.IsKillable(E.Range)))
                {
                    E.Cast(target.ServerPosition);
                }
            }
        }
    }
}
