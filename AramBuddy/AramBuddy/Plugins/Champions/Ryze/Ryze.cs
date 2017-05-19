using System.Linq;
using AramBuddy.MainCore.Common;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;

namespace AramBuddy.Plugins.Champions.Ryze
{
    internal class Ryze : Base
    {
        static Ryze()
        {
            MenuIni = MainMenu.AddMenu(MenuName, MenuName);
            AutoMenu = MenuIni.AddSubMenu("Auto");
            ComboMenu = MenuIni.AddSubMenu("Combo");
            HarassMenu = MenuIni.AddSubMenu("Harass");
            LaneClearMenu = MenuIni.AddSubMenu("LaneClear");
            KillStealMenu = MenuIni.AddSubMenu("KillSteal");

            AutoMenu.CreateCheckBox("gapw", "Anti GapCloser W");
            foreach (var spell in SpellList)
            {
                if (spell != R)
                {
                    ComboMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                    HarassMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                    HarassMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                    LaneClearMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                    LaneClearMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                    KillStealMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                }
            }
            E.CastDelay = 250;

            Gapcloser.OnGapcloser += Gapcloser_OnGapcloser;
        }

        private static void Gapcloser_OnGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs e)
        {
            if(!AutoMenu.CheckBoxValue("gapw"))
                return;

            if(!W.IsReady() || !sender.IsValidTarget(W.Range) || !sender.IsEnemy)
                return;

            W.Cast(sender);
        }

        public override void Active()
        {

        }

        public override void Combo()
        {
            if (ComboMenu.CheckBoxValue(SpellSlot.E) && E.IsReady())
            {
                var etarget = bestETarget;
                if (etarget != null)
                    E.Cast(etarget);
            }
            if (ComboMenu.CheckBoxValue(SpellSlot.Q) && Q.IsReady())
            {
                var qtarget = bestQTarget;
                if (qtarget != null)
                    Q.Cast(qtarget, 36);
            }
            if (ComboMenu.CheckBoxValue(SpellSlot.W) && W.IsReady())
            {
                var wtarget = W.GetTarget();
                if (wtarget != null)
                    W.Cast(wtarget);
            }
        }

        public override void Flee()
        {
        }

        public override void Harass()
        {
            if (HarassMenu.CheckBoxValue(SpellSlot.E) && HarassMenu.CompareSlider("Emana", user.ManaPercent) && E.IsReady())
            {
                var etarget = bestETarget;
                if (etarget != null)
                    E.Cast(etarget);
            }
            if (ComboMenu.CheckBoxValue(SpellSlot.Q) && HarassMenu.CompareSlider("Qmana", user.ManaPercent) && Q.IsReady())
            {
                var qtarget = bestQTarget;
                if (qtarget != null)
                    Q.Cast(qtarget, 36);
            }
            if (ComboMenu.CheckBoxValue(SpellSlot.W) && HarassMenu.CompareSlider("Wmana", user.ManaPercent) && W.IsReady())
            {
                var wtarget = W.GetTarget();
                if (wtarget != null)
                    W.Cast(wtarget);
            }
        }

        public override void LaneClear()
        {
        }

        public override void KillSteal()
        {
            if (KillStealMenu.CheckBoxValue(SpellSlot.E) && E.IsReady())
            {
                var etarget = E.GetKillStealTarget();
                if (etarget != null)
                    E.Cast(etarget);
            }
            if (KillStealMenu.CheckBoxValue(SpellSlot.Q) && Q.IsReady())
            {
                var qtarget = Q.GetKillStealTarget();
                if (qtarget != null)
                    Q.Cast(qtarget, 36);
            }
            if (KillStealMenu.CheckBoxValue(SpellSlot.W) && W.IsReady())
            {
                var wtarget = W.GetKillStealTarget();
                if (wtarget != null)
                    W.Cast(wtarget);
            }
        }

        private string EBuff = "RyzeE";
        private bool hasEBuff(Obj_AI_Base target) => target != null && target.HasBuff(this.EBuff);
        private Obj_AI_Base bestETarget
        {
            get
            {
                var possibleAoE =
                    EntityManager.Enemies.FindAll(
                        a => a.IsValidTarget(E.Range) && hasEBuff(a)
                        && EntityManager.Heroes.Enemies.Count(b => b.IsValidTarget()
                        && (b.PredictPosition(E.CastDelay).IsInRange(a.PredictPosition(E.CastDelay), 375)
                        || EntityManager.Enemies.Any(c => c.IsValidTarget() && hasEBuff(c) && c != a && c != b
                        && c.PredictPosition(E.CastDelay).IsInRange(b.PredictPosition(E.CastDelay), 375))) && b != a) > 0);

                if (possibleAoE.Any())
                {
                    return
                        possibleAoE.OrderByDescending(a => EntityManager.Heroes.Enemies.Count(b => b.IsValidTarget()
                        && (b.PredictPosition(E.CastDelay).IsInRange(a.PredictPosition(E.CastDelay), 375)
                        || EntityManager.Enemies.Any(c => c.IsValidTarget() && hasEBuff(c) && c != a && c != b
                        && c.PredictPosition(E.CastDelay).IsInRange(b.PredictPosition(E.CastDelay), 375)))))
                        .FirstOrDefault();
                }
                
                return TargetSelector.GetTarget(E.Range, DamageType.Magical);
            }
        }
        private AIHeroClient bestQTarget => TargetSelector.GetTarget(EntityManager.Heroes.Enemies.FindAll(this.hasEBuff), DamageType.Magical) ?? Q.GetTarget();
    }
}
