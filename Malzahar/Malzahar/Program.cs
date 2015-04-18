﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using SharpDX;

namespace Malzahar
{
    class Program
    {
        public const string ChampionName = "Malzahar";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        //ManaMenager
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;

        public static int FarmId;

        public static GameObject QMissile;
        public static GameObject RMissile;

        //AutoPotion
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);
        public static Items.Item Youmuu = new Items.Item(3142, 0);
        //Menu
        public static Menu Config;

        private static Obj_AI_Hero Player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;
            if (Player.BaseSkinName != ChampionName) return;

            //Create the spells
            Q = new Spell(SpellSlot.Q, 900);
            W = new Spell(SpellSlot.W, 800);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 700);

            Q.SetSkillshot(1f, 100, float.MaxValue, false, SkillshotType.SkillshotCircle);
            W.SetSkillshot(0.5f, 240, 20, false, SkillshotType.SkillshotCircle);


            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            //Create the menu
            Config = new Menu(ChampionName, ChampionName, true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();

            Config.SubMenu("Farm").AddItem(new MenuItem("LCE", "Lane clear E").SetValue(false));
            Config.SubMenu("Farm").AddItem(new MenuItem("farmR", "Lane clear R").SetValue(false));
            Config.SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana").SetValue(new Slider(60, 100, 30)));

            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCQ", "Q").SetValue(false));
            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCW", "W").SetValue(false));

            Config.AddItem(new MenuItem("inter", "OnPossibleToInterrupt Q")).SetValue(true);
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("Haras Q").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));
            Config.SubMenu("R Config").AddItem(new MenuItem("useR", "Semi-manual cast R key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space
            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));
            Config.AddItem(new MenuItem("AACombo", "AA in combo").SetValue(false));
            Config.AddItem(new MenuItem("Hit", "Hit Chance Skillshot").SetValue(new Slider(3, 3, 0)));

            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));
            //Add the events we are going to use:
            Game.OnUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnDelete += Obj_AI_Base_OnDelete;
            Obj_AI_Base.OnCreate += Obj_AI_Base_OnCreate;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnInterruptableSpell;
            Drawing.OnDraw += Drawing_OnDraw;
            Game.PrintChat("<font color=\"#ff00d8\">M</font>alzahar full automatic AI ver 1.0 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }

        private static void OnInterruptableSpell(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.Item("inter").GetValue<bool>() && Q.IsReady() && unit.IsValidTarget(Q.Range))
                Q.Cast(unit);
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var Target = (Obj_AI_Hero)gapcloser.Sender;
            if (Q.IsReady() && Config.Item("AGCQ").GetValue<bool>())
            {
                if (Target.IsValidTarget(Q.Range))
                {
                    Q.Cast(Player.Position);
                    debug("AGC Q");
                }
            }
            else if (W.IsReady() && Config.Item("AGCW").GetValue<bool>())
            {
                if (Target.IsValidTarget(W.Range))
                {
                    W.Cast(Player.Position);
                    debug("AGC W");
                }
            }
        }

        static void Orbwalking_BeforeAttack(LeagueSharp.Common.Orbwalking.BeforeAttackEventArgs args)
        {

            if (FarmId != args.Target.NetworkId)
                FarmId = args.Target.NetworkId;
        }

        private static void Obj_AI_Base_OnCreate(GameObject obj, EventArgs args)
        {

        }

        private static void Obj_AI_Base_OnDelete(GameObject obj, EventArgs args)
        {

        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            PotionMenager();
            if (Combo && !Config.Item("AACombo").GetValue<bool>())
            {
                    Orbwalking.Attack = false;
            }
            else
                Orbwalking.Attack = true;

            if (W.IsReady())
            {
                var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget() && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                {
                    var eDmg = E.GetDamage(t);
                    var wDmg = W.GetDamage(t) * 2;
                    var rDmg = R.GetDamage(t);
                    debug("W " + wDmg);
                    if (rDmg > t.Health)
                        R.Cast(t, true);
                    if (rDmg + wDmg > t.Health)
                    {

                    }
                    else if (R.GetDamage(t) > t.Health)
                        W.Cast(t, true, true);
                    else if (ObjectManager.Player.Mana > RMANA + EMANA && E.GetDamage(t) * 2 + W.GetDamage(t) > t.Health)
                        W.Cast(t, true, true);
                    if (ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA)
                        W.Cast(t, true, true);
                }

                var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, W.Range + 400, MinionTypes.All);
                var Rfarm = W.GetCircularFarmLocation(allMinionsQ, W.Width);

                if ( ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value
                    && Config.Item("farmR").GetValue<bool>() && ObjectManager.Player.Mana > QMANA + EMANA
                    && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear
                    && Rfarm.MinionsHit > 3)
                {
                    W.Cast(Rfarm.Position);
                }
            }

            if (Q.IsReady() )
            {
                ManaMenager();
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var qDmg = Q.GetDamage(t);
                    if (qDmg > t.Health)
                        Q.Cast(t, true);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + QMANA)
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA) && !ObjectManager.Player.UnderTurret(true))
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range) && Config.Item("haras" + enemy.BaseSkinName).GetValue<bool>()))
                        {
                            CastSpell(Q, enemy, Config.Item("Hit").GetValue<Slider>().Value);
                        }
                    }

                    else if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Farm) && ObjectManager.Player.Mana > RMANA + QMANA + EMANA)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                            {
                                Q.Cast(enemy, true);
                            }
                        }
                    }
                }
            }

            if (E.IsReady())
            {
                ManaMenager();
                var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {

                    var eDmg = E.GetDamage(t);
                    debug("" + eDmg);
                    if (eDmg > t.Health)
                        E.Cast(t, true);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + EMANA )
                    {
                        E.Cast(t, true);
                    }

                }
                farmE();
            }
            if (R.IsReady())
            {
                ManaMenager();
                var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
 

                    var eCd = E.Instance.CooldownExpires - Game.Time;

                    var eDmg = E.GetDamage(t);
                    var wDmg = W.GetDamage(t) * 2;
                    var rDmg = R.GetDamage(t);
                    debug("W " + wDmg);
                    if (rDmg > t.Health)
                        R.Cast(t, true);
                    else if (rDmg + wDmg > t.Health && W.IsReady()&& ObjectManager.Player.Mana > RMANA + WMANA)
                    {
                        W.Cast(t, true);
                        R.Cast(t, true);
                    }
                    else if (rDmg + wDmg + eDmg > t.Health && (E.IsReady() || eCd > E.Instance.Cooldown - 3) && W.IsReady() && ObjectManager.Player.Mana > RMANA + WMANA)
                    {
                        W.Cast(t, true);
                        R.Cast(t, true);
                    }
                }
                farmE();
            }
            
        }

        public static void farmE()
        {
            if (Config.Item("LCE").GetValue<bool>() && ObjectManager.Player.Mana > RMANA + EMANA  && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear &&  ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value)
            {

                var mobs = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    E.Cast(mob, true);
                    return;
                }

                var minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth);
                foreach (var minion in minions.Where(minion => minion.Health  < E.GetDamage(minion)))
                {

                        foreach (var minion2 in minions.Where(minion2 => minion.Distance(minion2.Position) < 400 && minion.NetworkId != minion2.NetworkId))
                        {
                            E.Cast(minion);
                        }
                    
                }
            }
        }

        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            //HitChance 0 - 2
            // example CastSpell(Q, ts, 2);
            if (HitChanceNum == 0)
                QWER.Cast(target, true);
            else if (HitChanceNum == 1)
                QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
            else if (HitChanceNum == 2)
            {
                if (QWER.Delay < 0.3)
                    QWER.CastIfHitchanceEquals(target, HitChance.Dashing, true);
                QWER.CastIfHitchanceEquals(target, HitChance.Immobile, true);
                QWER.CastIfWillHit(target, 2, true);
                if (target.Path.Count() < 2)
                    QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
            }
            else if (HitChanceNum == 3)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                //debug("" + target.Path.Count() + " " + (target.Position == target.ServerPosition) + (waypoints.Last<Vector2>().To3D() == target.ServerPosition));
                if (QWER.Delay < 0.3)
                    QWER.CastIfHitchanceEquals(target, HitChance.Dashing, true);
                QWER.CastIfHitchanceEquals(target, HitChance.Immobile, true);
                QWER.CastIfWillHit(target, 2, true);

                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || ObjectManager.Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                else if (target.Path.Count() < 2
                    && (target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) - ObjectManager.Player.Distance(target.Position)) > BackToFront
                    || target.HasBuffOfType(BuffType.Slow) || target.HasBuff("Recall")
                    || (target.Path.Count() == 0 && target.Position == target.ServerPosition)
                    ))
                {
                    if (target.IsFacing(ObjectManager.Player) || target.Path.Count() == 0)
                    {
                        if (ObjectManager.Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed) + (target.BoundingRadius)))
                            QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                    else
                    {
                        QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }
            }
        }

        private static bool Combo
        {
            get { return Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo; }
        }
        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
        }


        public static void ManaMenager()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
            if (!R.IsReady())
                RMANA = QMANA - ObjectManager.Player.Level * 2;
            else
                RMANA = R.Instance.ManaCost;

            if (Farm)
                RMANA = RMANA + ObjectManager.Player.CountEnemiesInRange(2500) * 20;

            if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.2)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
            }
        }

        public static void PotionMenager()
        {
            if (Config.Item("pots").GetValue<bool>() && !ObjectManager.Player.InFountain() && !ObjectManager.Player.HasBuff("Recall"))
            {
                if (Potion.IsReady() && !ObjectManager.Player.HasBuff("RegenerationPotion", true))
                {
                    if (ObjectManager.Player.CountEnemiesInRange(700) > 0 && ObjectManager.Player.Health + 200 < ObjectManager.Player.MaxHealth)
                        Potion.Cast();
                    else if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.6)
                        Potion.Cast();
                }
                if (ManaPotion.IsReady() && !ObjectManager.Player.HasBuff("FlaskOfCrystalWater", true))
                {
                    if (ObjectManager.Player.CountEnemiesInRange(1200) > 0 && ObjectManager.Player.Mana < RMANA + WMANA + EMANA + RMANA)
                        ManaPotion.Cast();
                }
            }
        }

        public static void debug(string msg)
        {
            if (Config.Item("debug").GetValue<bool>())
                Game.PrintChat(msg);
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (RMissile != null)
                Render.Circle.DrawCircle(RMissile.Position, 200, System.Drawing.Color.Cyan);

            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && Q.IsReady())
                    if (Q.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
            }
            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && R.IsReady())
                    if (R.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red);
            }
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && E.IsReady())
                    if (E.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Blue);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Blue);
            }
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && W.IsReady())
                    if (W.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Yellow);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Yellow);
            }
        }
    }
}
