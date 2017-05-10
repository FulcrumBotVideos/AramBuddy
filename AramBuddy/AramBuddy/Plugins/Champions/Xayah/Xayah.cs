using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AramBuddy.MainCore.Common;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Menu;
using SharpDX;

namespace AramBuddy.Plugins.Champions.Xayah
{
    class Xayah : Base
    {
        private static string featerName = "Xayah_Base_Passive_Dagger_indicator8s.troy";

        private static List<XayahFeather> xayahFeathers = new List<XayahFeather>();

        static Xayah()
        {
            MenuIni = MainMenu.AddMenu(MenuName, MenuName);
            ComboMenu = MenuIni.AddSubMenu("Combo");
            HarassMenu = MenuIni.AddSubMenu("Harass");
            LaneClearMenu = MenuIni.AddSubMenu("LaneClear");
            KillStealMenu = MenuIni.AddSubMenu("KillSteal");

            foreach (var spell in SpellList.Where(s => s != E))
            {
                ComboMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                if (spell != R)
                {
                    HarassMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                    HarassMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                    LaneClearMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
                    LaneClearMenu.CreateSlider(spell.Slot + "mana", spell.Slot + " Mana Manager", 60);
                }
                KillStealMenu.CreateCheckBox(spell.Slot, "Use " + spell.Slot);
            }
            ComboMenu.CreateCheckBox("Esnare", "Use E To Snare Target");

            GameObject.OnCreate += GameObject_OnCreate;
            Orbwalker.OnAttack += Orbwalker_OnAttack;
        }

        private static void Orbwalker_OnAttack(AttackableUnit target, EventArgs args)
        {
            if (target is AIHeroClient && W.IsReady() && ComboMenu.CheckBoxValue(SpellSlot.W))
                W.Cast();
        }

        private static void GameObject_OnCreate(GameObject sender, EventArgs args)
        {
            var minion = sender as Obj_AI_Minion;
            if(minion == null)
                return;

            if(minion.Name != "Feather")
                return;

            xayahFeathers.Add(new XayahFeather(minion));
        }

        public override void Active()
        {
            xayahFeathers.RemoveAll(x => !x.IsValid);
        }

        private class rHits
        {
            public Vector3 castPos;
            public int hits;
            public Geometry.Polygon poly;
        }

        public override void Combo()
        {
            if (E.IsReady() && ComboMenu.CheckBoxValue("Esnare"))
            {
                var target = EntityManager.Heroes.Enemies.FirstOrDefault(x => x.IsKillable() && this.WillSnare(x));
                if (target != null)
                    E.Cast(target.ServerPosition);
            }

            if (ComboMenu.CheckBoxValue(SpellSlot.R))
            {
                var targets = EntityManager.Heroes.Enemies.FindAll(e => e.IsKillable(R.Range));

                if (targets.Count >= 2)
                {
                    var results = targets.Select(t => new Geometry.Polygon.Sector(user.ServerPosition, R.GetPrediction(t).CastPosition, (float)(R.SetSkillshot().ConeAngleDegrees * Math.PI / 180), R.Range))
                        .Select(sector => new rHits { castPos = sector.CenterOfPolygon().To3D(), poly = sector, hits = targets.Count(sector.IsInside) }).ToList();

                    var bestHits = results.OrderByDescending(e => e.hits).FirstOrDefault(r => r.hits >= 2);
                    if (bestHits != null)
                        R.Cast(bestHits.castPos);
                }
            }

            if (!Q.IsReady() && ComboMenu.CheckBoxValue(SpellSlot.Q))
                return;

            var qTarget = Q.GetTarget();
            if(!qTarget.IsKillable())
                return;

            Q.Cast(qTarget);
        }

        public override void Flee()
        {

        }

        public override void Harass()
        {
            var target = Q.GetTarget();
            if (target == null || !target.IsKillable(Q.Range))
                return;

            if (Q.IsReady() && HarassMenu.CheckBoxValue(Q.Slot) && HarassMenu.CompareSlider(Q.Slot + "mana", user.ManaPercent))
            {
                Q.CastAOE(1, Q.Range, target);
            }
        }

        public override void LaneClear()
        {
            var linefarmloc = Q.SetSkillshot().GetBestLinearCastPosition(Q.LaneMinions());
            if (Q.IsReady() && linefarmloc.HitNumber > 1 && LaneClearMenu.CheckBoxValue(SpellSlot.Q) && LaneClearMenu.CompareSlider(Q.Slot + "mana", user.ManaPercent))
            {
                Q.Cast(linefarmloc.CastPosition);
            }
        }

        public override void KillSteal()
        {
            foreach (var target in EntityManager.Heroes.Enemies.Where(m => m != null))
            {
                if (Q.IsReady() && KillStealMenu.CheckBoxValue(Q.Slot) && target.IsKillable(Q.Range) && this.QDmg(target) > target.TotalShieldHealth())
                {
                    Q.CastAOE(1, Q.Range, target);
                }
            }
        }

        public bool WillSnare(Obj_AI_Base target)
        {
            return xayahFeathers.Count(f => f.WillHit(target)) > 2;
        }

        private float QDmg(Obj_AI_Base target)
        {
            return user.CalculateDamageOnUnit(target, DamageType.Physical, (20 * Q.Level + (0.5f * user.FlatPhysicalDamageMod) * 2));
        }
    }
}
