using System;
using System.Linq;
using AramBuddy.MainCore.Common;
using AramBuddy.MainCore.Logics.Casting;
using AramBuddy.MainCore.Utility.GameObjects;
using EloBuddy;
using EloBuddy.SDK;
using SharpDX;
using static AramBuddy.MainCore.Common.Misc;
using static AramBuddy.Config;

namespace AramBuddy.MainCore.Logics
{
    internal class Pathing
    {
        /// <summary>
        ///     Bot movements position.
        /// </summary>
        public static Vector3 Position;

        /// <summary>
        ///     Picking best Position to move to.
        /// </summary>
        public static Vector3 BestPosition()
        {
            if (EnableTeleport && ObjectsManager.ClosestAlly != null)
            {
                Program.Moveto = "Teleporting";
                Teleport.Cast();
            }
            
            // If player is Zombie moves follow nearest Enemy.
            if (Player.Instance.IsZombie())
            {
                var ZombieTarget = TargetSelector.GetTarget(1000, DamageType.Mixed);
                if (ZombieTarget != null)
                {
                    Program.Moveto = "ZombieTarget";
                    Position = ZombieTarget.PredictPosition();
                    return Position;
                }
                if (ObjectsManager.NearestEnemy != null)
                {
                    Program.Moveto = "NearestEnemy";
                    Position = ObjectsManager.NearestEnemy.PredictPosition();
                    return Position;
                }
                if (ObjectsManager.NearestEnemyMinion != null)
                {
                    Program.Moveto = "NearestEnemyMinion";
                    Position = ObjectsManager.NearestEnemyMinion.PredictPosition();
                    return Position;
                }
            }

            // Feeding Poros
            var poro = ObjectsManager.ClosesetPoro;
            if (poro != null)
            {
                var porosnax = new Item(2052);
                if (porosnax != null && porosnax.IsOwned(Player.Instance) && porosnax.IsReady())
                {
                    porosnax.Cast(poro);
                    Logger.Send("Feeding ClosesetPoro");
                }
            }

            // Moves to HealthRelic if the bot needs heal.
            var needHR = Player.Instance.PredictHealthPercent() <= HealthRelicHP || Player.Instance.ManaPercent <= HealthRelicMP && !Player.Instance.IsNoManaHero();
            var healthRelic = ObjectsManager.HealthRelic;
            if (needHR && healthRelic != null)
            {
                var allyneedHR = EntityManager.Heroes.Allies.Any(a => !a.IsMe && Player.Instance.PredictHealth() > a.PredictHealth()
                        && a.Path.LastOrDefault(p => p.IsInRange(healthRelic, a.BoundingRadius + healthRelic.BoundingRadius)) != null);

                if (!allyneedHR && DontStealHR || !DontStealHR)
                {
                    var safeHR = Player.Instance.SafePath(healthRelic) || Player.Instance.Distance(healthRelic) <= 200;
                    if (safeHR)
                    {
                        var formana = Player.Instance.ManaPercent <= HealthRelicMP && !Player.Instance.IsNoManaHero();
                        if (healthRelic.Name.Contains("Bard") && !formana)
                        {
                            Program.Moveto = "BardShrine";
                            Position = healthRelic.Position.Random(MyHero.Instance.BoundingRadius);
                            return Position;
                        }
                        Program.Moveto = "HealthRelic";
                        Position = healthRelic.Position.Random(MyHero.Instance.BoundingRadius);
                        return Position;
                    }
                }
            }

            // Hunting Bard chimes kappa.
            var BardChime = ObjectsManager.BardChime;
            if (PickBardChimes && BardChime != null)
            {
                Program.Moveto = "BardChime";
                Position = BardChime.Position.Random(MyHero.Instance.BoundingRadius);
                return Position;
            }

            // Pick Thresh Lantern
            var ThreshLantern = ObjectsManager.ThreshLantern;
            if (ThreshLantern != null)
            {
                if (Player.Instance.Distance(ThreshLantern) > 300)
                {
                    Program.Moveto = "ThreshLantern";
                    Position = ThreshLantern.Position.Random();
                }
                else
                {
                    Program.Moveto = "ThreshLantern";
                    Player.UseObject(ThreshLantern);
                }
                return Position;
            }

            if (PickDravenAxe && ObjectsManager.DravenAxe != null)
            {
                Program.Moveto = "DravenAxe";
                Position = ObjectsManager.DravenAxe.Position;
                return Position;
            }

            if (PickOlafAxe && ObjectsManager.OlafAxe != null)
            {
                Program.Moveto = "OlafAxe";
                Position = ObjectsManager.OlafAxe.Position;
                return Position;
            }

            if (PickZacBlops && ObjectsManager.ZacBlop != null)
            {
                Program.Moveto = "ZacBlop";
                Position = ObjectsManager.ZacBlop.Position;
                return Position;
            }
            
            /* fix core pls not working :pepe:
            if (PickCorkiBomb && ObjectsManager.CorkiBomb != null)
            {
                Program.Moveto = "CorkiBomb";
                if (Player.Instance.IsInRange(ObjectsManager.CorkiBomb, 300))
                {
                    Program.Moveto = "UsingCorkiBomb";
                    Player.UseObject(ObjectsManager.CorkiBomb);
                }
                Position = ObjectsManager.CorkiBomb.Position;
                return;
            }*/

            // Moves to the Farthest Ally if the bot has Autsim
            if (Brain.Alone() && ObjectsManager.FarthestAllyToFollow != null && Player.Instance.Distance(ObjectsManager.AllySpawn) <= 3000)
            {
                Program.Moveto = "FarthestAllyToFollow";
                Position = ObjectsManager.FarthestAllyToFollow.PredictPosition().Random();
                return Position;
            }

            // Stays Under tower if the bot health under 10%.
            if ((ModesManager.CurrentMode == ModesManager.Modes.Flee || (Player.Instance.PredictHealthPercent() < 10 && Player.Instance.CountAllyHeros(SafeValue + 2000) < 3))
                && EntityManager.Heroes.Enemies.Count(e => e.IsValid && !e.IsDead && e.IsInRange(Player.Instance, SafeValue + 200)) > 0)
            {
                if (ObjectsManager.SafeAllyTurret != null)
                {
                    Program.Moveto = "SafeAllyTurretFlee";
                    Position = ObjectsManager.SafeAllyTurret.PredictPosition().Random().Extend(ObjectsManager.AllySpawn.Position.Random(), 400).To3D();
                    return Position;
                }
                if (ObjectsManager.AllySpawn != null)
                {
                    Program.Moveto = "AllySpawnFlee";
                    Position = ObjectsManager.AllySpawn.Position.Random();
                    return Position;
                }
            }

            // Moves to AllySpawn if the bot is diving and it's not safe to dive.
            if (((Player.Instance.UnderEnemyTurret() && !SafeToDive) || MyHero.TurretAttackingMe) && ObjectsManager.AllySpawn != null)
            {
                Program.Moveto = "AllySpawn2";
                Position = ObjectsManager.AllySpawn.Position.Random();
                return Position;
            }

            return Position = Player.Instance.GetAutoAttackRange() < 425 ? MeleeLogic() : RangedLogic();
        }

        /// <summary>
        ///     Melee Champions Logic.
        /// </summary>
        public static Vector3 MeleeLogic()
        {
            var AllySpawn = ObjectsManager.AllySpawn;

            // if there is a TeamFight follow NearestEnemy.
            var NearestEnemy = ObjectsManager.NearestEnemy;
            if (Brain.TeamFightActive && Player.Instance.PredictHealthPercent() > 20 && !(ModesManager.CurrentMode == ModesManager.Modes.None || ModesManager.CurrentMode == ModesManager.Modes.Flee)
                && NearestEnemy != null && NearestEnemy.TeamTotal() >= NearestEnemy.TeamTotal(true)
                && NearestEnemy.CountAllyHeros(SafeValue) > 1)
            {
                // if there is a TeamFight move from NearestEnemy to nearestally.
                if (ObjectsManager.SafestAllyToFollow != null)
                {
                    var pos = NearestEnemy.KitePos(ObjectsManager.SafestAllyToFollow);
                    if (Player.Instance.SafePath(pos))
                    {
                        Program.Moveto = "NearestEnemyToNearestAlly";
                        Position = pos;
                        return pos;
                    }
                }
                
                // if there is a TeamFight move from NearestEnemy to AllySpawn.
                if (AllySpawn != null)
                {
                    var pos = NearestEnemy.KitePos(AllySpawn);
                    if (Player.Instance.SafePath(pos))
                    {
                        Program.Moveto = "NearestEnemyToAllySpawn";
                        Position = pos;
                        return Position;
                    }
                }
            }

            // Tower Defence
            var FarthestAllyTurret = ObjectsManager.FarthestAllyTurret;
            if (Player.Instance.IsUnderHisturret() && FarthestAllyTurret != null && Player.Instance.PredictHealthPercent() >= 20
                && !(ModesManager.CurrentMode == ModesManager.Modes.None || ModesManager.CurrentMode == ModesManager.Modes.Flee))
            {
                if (FarthestAllyTurret.CountEnemyHeros((int)FarthestAllyTurret.GetAutoAttackRange(Player.Instance) + 50) > 0)
                {
                    var enemy = EntityManager.Heroes.Enemies.OrderBy(o => o.Distance(AllySpawn)).FirstOrDefault(e => e.IsKillable(3000)
                    && e.TeamTotal() >= e.TeamTotal(true) && (e.CountAllyHeros(SafeValue) > 1 || e.CountEnemyHeros(SafeValue) < 2) && e.UnderEnemyTurret());
                    if (enemy != null && enemy.UnderEnemyTurret())
                    {
                        Program.Moveto = "EnemyUnderTurret";
                        Position = enemy.KitePos(AllySpawn);
                        return Position;
                    }
                }
            }

            var NearestEnemyObject = ObjectsManager.NearestEnemyObject;
            // if Can AttackObject then start attacking THE DAMN OBJECT FFS.
            if (NearestEnemyObject != null && Player.Instance.PredictHealthPercent() > 20 && ModesManager.AttackObject
                && (NearestEnemyObject.Position.TeamTotal() > NearestEnemyObject.Position.TeamTotal(true)
                || NearestEnemyObject.CountEnemyHeros(SafeValue + 100) == 0))
            {
                var extendto = new Vector3();
                if (AllySpawn != null)
                {
                    extendto = AllySpawn.Position;
                }
                if (ObjectsManager.NearestMinion != null)
                {
                    extendto = ObjectsManager.NearestMinion.Position;
                }
                if (ObjectsManager.NearestAlly != null)
                {
                    extendto = ObjectsManager.NearestAlly.Position;
                }
                var extendtopos = NearestEnemyObject.KitePos(extendto);
                var rect = new Geometry.Polygon.Rectangle(Player.Instance.ServerPosition, NearestEnemyObject.Position, 400);
                var Enemy = EntityManager.Heroes.Enemies.Any(a => a != null && a.IsValid && a.TeamTotal(true) > a.TeamTotal() && !a.IsDead && new Geometry.Polygon.Circle(a.PredictPosition(), a.GetAutoAttackRange(Player.Instance)).Points.Any(p => rect.IsInside(p)));
                if (!Enemy)
                {
                    if (ObjectsManager.EnemyTurret != null)
                    {
                        var TurretCircle = new Geometry.Polygon.Circle(ObjectsManager.EnemyTurret.ServerPosition, ObjectsManager.EnemyTurret.GetAutoAttackRange(Player.Instance));


                        if (NearestEnemyObject.IsTurret())
                        {
                            if (SafeToDive)
                            {
                                Program.Moveto = "NearestEnemyObject";
                                Position = extendtopos;
                                return Position;
                            }
                        }
                        if (!TurretCircle.Points.Any(p => rect.IsInside(p)))
                        {
                            Program.Moveto = "NearestEnemyObject2";
                            Position = extendtopos;
                            return Position;
                        }
                    }
                    else
                    {
                        Program.Moveto = "NearestEnemyObject3";
                        Position = extendtopos;
                        return Position;
                    }
                }
            }

            // if NearestEnemyMinion exsists moves to NearestEnemyMinion.
            if (ObjectsManager.NearestEnemyMinion != null && AllySpawn != null && ModesManager.LaneClear && Player.Instance.PredictHealthPercent() > 25)
            {
                Program.Moveto = "NearestEnemyMinion";
                Position = ObjectsManager.NearestEnemyMinion.PredictPosition().Extend(AllySpawn.Position.Random(), KiteDistance(ObjectsManager.NearestEnemyMinion)).To3D();
                return Position;
            }

            // if SafestAllyToFollow not exsist picks other to follow.
            if (ObjectsManager.SafestAllyToFollow != null)
            {
                // if SafestAllyToFollow exsist follow BestAllyToFollow.
                Program.Moveto = "SafestAllyToFollow";
                Position = ObjectsManager.SafestAllyToFollow.PredictPosition().Random();
                return Position;
            }
            
            // if Minion exsists moves to Minion.
            if (ObjectsManager.AllyMinion != null)
            {
                Program.Moveto = "AllyMinion";
                Position = ObjectsManager.AllyMinion.PredictPosition().Random();
                return Position;
            }

            // if FarthestAllyToFollow exsists moves to FarthestAllyToFollow.
            if (ObjectsManager.FarthestAllyToFollow != null)
            {
                Program.Moveto = "FarthestAllyToFollow";
                Position = ObjectsManager.FarthestAllyToFollow.PredictPosition().Random();
                return Position;
            }

            // if SecondTurret exsists moves to SecondTurret.
            if (ObjectsManager.SecondTurret != null)
            {
                Program.Moveto = "SecondTurret";
                Position = ObjectsManager.SecondTurret.PredictPosition().Extend(AllySpawn, 400).To3D().Random();
                return Position;
            }

            // if SafeAllyTurret exsists moves to SafeAllyTurret.
            if (ObjectsManager.SafeAllyTurret != null)
            {
                Program.Moveto = "SafeAllyTurret";
                Position = ObjectsManager.SafeAllyTurret.ServerPosition.Extend(AllySpawn, 400).To3D().Random();
                return Position;
            }

            // if ClosesetAllyTurret exsists moves to ClosesetAllyTurret.
            if (ObjectsManager.ClosesetAllyTurret != null)
            {
                Program.Moveto = "ClosesetAllyTurret";
                Position = ObjectsManager.ClosesetAllyTurret.ServerPosition.Extend(AllySpawn, 400).To3D().Random();
                return Position;
            }

            // Well if it ends up like this then best thing is to let it end.
            if (AllySpawn != null)
            {
                Program.Moveto = "AllySpawn3";
                Position = AllySpawn.Position.Random();
                return Position;
            }
            return Vector3.Zero;
        }

        /// <summary>
        ///     Ranged Champions Logic.
        /// </summary>
        public static Vector3 RangedLogic()
        {
            var NearestEnemy = ObjectsManager.NearestEnemy;

            // TeamFighting Logic.
            if (NearestEnemy != null && Brain.TeamFightActive && MyHero.Instance.PredictHealthPercent() > 15
                && !(ModesManager.CurrentMode == ModesManager.Modes.Flee || ModesManager.CurrentMode == ModesManager.Modes.None))
            {
                var pos = NearestEnemy.KitePos(Detector.LastAlliesTeamFightPos);
                if (MyHero.Instance.SafePath(pos))
                {
                    Program.Moveto = "KiteNearestEnemy";
                    Position = pos;
                    return Position;
                }
            }

            // Tower Defence
            if (ObjectsManager.FarthestAllyTurret != null && Player.Instance.PredictHealthPercent() >= 20)
            {
                if (ObjectsManager.FarthestAllyTurret.CountEnemyHeros((int)ObjectsManager.FarthestAllyTurret.GetAutoAttackRange(MyHero.Instance) + 50) > 0)
                {
                    var enemy = EntityManager.Heroes.Enemies.OrderBy(o => o.Distance(ObjectsManager.AllySpawn)).FirstOrDefault(e => e.IsKillable() && e.IsUnderEnemyturret()
                    && (e.TeamTotal() >= e.TeamTotal(true) || e.CountAllyHeros(SafeValue) > 1 || e.CountEnemyHeros(SafeValue) < 2));
                    if (enemy != null)
                    {
                        Program.Moveto = "DefendingTower";
                        Position = enemy.KitePos(ObjectsManager.AllySpawn);
                        return Position;
                    }
                }
            }

            // Inhb defend
            var AllyInhb = ObjectsManager.AllyInhb;
            if (AllyInhb != null && AllyInhb.IsValidTarget() && AllyInhb.CountEnemyHeroesInRangeWithPrediction(SafeValue) > 0 && AllyInhb.Position.TeamTotal() > AllyInhb.Position.TeamTotal(true))
            {
                var enemy = EntityManager.Heroes.Enemies.OrderBy(o => o.Distance(ObjectsManager.AllySpawn)).FirstOrDefault(e => e.IsKillable() && e.IsInRange(AllyInhb, SafeValue)
                && (e.TeamTotal() >= e.TeamTotal(true) || e.CountAllyHeros(SafeValue) > 1 || e.CountEnemyHeros(SafeValue) < 2));

                if (enemy != null)
                {
                    Program.Moveto = "DefendingInhbitor";
                    Position = enemy.KitePos(ObjectsManager.AllySpawn);
                    return Position;
                }
            }

            var EnemyObject = ObjectsManager.NearestEnemyObject;
            // if Can AttackObject then start attacking THE DAMN OBJECT FFS.
            if (EnemyObject != null && Player.Instance.PredictHealthPercent() > 20 && ModesManager.AttackObject
                && (EnemyObject.Position.TeamTotal() > EnemyObject.Position.TeamTotal(true) || EnemyObject.CountEnemyHeros(SafeValue + 100) < 1))
            {
                var extendto = new Vector3();
                if (ObjectsManager.AllySpawn != null)
                {
                    extendto = ObjectsManager.AllySpawn.Position;
                }
                if (ObjectsManager.NearestMinion != null)
                {
                    extendto = ObjectsManager.NearestMinion.Position;
                }
                if (ObjectsManager.NearestAlly != null)
                {
                    extendto = ObjectsManager.NearestAlly.Position;
                }
                var extendtopos = ObjectsManager.NearestEnemyObject.KitePos(extendto);
                var rect = new Geometry.Polygon.Rectangle(Player.Instance.ServerPosition, ObjectsManager.NearestEnemyObject.Position, 400);
                var Enemy = EntityManager.Heroes.Enemies.Any(a => a != null && a.IsValid && a.TeamTotal(true) > a.TeamTotal() && !a.IsDead && new Geometry.Polygon.Circle(a.PredictPosition(), a.GetAutoAttackRange(Player.Instance)).Points.Any(p => rect.IsInside(p)));
                if (!Enemy)
                {
                    if (ObjectsManager.EnemyTurret != null)
                    {
                        var TurretCircle = new Geometry.Polygon.Circle(ObjectsManager.EnemyTurret.ServerPosition, ObjectsManager.EnemyTurret.GetAutoAttackRange(Player.Instance));


                        if (ObjectsManager.NearestEnemyObject is Obj_AI_Turret)
                        {
                            if (SafeToDive)
                            {
                                Program.Moveto = "NearestEnemyObject";
                                Position = extendtopos;
                                return Position;
                            }
                        }
                        if (!TurretCircle.Points.Any(p => rect.IsInside(p)))
                        {
                            Program.Moveto = "NearestEnemyObject2";
                            Position = extendtopos;
                            return Position;
                        }
                    }
                    else
                    {
                        Program.Moveto = "NearestEnemyObject3";
                        Position = extendtopos;
                        return Position;
                    }
                }
            }

            // if NearestEnemyMinion exsists moves to NearestEnemyMinion.
            var NearestEnemyMinion = ObjectsManager.NearestEnemyMinion;
            if (NearestEnemyMinion != null && MyHero.Instance.SafePath(NearestEnemyMinion) && NearestEnemyMinion.CountEnemyMinionsInRangeWithPrediction(SafeValue) > 1 && ObjectsManager.AllySpawn != null && Player.Instance.PredictHealthPercent() > 20)
            {
                Program.Moveto = "NearestEnemyMinion";
                Position = NearestEnemyMinion.PredictPosition().Extend(ObjectsManager.AllySpawn.Position.Random(), KiteDistance(NearestEnemyMinion)).To3D();
                return Position;
            }

            // if SafestAllyToFollow2 exsists moves to SafestAllyToFollow2.
            if (ObjectsManager.SafestAllyToFollow2 != null)
            {
                Program.Moveto = "SafestAllyToFollow2";
                Position = ObjectsManager.SafestAllyToFollow2.PredictPosition().Extend(ObjectsManager.AllySpawn, 100).Random();
                return Position;
            }
            
            // if Minion not exsist picks other to follow.
            if (ObjectsManager.AllyMinion != null)
            {
                Program.Moveto = "AllyMinion";
                Position = ObjectsManager.AllyMinion.PredictPosition().Extend(ObjectsManager.AllySpawn, 100).Random();
                return Position;
            }
            
            // if SecondTurret exsists moves to SecondTurret.
            if (ObjectsManager.SecondTurret != null)
            {
                Program.Moveto = "SecondTurret";
                Position = ObjectsManager.SecondTurret.ServerPosition.Extend(ObjectsManager.AllySpawn, 425).To3D().Random();
                return Position;
            }

            // if SafeAllyTurret exsists moves to SafeAllyTurret.
            if (ObjectsManager.SafeAllyTurret != null)
            {
                Program.Moveto = "SafeAllyTurret";
                Position = ObjectsManager.SafeAllyTurret.ServerPosition.Extend(ObjectsManager.AllySpawn, 425).To3D().Random();
                return Position;
            }

            // if ClosesetAllyTurret exsists moves to ClosesetAllyTurret.
            if (ObjectsManager.ClosesetAllyTurret != null)
            {
                Program.Moveto = "ClosesetAllyTurret";
                Position = ObjectsManager.ClosesetAllyTurret.ServerPosition.Extend(ObjectsManager.AllySpawn, 425).To3D().Random();
                return Position;
            }

            // Well if it ends up like this then best thing is to let it end.
            if (ObjectsManager.AllySpawn != null)
            {
                Program.Moveto = "AllySpawn3";
                Position = ObjectsManager.AllySpawn.Position.Random();
                return Position;
            }
            return Vector3.Zero;
        }

        /// <summary>
        ///     Sends movement commands.
        /// </summary>
        public static void MoveTo(Vector3 pos)
        {
            if(pos.IsZero)
                return;

            var pos2 = pos;
            var rnd = new Random().Next(750, 2000);
            if (Player.Instance.Distance(pos) > rnd)
            {
                pos2 = Player.Instance.ServerPosition.Extend(pos, rnd).To3D();
            }

            // This to prevent the bot from spamming unnecessary movements.
            var rnddis = new Random().Next(45, 75);
            if ((!Player.Instance.Path.LastOrDefault().IsInRange(pos2, rnddis) || !Player.Instance.IsMoving) && !Player.Instance.IsInRange(pos2, rnddis) /*&& Core.GameTickCount - lastmove >= new Random().Next(100 + Game.Ping, 600 + Game.Ping)*/)
            {
                // This to prevent diving.
                if (pos2.UnderEnemyTurret() && !SafeToDive)
                {
                    //return;
                }
                
                // This to prevent Walking into walls, buildings or traps.
                if ((pos2.IsWall() || pos2.IsBuilding() || ObjectsManager.EnemyTraps.Any(t => t.Trap.IsInRange(pos2, t.Trap.BoundingRadius * 2))) && !Brain.RunningItDownMid)
                {
                    return;
                }

                // Issues Movement Commands.
                Orbwalker.OrbwalkTo(pos2);
            }
        }
    }
}
