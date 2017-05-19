using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AramBuddy.MainCore.Logics;
using AramBuddy.MainCore.Utility.GameObjects;
using AramBuddy.Plugins.KappaEvade;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;

namespace AramBuddy.MainCore.Common
{
    public static class Misc
    {
        /// <summary>
        ///     Returns Spell Mana Cost.
        /// </summary>
        public static float Mana(this Spell.SpellBase spell)
        {
            return spell.Handle.SData.Mana;
        }

        /// <summary>
        ///     Returns true if target Is CC'D.
        /// </summary>
        public static bool IsCC(this Obj_AI_Base target)
        {
            return !target.CanMove || target.HasBuffOfType(BuffType.Charm) || target.HasBuffOfType(BuffType.Knockback) || target.HasBuffOfType(BuffType.Knockup) || target.HasBuffOfType(BuffType.Fear)
                   || target.HasBuffOfType(BuffType.Snare) || target.HasBuffOfType(BuffType.Stun) || target.HasBuffOfType(BuffType.Suppression) || target.HasBuffOfType(BuffType.Taunt);
            //|| target.HasBuffOfType(BuffType.Grounded);
        }

        /// <summary>
        ///     Cache the TeamTotal to prevent lags.
        /// </summary>
        private static float EnemyTeamTotal;
        private static float AllyTeamTotal;

        /// <summary>
        ///     Last Update Done To The Damages.
        /// </summary>
        private static int lastTeamTotalupdate;

        /// <summary>
        ///     Returns teams totals - used for picking best fights.
        /// </summary>
        public static float TeamTotal(this Vector3 Position, bool Enemy = false)
        {
            if (Core.GameTickCount - lastTeamTotalupdate > 500)
            {
                EnemyTeamTotal = TeamDamage(Position, true);
                AllyTeamTotal = TeamDamage(Position);
                var Allies = EntityManager.Heroes.Allies.Where(e => !e.IsAFK() && e.IsValid && !e.IsDead && (e.IsInRange(Position, Config.SafeValue) || e.IsMe));

                foreach (var t in Allies)
                {
                    AllyTeamTotal += t.GetScore();
                }

                var Aturrets = EntityManager.Turrets.Allies.Where(t => t.IsValidTarget() && t.IsInRange(Position, t.GetAutoAttackRange(Player.Instance)));

                foreach (var t in Aturrets)
                {
                    AllyTeamTotal += t.Health * 0.15f;
                    AllyTeamTotal += t.Armor;
                    AllyTeamTotal += t.SpellBlock;
                    AllyTeamTotal += t.GetAutoAttackDamage(Player.Instance) * 2;
                }

                var Enemies = EntityManager.Heroes.Enemies.Where(e => !e.IsAFK() && e.IsValid && !e.IsDead && e.IsInRange(Position, Config.SafeValue));

                foreach (var t in Enemies)
                {
                    EnemyTeamTotal += t.GetScore();
                }

                var Eturrets = EntityManager.Turrets.Enemies.Where(t => t.IsValidTarget() && t.IsInRange(Position, t.GetAutoAttackRange(Player.Instance)));

                foreach (var t in Eturrets)
                {
                    EnemyTeamTotal += t.Health * 0.15f;
                    EnemyTeamTotal += t.Armor;
                    EnemyTeamTotal += t.SpellBlock;
                    EnemyTeamTotal += t.GetAutoAttackDamage(Player.Instance) * 2;
                }

                lastTeamTotalupdate = Core.GameTickCount;
            }

            return Enemy ? EnemyTeamTotal : AllyTeamTotal;
        }

        public static float TeamTotal(this Obj_AI_Base target, bool Enemy = false)
        {
            return TeamTotal(target.PredictPosition(), Enemy);
        }
        
        /// <summary>
        ///     Returns Spells Damage for the Whole Team.
        /// </summary>
        public static float TeamDamage(Vector3 Position, bool Enemy = false)
        {
            var result = 0f;

            var team = EntityManager.Heroes.AllHeroes.Where(e => (Enemy ? e.IsEnemy : e.IsAlly) && e.IsValidTarget() && e.IsInRange(Position, Config.SafeValue));
            foreach (var hero in team)
            {
                result += hero.GetAllSpellsDamage(Player.Instance);
            }

            return result;
        }

        public static float GetScore(this AIHeroClient t)
        {
            var result = 0f;
            result += t.Health * 0.35f;
            result += t.Mana * 0.15f;
            result += t.Armor;
            result += t.SpellBlock;
            result += t.GetAutoAttackDamage(Player.Instance, true) * 2;
            result += t.GetAllSpellsDamage(MyHero.Instance);
            result += t.TotalAttackDamage;
            result += t.TotalMagicalDamage;

            return result;
        }

        /// <summary>
        ///     Zombie heros list.
        /// </summary>
        public static List<Champion> ZombieHeros = new List<Champion>
        {
            Champion.KogMaw, Champion.Sion, Champion.Karthus
        };

        /// <summary>
        ///     Returns true if the hero Has a Zombie form.
        /// </summary>
        public static bool IsZombie(this AIHeroClient hero)
        {
            return ZombieHeros.Contains(hero.Hero) && hero.IsZombie;
        }
        
        /// <summary>
        ///     Returns true if it's safe to dive.
        /// </summary>
        public static bool SafeToDive
        {
            get
            {
                var turret = ObjectsManager.EnemyTurret;

                var attackrange = turret?.GetAutoAttackRange(Player.Instance) ?? 0;
                return Player.Instance.IsZombie() || turret != null && Player.Instance.PredictHealthPercent() > 15 && !MyHero.TurretAttackingMe
                       && (turret.CountAllyMinionsInRangeWithPrediction((int)attackrange) > 2 || turret.CountAllyHeros((int)attackrange) > 1
                       || (turret.TrackedUnit() != null && turret.TrackedUnit().IsAttacking && turret.TrackedUnit().Target != null && !turret.TrackedUnit().Target.IsMe && turret.TrackedUnit().Target.IsChampion()));
            }
        }

        /// <summary>
        ///     Returns true if it's safe to Attack.
        /// </summary>
        public static bool SafeToAttack
        {
            get
            {
                var turret = ObjectsManager.EnemyTurret;
                return Player.Instance.IsZombie() || !Player.Instance.IsKillable() || !MyHero.TurretAttackingMe
                    && (Player.Instance.ServerPosition.SafeDive() || turret?.LastTarget() != null && turret.LastTarget().IsChampion() && !turret.LastTarget().IsMe);
            }
        }

        /// <summary>
        ///     Returns true if Obj_AI_Base is UnderEnemyTurret.
        /// </summary>
        public static bool UnderEnemyTurret(this Obj_AI_Base target)
        {
            return EntityManager.Turrets.AllTurrets.Any(t => t.Team != target.Team && t.IsValidTarget() && t.IsInRange(target, t.GetAutoAttackRange(target)));
        }

        /// <summary>
        ///     Returns true if Vector3 is UnderEnemyTurret.
        /// </summary>
        public static bool UnderEnemyTurret(this Vector3 pos)
        {
            return EntityManager.Turrets.Enemies.Any(t => !t.IsDead && t.IsValid && t.PredictHealth() > 0 && t.IsInRange(pos, t.GetAutoAttackRange(Player.Instance)));
        }

        /// <summary>
        ///     Returns true if Vector2 is UnderEnemyTurret.
        /// </summary>
        public static bool UnderEnemyTurret(this Vector2 pos)
        {
            return EntityManager.Turrets.Enemies.Any(t => !t.IsDead && t.IsValid && t.PredictHealth() > 0 && t.IsInRange(pos, t.GetAutoAttackRange(Player.Instance)));
        }

        /// <summary>
        ///     Returns true if Vector3 is UnderAlliedTurret.
        /// </summary>
        public static bool UnderAlliedTurret(this Vector3 pos)
        {
            return EntityManager.Turrets.Allies.Any(t => !t.IsDead && t.IsValid && t.PredictHealth() > 0 && t.IsInRange(pos, t.GetAutoAttackRange(Player.Instance)));
        }

        /// <summary>
        ///     Returns true if Vector2 is UnderAlliedTurret.
        /// </summary>
        public static bool UnderAlliedTurret(this Vector2 pos)
        {
            return EntityManager.Turrets.Allies.Any(t => !t.IsDead && t.IsValid && t.PredictHealth() > 0 && t.IsInRange(pos, t.GetAutoAttackRange(Player.Instance)));
        }

        /// <summary>
        ///     Returns true if your team is teamfighting.
        /// </summary>
        public static bool TeamFight
        {
            get
            {
                /*
                var myfight = Player.Instance.CountAllyHeros(Config.SafeValue) >= Player.Instance.CountEnemyHeros(Config.SafeValue)
                              && AutoAttacks.Count(a => Core.GameTickCount - a.LastAttackTick < 200 && a.Attacker is AIHeroClient && a.Attacker.IsEnemy && a.Target != null && a.Target.IsMe) > 1;
                var alliesalive = EntityManager.Heroes.Allies.Count(a => a.IsValidTarget() && a.IsActive());
                return EntityManager.Heroes.Allies.Count(a => (a.IsAttackPlayer() || a.IsAttackingPlayer) && a.CountAllyHeros(Config.SafeValue) > 1 && a.IsValidTarget()) > alliesalive / 2 || myfight;*/
                return Detector.TeamFightActive();
            }
        }

        /// <summary>
        ///     Returns a recreated name of the target.
        /// </summary>
        public static string Name(this Obj_AI_Base target)
        {
            if (ObjectManager.Get<Obj_AI_Base>().Count(o => o.BaseSkinName.Equals(target.BaseSkinName)) > 1)
            {
                return target.BaseSkinName + "(" + target.Name + ")";
            }
            return target.BaseSkinName;
        }

        public static string CleanChampionName(this AIHeroClient hero)
        {
            return hero.ChampionName.CleanChampionName();
        }

        public static string CleanChampionName(this string str)
        {
            var clean = str.Trim().Replace("\'", "").Replace(".", "").Replace(" ", "");
            if (clean.Equals("MonkeyKing"))
                return "Wukong";
            if (clean.Equals("Chogath"))
                return "ChoGath";
            if (clean.Equals("Kogmaw"))
                return "KogMaw";
            if (clean.Equals("Leesin"))
                return "LeeSin";
            if (clean.Equals("Khazix"))
                return "KhaZix";
            if (clean.Equals("Velkoz"))
                return "VelKoz";
            return clean;
        }

        /// <summary>
        ///     Returns the target KDA [BROKEN FROM CORE].
        /// </summary>
        public static float KDA(this AIHeroClient target)
        {
            return target.ChampionsKilled + target.Assists / target.Deaths;
        }

        public static float DistanceFromAllHeros(Vector3 pos)
        {
            return EntityManager.Heroes.AllHeroes.Where(a => a.IsValidTarget()).Sum(a => pos.Distance(a));
        }

        public static float DistanceFromAllHeros(this Obj_AI_Base target)
        {
            return DistanceFromAllHeros(target.ServerPosition);
        }

        public static float DistanceFromAllies(Vector3 pos)
        {
            return EntityManager.Heroes.Allies.Where(a => a.IsValidTarget()).Sum(a => pos.Distance(a));
        }

        public static float DistanceFromAllies(this Obj_AI_Base target)
        {
            return DistanceFromAllies(target.PredictPosition());
        }

        public static float DistanceFromEnemies(Vector3 pos)
        {
            return EntityManager.Heroes.Enemies.Where(a => a.IsValidTarget()).Sum(a => pos.Distance(a));
        }

        public static float DistanceFromEnemies(this Obj_AI_Base target)
        {
            return DistanceFromEnemies(target.PredictPosition());
        }

        public static bool CanKill(this AIHeroClient hero, AIHeroClient target)
        {
            return hero.GetAutoAttackDamage(target, true) >= target.TotalShieldHealth() && target.IsKillable(hero.GetAutoAttackRange(target));
        }

        public static IEnumerable<AIHeroClient> GetKillStealTargets(this Spell.SpellBase spell)
        {
            return EntityManager.Heroes.Enemies.OrderByDescending(TargetSelector.GetPriority).Where(o => spell.WillKill(o) && o.IsKillable());
        }

        public static AIHeroClient GetKillStealTarget(this Spell.SpellBase spell)
        {
            return spell.GetKillStealTargets().FirstOrDefault(o => o.IsKillable(spell.Range));
        }

        public static float AlliesAADamage(this Obj_AI_Base target)
        {
            return EntityManager.Allies.Where(a => a.IsValidTarget() && a.IsInRange(target, a.GetAutoAttackRange(target))).Sum(d => d.GetAutoAttackDamage(target, true));
        }

        /// <summary>
        ///     Returns true if the target stacked with bots
        /// </summary>
        public static bool StackedBots(this AIHeroClient target)
        {
            return EntityManager.Heroes.AllHeroes.Count(h => h.Team == target.Team && !h.IdEquals(target) && h.Distance(target) <= h.BoundingRadius + target.BoundingRadius + 75) > 0;
        }

        /// <summary>
        ///     Returns a recreated name of the target.
        /// </summary>
        public static string Name(this AIHeroClient target)
        {
            if (EntityManager.Heroes.AllHeroes.Count(h => h.BaseSkinName.Equals(target.BaseSkinName)) > 1)
            {
                return target.BaseSkinName + "(" + target.Name + ")";
            }
            return target.BaseSkinName;
        }

        public static int CountEnemyHeros(this Vector3 Pos, float range = 1200, int time = 250)
        {
            return Pos.CountEnemyHeroesInRangeWithPrediction((int)range, time);
        }

        public static int CountEnemyHeros(this Vector2 Pos, float range = 1200, int time = 250)
        {
            return Pos.To3DWorld().CountEnemyHeros(range, time);
        }

        public static int CountEnemyHeros(this Obj_AI_Base target, float range = 1200, int time = 250)
        {
            return target.PredictPosition(time).CountEnemyHeros(range, time);
        }

        public static int CountEnemyHeros(this GameObject target, float range = 1200, int time = 250)
        {
            return target.Position.CountEnemyHeros(range, time);
        }

        public static int CountAllyHeros(this Vector3 Pos, float range = 1200, int time = 250)
        {
            return EntityManager.Heroes.Allies.Count(e => e.IsValidTarget() && e.PredictPosition(time).IsInRange(Pos, range));
        }

        public static int CountAllyHeros(this Obj_AI_Base target, float range = 1200, int time = 250)
        {
            return target.PredictPosition(time).CountAllyHeros(range, time);
        }

        public static int CountAllyHeros(this GameObject target, float range = 1200, int time = 250)
        {
            return target.Position.CountAllyHeros(range, time);
        }

        public static bool IsAirborne(this Obj_AI_Base target)
        {
            try
            {
                return target.HasBuffOfType(BuffType.Knockback) || target.HasBuffOfType(BuffType.Knockup);
            }
            catch (Exception e)
            {
                Logger.Send("ERROR: ", e, Logger.LogLevel.Error);
                return false;
            }
        }

        public static bool IsValidTarget(this GameObject obj)
        {
            if (obj == null || !obj.IsValid || obj.IsDead)
            {
                return false;
            }

            var nexues = obj as Obj_HQ;
            if (nexues != null)
            {
                return !ObjectsManager.EnemyInhb.IsValidTarget() && nexues.IsTargetable;
            }

            var asbase = obj as Obj_AI_Base;
            if (asbase != null)
            {
                return asbase.IsValidTarget(int.MaxValue);
            }

            var inhb = obj as Obj_BarracksDampener;
            if (inhb != null)
            {
                return inhb.Health > 0 && inhb.IsTargetable;
            }

            return true;
        }

        public static bool UnderTurret(this Obj_AI_Base target)
        {
            return EntityManager.Turrets.AllTurrets.Any(t => t.IsValid && !t.IsDead && t.Team != target.Team && t.IsInAutoAttackRange(target));
        }

        /// <summary>
        ///     Returns true if you can deal damage to the target.
        /// </summary>
        public static bool IsKillable(this AIHeroClient target, float range)
        {
            return !target.HasBuff("kindredrnodeathbuff") && !target.HasUndyingBuff(true) && !target.Buffs.Any(b => b.Name.ToLower().Contains("fioraw")) && !target.HasBuff("JudicatorIntervention") && !target.IsZombie
                   && !target.HasBuff("ChronoShift") && !target.HasBuff("UndyingRage") && !target.IsInvulnerable && !target.IsZombie && !target.HasBuff("bansheesveil") && !target.IsDead
                   && !target.IsPhysicalImmune && target.PredictHealth() > 0 && !target.HasBuffOfType(BuffType.Invulnerability) && !target.HasBuffOfType(BuffType.PhysicalImmunity) && target.IsValidTarget(range);
        }
        
        /// <summary>
        ///     Returns true if you can deal damage to the target.
        /// </summary>
        public static bool IsKillable(this AIHeroClient target)
        {
            return !target.HasBuff("kindredrnodeathbuff") && !target.HasUndyingBuff(true) && !target.Buffs.Any(b => b.Name.ToLower().Contains("fioraw")) && !target.HasBuff("JudicatorIntervention") && !target.IsZombie
                   && !target.HasBuff("ChronoShift") && !target.HasBuff("UndyingRage") && !target.IsInvulnerable && !target.IsZombie && !target.HasBuff("bansheesveil") && !target.IsDead
                   && !target.IsPhysicalImmune && target.PredictHealth() > 0 && !target.HasBuffOfType(BuffType.Invulnerability) && !target.HasBuffOfType(BuffType.PhysicalImmunity) && target.IsValidTarget();
        }

        /// <summary>
        ///     Returns true if you can deal damage to the target.
        /// </summary>
        public static bool IsKillable(this Obj_AI_Base target, float range)
        {
            return !target.HasBuff("kindredrnodeathbuff") && !target.Buffs.Any(b => b.Name.ToLower().Contains("fioraw")) && !target.HasBuff("JudicatorIntervention") && !target.IsZombie
                   && !target.HasBuff("ChronoShift") && !target.HasBuff("UndyingRage") && !target.IsInvulnerable && !target.IsZombie && !target.HasBuff("bansheesveil") && !target.IsDead
                   && !target.IsPhysicalImmune && target.PredictHealth() > 0 && !target.HasBuffOfType(BuffType.Invulnerability) && !target.HasBuffOfType(BuffType.PhysicalImmunity) && target.IsValidTarget(range);
        }

        /// <summary>
        ///     Returns true if you can deal damage to the target.
        /// </summary>
        public static bool IsKillable(this Obj_AI_Base target)
        {
            return !target.HasBuff("kindredrnodeathbuff") && !target.Buffs.Any(b => b.Name.ToLower().Contains("fioraw")) && !target.HasBuff("JudicatorIntervention") && !target.IsZombie
                   && !target.HasBuff("ChronoShift") && !target.HasBuff("UndyingRage") && !target.IsInvulnerable && !target.IsZombie && !target.HasBuff("bansheesveil") && !target.IsDead
                   && !target.IsPhysicalImmune && target.PredictHealth() > 0 && !target.HasBuffOfType(BuffType.Invulnerability) && !target.HasBuffOfType(BuffType.PhysicalImmunity) && target.IsValidTarget();
        }

        public static Vector3 KitePos(this GameObject target, Vector3 KiteTo = default(Vector3))
        {
            Vector3 pos;
            var start = target.IsChampion() ? (target as AIHeroClient).PredictPosition() : target.Position;
            if (KiteTo != default(Vector3))
            {
                pos = start.Extend(KiteTo, KiteDistance(target)).To3D();
            }
            else
            {
                pos = ObjectsManager.SafeAllyToFollow != null ? start.Extend(ObjectsManager.SafeAllyToFollow.PredictPosition(), KiteDistance(target)).To3D() 
                    : start.Extend(ObjectsManager.AllySpawn, KiteDistance(target)).To3D();
            }
            if (pos != Vector3.Zero && ObjectsManager.AllySpawn.Distance(pos) > ObjectsManager.AllySpawn.Distance(Player.Instance) || pos.IsWall() || pos.IsBuilding())
            {
                pos = start.Extend(ObjectsManager.AllySpawn, KiteDistance(target)).To3D();
            }
            return pos;
        }

        public static Vector3 KitePos(this GameObject target, GameObject KiteTo)
        {
            return KitePos(target, KiteTo.Position);
        }

        public static Vector3 KitePos(this Obj_AI_Base target, GameObject KiteTo)
        {
            var pos = target.PredictPosition().Extend(KiteTo, KiteDistance(target)).To3D();
            if (pos != Vector3.Zero && ObjectsManager.AllySpawn.Distance(pos) > ObjectsManager.AllySpawn.Distance(Player.Instance) || pos.IsWall() || pos.IsBuilding())
            {
                pos = target.PredictPosition().Extend(ObjectsManager.AllySpawn, KiteDistance(target)).To3D();
            }
            return pos;
        }

        /// <summary>
        ///     Casts spell with selected hitchance.
        /// </summary>
        public static void Cast(this Spell.Skillshot spell, Obj_AI_Base target, HitChance hitChance)
        {
            if (target != null && spell.IsReady() && target.IsKillable(spell.Range))
            {
                var pred = spell.GetPrediction(target);
                if (pred.HitChance >= hitChance || target.IsCC())
                {
                    spell.Cast(pred.CastPosition);
                }
            }
        }

        /// <summary>
        ///     Casts spell with selected hitchance.
        /// </summary>
        public static void Cast(this Spell.SpellBase spell, Obj_AI_Base target, HitChance hitChance)
        {
            if (target != null && spell.IsReady() && target.IsKillable(spell.Range))
            {
                var pred = spell.GetPrediction(target);
                if (pred.HitChance >= hitChance || target.IsCC())
                {
                    spell.Cast(pred.CastPosition);
                }
            }
        }

        /// <summary>
        ///     Casts spell with selected hitchancepercent.
        /// </summary>
        public static void Cast(this Spell.Skillshot spell, Obj_AI_Base target, float hitchancepercent)
        {
            if (target != null && spell.IsReady() && target.IsKillable(spell.Range))
            {
                var pred = spell.GetPrediction(target);
                if (pred.HitChancePercent >= hitchancepercent || target.IsCC())
                {
                    spell.Cast(pred.CastPosition);
                }
            }
        }

        /// <summary>
        ///     Casts spell with selected hitchancepercent.
        /// </summary>
        public static void Cast(this Spell.SpellBase spell, Obj_AI_Base target, float hitchancepercent)
        {
            if (target != null && spell.IsReady() && target.IsKillable(spell.Range))
            {
                var pred = spell.GetPrediction(target);
                if (pred.HitChancePercent >= hitchancepercent || target.IsCC())
                {
                    spell.Cast(pred.CastPosition);
                }
            }
        }

        /// <summary>
        ///     Returns true if the target is big minion (Siege / Super Minion).
        /// </summary>
        public static bool IsBigMinion(this Obj_AI_Base target)
        {
            return target.BaseSkinName.ToLower().Contains("siege") || target.BaseSkinName.ToLower().Contains("super");
        }

        /// <summary>
        ///     Returns Enemies In Spell Range.
        /// </summary>
        public static IEnumerable<Obj_AI_Base> Enemies(this Spell.SpellBase spell)
        {
            return EntityManager.Enemies.Where(m => m.IsKillable() && m.IsInRange(spell.RangeCheckSource ?? Player.Instance.ServerPosition, spell.Range));
        }

        /// <summary>
        ///     Returns Lane Minions In Spell Range.
        /// </summary>
        public static IEnumerable<Obj_AI_Minion> LaneMinions(this Spell.SpellBase spell)
        {
            return EntityManager.MinionsAndMonsters.EnemyMinions.Where(m => m.IsKillable(spell.Range));
        }

        public static bool CastStartToEnd(this Spell.SpellBase spell, Vector3 start, Vector3 end)
        {
            var skillshot = spell as Spell.Skillshot;
            if (skillshot != null)
            {
                skillshot.CastStartToEnd(start, end);
                return true;
            }
            return false;
        }

        public static PredictionResult GetPrediction(this Spell.SpellBase spell, Obj_AI_Base target)
        {
            var skillshot = spell as Spell.Skillshot;
            return skillshot?.GetPrediction(target);
        }

        public static float ProtectFPS
        {
            get
            {
                return 125; //Game.FPS < 60 && !Game.FPS.Equals(25) ? Game.FPS * 2 : Game.FPS;
            }
        }
        
        public static string AramBuddyFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\EloBuddy\\AramBuddy";
        public static string[] AramBuddySubFolders = { "\\Builds", "\\Chat", "\\temp", "\\Logs", "\\Logs\\AramBuddyLogs", "\\Logs\\ChatLogs" };

        public static AramBuddyDirectories AramBuddyDirectory;
        
        public static void SaveLogs(string str, AramBuddyDirectories Directory)
        {
            return;
            var dir = AramBuddyFolder + "\\Logs";
            switch (Directory)
            {
                case AramBuddyDirectories.AramBuddyLogs:
                    if (!Config.EnableLogs) return;
                    dir += "\\AramBuddyLogs\\";
                    break;
                case AramBuddyDirectories.ChatLogs:
                    if (!Config.SaveChat) return;
                    dir += "\\ChatLogs\\";
                    break;
                default:
                    return;
            }

            var file = "[" + Player.Instance.Name + " " + Player.Instance.ChampionName + "] - [" + DateTime.Now.ToString("yy-MM-dd") + "] " + Game.GameId + ".txt";
            using (var stream = new StreamWriter(dir + file, true))
            {
                stream.WriteLine(str);
                //stream.Close();
            }
        }

        public enum AramBuddyDirectories
        {
            Builds, Chat, Temp, AramBuddyLogs, Logs, ChatLogs
        }

        public static void CreateAramBuddyDirectories()
        {
            if (!Directory.Exists(AramBuddyFolder))
            {
                Directory.CreateDirectory(AramBuddyFolder);
            }
            foreach (var f in AramBuddySubFolders)
            {
                if (!Directory.Exists(AramBuddyFolder + f))
                {
                    Directory.CreateDirectory(AramBuddyFolder + f);
                }
            }
        }

        public static void CreateAramBuddyFile(string FileName, AramBuddyDirectories Directory)
        {
            CreateAramBuddyDirectories();

            var dir = AramBuddyFolder;
            switch (Directory)
            {
                case AramBuddyDirectories.AramBuddyLogs:
                    dir += "\\Logs\\AramBuddyLogs\\";
                    break;
                case AramBuddyDirectories.Logs:
                    dir += "\\Logs\\";
                    break;
                case AramBuddyDirectories.ChatLogs:
                    dir += "\\Logs\\ChatLogs\\";
                    break;
                case AramBuddyDirectories.Builds:
                    dir += "\\Builds\\";
                    break;
                case AramBuddyDirectories.Chat:
                    dir += "\\Chat\\";
                    break;
                case AramBuddyDirectories.Temp:
                    dir += "\\temp\\";
                    break;
            }
            if (!File.Exists(dir + FileName))
            {
                File.Create(dir + FileName);
            }
        }

        /// <summary>
        ///     Distance To Keep from an Object and still be able to attack.
        /// </summary>
        public static float KiteDistance(GameObject target)
        {
            var extra = target.BoundingRadius * 0.5f;

            return (Player.Instance.GetAutoAttackRange() * (Player.Instance.GetAutoAttackRange() < 425 ? 0.5f : 0.8f)) + extra;
        }

        public static List<string> NoManaHeros = new List<string>
        {
            "Akali", "DrMundo", "Garen", "Gnar", "Katarina", "Kennen", "Kled", "LeeSin", "Mordekaiser", "RekSai", "Renekton", "Rengar", "Riven", "Rumble", "Shen", "Shyvana", "Tryndamere", "Vladimir", "Yasuo", "Zac"
        };

        /// <summary>
        ///     Returns true if the target is no mana hero.
        /// </summary>
        public static bool IsNoManaHero(this AIHeroClient target)
        {
            return NoManaHeros.Contains(target.ChampionName.Trim()) || target.Mana <= 0;
        }

        public static bool AlliesMoreThanEnemies(this GameObject target, int range = -1)
        {
            range = range.Equals(-1) ? Config.SafeValue : range;
            return target.CountAllyHeros(range) >= target.CountEnemyHeros(range);
        }

        public static bool AlliesMoreThanEnemies(this Vector3 target, int range = -1)
        {
            range = range.Equals(-1) ? Config.SafeValue : range;
            return target.CountAllyHeros(range) > target.CountEnemyHeros(range);
        }

        public static bool EnemiesMoreThanAllies(this GameObject target, int range = -1)
        {
            range = range.Equals(-1) ? Config.SafeValue : range;
            return target.CountEnemyHeros(range) > target.CountAllyHeros(range);
        }

        /// <summary>
        ///     Returns true if the target is doing something.
        /// </summary>
        public static bool IsActive(this AIHeroClient target)
        {
            return target.IsValid && !target.IsDead && !target.IsAFK();
        }

        public static bool IsChampion(this GameObject obj)
        {
            return obj != null && obj.IsValid && obj is AIHeroClient;
        }

        public static bool IsTurret(this GameObject obj)
        {
            return obj != null && obj.IsValid && obj is Obj_AI_Turret;
        }

        public static bool IsMinion(this GameObject obj)
        {
            return obj != null && obj.IsValid && obj is Obj_AI_Minion;
        }

        public static bool IsNexus(this GameObject obj)
        {
            return obj != null && obj.IsValid && obj is Obj_HQ;
        }

        public static bool IsInhibitor(this GameObject obj)
        {
            return obj != null && obj.IsValid && obj is Obj_BarracksDampener;
        }

        /// <summary>
        ///     Casts AoE spell with selected hitchance.
        /// </summary>
        private static bool CastLineAoE(this Spell.SpellBase spell, Obj_AI_Base target, HitChance hitChance, int hits = 2)
        {
            var skillshot = spell as Spell.Skillshot;
            if (target != null && skillshot != null && skillshot.IsReady() && target.IsKillable(skillshot.Range))
            {
                var pred = spell.GetPrediction(target);
                var rect = new Geometry.Polygon.Rectangle(Player.Instance.ServerPosition, Player.Instance.ServerPosition.Extend(pred.CastPosition, skillshot.Range).To3D(), skillshot.Width);
                if (EntityManager.Heroes.Enemies.Count(e => e != null && e.IsKillable(skillshot.Range) && skillshot.GetPrediction(e).HitChance >= hitChance && rect.IsInside(skillshot.GetPrediction(e).CastPosition)) >= hits)
                {
                    return skillshot.Cast(pred.CastPosition);
                }
            }
            return false;
        }

        public static Spell.Skillshot SetSkillshot(this Spell.SpellBase spell)
        {
            return spell as Spell.Skillshot;
        }

        public static Vector3 CenterVectors(this List<Vector3> vectors)
        {
            return vectors.Aggregate(Vector3.Zero, (current, vector) => current + vector) / vectors.Count;
        }

        /// <summary>
        ///     Creates a checkbox.
        /// </summary>
        public static CheckBox CreateCheckBox(this Menu m, string id, string name, bool defaultvalue = true)
        {
            return m.Add(id, new CheckBox(name, defaultvalue));
        }

        /// <summary>
        ///     Creates a checkbox.
        /// </summary>
        public static CheckBox CreateCheckBox(this Menu m, SpellSlot slot, string name, bool defaultvalue = true)
        {
            return m.Add(slot.ToString(), new CheckBox(name, defaultvalue));
        }

        /// <summary>
        ///     Creates a slider.
        /// </summary>
        public static Slider CreateSlider(this Menu m, string id, string name, int defaultvalue = 0, int MinValue = 0, int MaxValue = 100)
        {
            return m.Add(id, new Slider(name, defaultvalue, MinValue, MaxValue));
        }

        /// <summary>
        ///     Returns CheckBox Value.
        /// </summary>
        public static bool CheckBoxValue(this Menu m, string id)
        {
            return m[id].Cast<CheckBox>().CurrentValue;
        }

        /// <summary>
        ///     Returns CheckBox Value.
        /// </summary>
        public static bool CheckBoxValue(this Menu m, SpellSlot slot)
        {
            return m[slot.ToString()].Cast<CheckBox>().CurrentValue;
        }

        /// <summary>
        ///     Returns Slider Value.
        /// </summary>
        public static int SliderValue(this Menu m, string id)
        {
            return m[id].Cast<Slider>().CurrentValue;
        }

        /// <summary>
        ///     Returns ComboBox Value.
        /// </summary>
        public static int ComboBoxValue(this Menu m, string id)
        {
            return m[id].Cast<ComboBox>().CurrentValue;
        }

        /// <summary>
        ///     Returns true if the value is >= the slider.
        /// </summary>
        public static bool CompareSlider(this Menu m, string id, float value)
        {
            return value >= m[id].Cast<Slider>().CurrentValue;
        }

        /// <summary>
        ///     Returns true if the spell will kill the target.
        /// </summary>
        public static bool WillKill(this Spell.SpellBase spell, Obj_AI_Base target, float MultiplyDmgBy = 1, float ExtraDamage = 0, DamageType ExtraDamageType = DamageType.True)
        {
            return Player.Instance.GetSpellDamage(target, spell.Slot) * MultiplyDmgBy + Player.Instance.CalculateDamageOnUnit(target, ExtraDamageType, ExtraDamage) >= spell.GetHealthPrediction(target) && !target.WillDie(spell);
        }
        
        /// <summary>
        ///     Returns true if the target will die before the spell finish him.
        /// </summary>
        public static bool WillDie(this Obj_AI_Base target, Spell.SpellBase spell)
        {
            return spell.GetHealthPrediction(target) <= 0;
        }

        public static bool WillDie(this Obj_AI_Base target, int Time = 250)
        {
            return target.PredictHealth() <= 0;
        }

        /// <summary>
        ///     Attemtps To Cast the spell AoE.
        /// </summary>
        public static bool CastAOE(this Spell.SpellBase spell, int hitcount, float CustomRange = -1, AIHeroClient target = null)
        {
            var skillshot = spell.SetSkillshot();
            var range = CustomRange.Equals(-1) ? spell.Range : CustomRange;
            if (skillshot.Type == SkillShotType.Circular)
            {
                foreach (var enemy in EntityManager.Heroes.Enemies.Where(e => e.IsKillable(range)))
                {
                    var pred = spell.GetPrediction(enemy);
                    var circle = new Geometry.Polygon.Circle(pred.CastPosition, skillshot.Width);
                    foreach (var point in circle.Points)
                    {
                        circle = new Geometry.Polygon.Circle(point, skillshot.Width);
                        foreach (var p in circle.Points.OrderBy(a => a.Distance(pred.CastPosition)))
                        {
                            if (p.CountEnemyHeros(skillshot.Width) >= hitcount)
                            {
                                if (target == null)
                                {
                                    Player.CastSpell(spell.Slot, p.To3D());
                                    return true;
                                }
                                if (target.ServerPosition.IsInRange(p.To3D(), skillshot.Width))
                                {
                                    Player.CastSpell(spell.Slot, p.To3D());
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            if (skillshot.Type == SkillShotType.Linear)
            {
                return target != null ? spell.CastLineAoE(target, HitChance.Low, hitcount)
                    : EntityManager.Heroes.Enemies.Where(e => e.IsKillable(spell.Range)).Select(e => spell.CastLineAoE(e, HitChance.Low, hitcount)).FirstOrDefault();
            }

            return skillshot.CastIfItWillHit(hitcount);
        }

        /// <summary>
        ///     Class for getting if the figths info.
        /// </summary>
        public class LastAttack
        {
            public Obj_AI_Base Attacker;
            public Obj_AI_Base Target;
            public float LastAttackTick;

            public LastAttack(Obj_AI_Base from, Obj_AI_Base target)
            {
                this.Attacker = from;
                this.Target = target;
                this.LastAttackTick = 0f;
            }
        }

        /// <summary>
        ///     Returns true if the Item IsReady.
        /// </summary>
        public static bool ItemReady(this Item item, Menu menu)
        {
            return item != null && item.IsOwned(Player.Instance) && item.IsReady() && menu.CheckBoxValue(item.Id.ToString());
        }

        /// <summary>
        ///     Returns The predicted position for the target.
        /// </summary>
        public static Vector3 PredictPosition(this Obj_AI_Base target, int Time = 250)
        {
            return Prediction.Position.PredictUnitPosition(target, Time).To3D();
        }

        /// <summary>
        ///     Returns The predicted position for the target.
        /// </summary>
        public static Vector2 PredictPosition2D(this Obj_AI_Base target, int Time = 250)
        {
            return Prediction.Position.PredictUnitPosition(target, Time);
        }

        /// <summary>
        ///     Returns The predicted Health for the target.
        /// </summary>
        public static float PredictHealth(this Obj_AI_Base target, int Time = 250)
        {
            return Prediction.Health.GetPrediction(target, Time);
        }

        /// <summary>
        ///     Returns The predicted HealthPercent for the target.
        /// </summary>
        public static float PredictHealthPercent(this Obj_AI_Base target, int Time = 250)
        {
            return Prediction.Health.GetPrediction(target, Time) / target.MaxHealth * 100;
        }

        /// <summary>
        ///     Returns True if the dive is safe.
        /// </summary>
        public static bool SafeDive(this Vector3 pos)
        {
            return SafeToDive && pos.UnderEnemyTurret() || !pos.UnderEnemyTurret() || Player.Instance.IsZombie();
        }

        /// <summary>
        ///     Returns true if the positon is safe.
        /// </summary>
        public static bool IsSafe(this Vector3 pos)
        {
            //var path = Player.Instance.GetPath(pos);
            return (pos.SafeDive() && Player.Instance.ServerPosition.Extend(pos, 750).To3D().SafeDive()
                && ((Config.EnableEvade && !KappaEvade.dangerPolygons.Any(s => s.IsInside(pos) /*|| path.Any(s.IsInside)*/)
                && !ObjectsManager.EnemyTraps.Select(t => new Geometry.Polygon.Circle(t.Trap.ServerPosition, t.Trap.BoundingRadius * 3))
                .Any(c => c.IsInside(pos) /*|| path.Any(c.IsInside)*/)) || !Config.EnableEvade)) && (TeamTotal(pos) > TeamTotal(pos, true) || pos.AlliesMoreThanEnemies()) || Player.Instance.IsZombie();
        }

        /// <summary>
        ///     Returns true if the target is safe.
        /// </summary>
        public static bool IsSafe(this Obj_AI_Base target)
        {
            return target.PredictPosition().IsSafe();
        }

        public static bool PathUnderEnemyTurret(this Obj_AI_Base target, Vector3 point)
        {
            var start = target.ServerPosition.Extend(point, -300).To3D();
            var end = point.Extend(start, -300).To3D();
            var betterpath = new Geometry.Polygon.Rectangle(start, end, 600);
            var pathinsideenemyturret = EntityManager.Turrets.Enemies.Any(t => t.IsValid && !t.IsDead && !t.IsSafe() && betterpath.IsInside(t));
            return pathinsideenemyturret;
        }

        public static bool PathUnderEnemyTurret(this Obj_AI_Base target, GameObject point)
        {
            return target.PathUnderEnemyTurret(point.Position);
        }

        public static bool SafePath(this Obj_AI_Base target, Vector3 point)
        {
            var start = target.ServerPosition.Extend(point, -200).To3D();
            var end = point.Extend(start, -200).To3D();
            var betterpath = new Geometry.Polygon.Rectangle(start, end, 400);
            var moreallies = EntityManager.Heroes.Allies.Count(a => a.IsValidTarget() && a.TeamTotal() > a.TeamTotal(true) && betterpath.IsInside(a)) > EntityManager.Heroes.Enemies.Count(a => !a.IsAFK() && a.IsValidTarget() && betterpath.IsInside(a));

            return moreallies && !PathUnderEnemyTurret(target, point) && point.IsSafe();
        }

        public static bool SafePath(this Obj_AI_Base target, GameObject point)
        {
            return target.SafePath(point.Position);
        }

        /// <summary>
        ///     Returns Death Timer for the target.
        /// </summary>
        public static float DeathTimer(this AIHeroClient hero)
        {
            var spawntime = 0;

            if (Game.MapId == GameMapId.HowlingAbyss)
            {
                return Math.Max(10, hero.Level * 2 + 4);
            }

            var currentminutes = Game.Time / 60;

            var BRW = hero.Level * 2.5 + 7.5;

            if (currentminutes > 15 && currentminutes < 30)
            {
                spawntime = (int)(BRW + ((BRW / 100) * (currentminutes - 15) * 2 * 0.425));
            }

            if (currentminutes > 30 && currentminutes < 45)
            {
                spawntime = (int)(BRW + ((BRW / 100) * (currentminutes - 15) * 2 * 0.425) + ((BRW / 100) * (currentminutes - 30) * 2 * 0.30));
            }

            if (currentminutes > 45 && currentminutes < 53.5)
            {
                spawntime = (int)(BRW + ((BRW / 100) * (currentminutes - 15) * 2 * 0.425) + ((BRW / 100) * (currentminutes - 30) * 2 * 0.30) * ((BRW / 100) * (currentminutes - 45) * 2 * 1.45));
            }

            if (currentminutes > 53.5)
            {
                spawntime = (int)((BRW + ((BRW / 100) * (currentminutes - 15) * 2 * 0.425) + ((BRW / 100) * (currentminutes - 30) * 2 * 0.30) + ((BRW / 100) * (currentminutes - 45) * 2 * 1.45)) * 1.5f);
            }

            if (spawntime == 0)
            {
                spawntime = (int)BRW;
            }

            return spawntime;
        }

        /// <summary>
        ///     Randomize Vector3.
        /// </summary>
        public static Vector3 Random(this Vector3 pos, float randomValue = 200)
        {
            var rnd = new Random();
            var X = rnd.Next((int)(pos.X - randomValue), (int)(pos.X + randomValue));
            var Y = rnd.Next((int)(pos.Y - randomValue), (int)(pos.Y + randomValue));
            return new Vector3(X, Y, pos.Z);
        }

        /// <summary>
        ///     Randomize Vector3.
        /// </summary>
        public static Vector3 Random(this Vector2 pos2)
        {
            var pos = pos2.To3DWorld();
            var rnd = new Random();
            var X = rnd.Next((int)(pos.X - 200), (int)(pos.X + 200));
            var Y = rnd.Next((int)(pos.Y - 200), (int)(pos.Y + 200));
            return new Vector3(X, Y, pos.Z);
        }

        public static SpellSlot[] KnownSpellSlots = { SpellSlot.Q, SpellSlot.W, SpellSlot.E, SpellSlot.R };

        public static float GetAllSpellsDamage(this AIHeroClient source, Obj_AI_Base target)
        {
            var result = KnownSpellSlots.Where(source.IsReady).Sum(s => source.GetSpellDamage(target, s));
            return result;
        }

        public static SpellDataInst SlotToSpell(this AIHeroClient hero, SpellSlot slot)
        {
            return hero.Spellbook.GetSpell(slot);
        }

        public static float CurrentCD(this SpellDataInst spell)
        {
            var t = spell.CooldownExpires - Game.Time;
            return t;
        }

        public static float CurrentCD(this AIHeroClient hero, SpellSlot slot)
        {
            var spell = SlotToSpell(hero, slot);
            return spell.CurrentCD();
        }

        public static bool IsOnCoolDown(this AIHeroClient hero, SpellSlot slot)
        {
            return CurrentCD(hero, slot) > 0;
        }

        public static bool IsReady(this AIHeroClient hero, SpellSlot slot)
        {
            return !IsOnCoolDown(hero, slot);
        }
    }
}
