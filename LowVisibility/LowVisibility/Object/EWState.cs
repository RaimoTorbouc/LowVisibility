﻿using BattleTech;
using IRBTModUtils;
using IRBTModUtils.Extension;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using us.frostraptor.modUtils;
using us.frostraptor.modUtils.math;

namespace LowVisibility.Object
{

    // <signature_modifier>_<details_modifier> _<mediumAttackMod>_<longAttackmod> _<extremeAttackMod>
    public class Stealth
    {
        public float SignatureMulti = 0.0f;
        public int DetailsMod = 0;
        public int MediumRangeAttackMod = 0;
        public int LongRangeAttackMod = 0;
        public int ExtremeRangeAttackMod = 0;

        public override string ToString()
        {
            return $"SignatureMulti:{SignatureMulti} details:{DetailsMod} " +
$"rangeMods:{MediumRangeAttackMod} / {LongRangeAttackMod} / {ExtremeRangeAttackMod}";
        }
    }

    // <maxCharges>_<visibilityModPerCharge>_<attackModPerCharge>_<hexesUntilDecay>
    public class Mimetic
    {
        public int MaxCharges = 0;
        public float VisibilityMod = 0f;
        public float AttackMod = 0f;
        public int HexesUntilDecay = 0;

        public override string ToString()
        {
            return $"maxCharges: {MaxCharges} visibilityMod:{VisibilityMod} attackMod:{AttackMod} hexesUntilDecay:{HexesUntilDecay}";
        }
    }

    // <initialAttackModifier>_<attackModifierCap>_<hexesUntilDecay>
    public class ZoomVision
    {
        public int AttackMod = 0;
        public int AttackCap = 0;
        public int HexesUntilDecay = 0;
        public readonly int MaximumRange = 0;

        public ZoomVision(int mod, int cap, int decay)
        {
            this.AttackMod = mod;
            this.AttackCap = cap;
            this.HexesUntilDecay = decay;
            this.MaximumRange = HexUtils.HexesInRange(mod, cap, decay) * 30;
        }

        public override string ToString()
        {
            return $"attackMod:{AttackMod} attackCap:{AttackCap} hexesUntilDecay:{HexesUntilDecay} maximumRange: {MaximumRange}";
        }
    }

    // <initialAttackModifier>_<heatDivisorForStep>_<hexesUntilDecay>
    public class HeatVision
    {
        public int AttackMod = 0;
        public float HeatDivisor = 1f;
        public int MaximumRange = 0;

        public override string ToString()
        {
            return $"attackMod:{AttackMod} heatDivisor:{HeatDivisor} maximumRange: {MaximumRange}";
        }
    }

    // <signatureMod>_<detailsMod>_<attackMod>
    public class NarcEffect
    {
        public int AttackMod = 0;
        public float SignatureMod = 0.0f;
        public int DetailsMod = 0;
    }

    // <signatureMod>_<detailsMod>_<attackMod>
    public class TagEffect
    {
        public int AttackMod = 0;
        public float SignatureMod = 0.0f;
        public int DetailsMod = 0;
    }

    public class EWState
    {

        private readonly AbstractActor actor;

        private int ewCheck = 0; // Raw value before any manipulation

        private int shieldedByECMMod = 0;
        private int jammedByECMMod = 0;

        private int advSensorsCarrierMod = 0;

        private int probeCarrierMod = 0;
        private int pingedByProbeMod = 0;

        private Stealth stealth = null;
        private Mimetic mimetic = null;

        private ZoomVision zoomVision = null;
        private HeatVision heatVision = null;

        private NarcEffect narcEffect = null;
        private TagEffect tagEffect = null;

        private int tacticsMod = 0;

        private bool nightVision = false;
        private bool sharesVision = false;

        // Necessary for serialization
        public EWState() { }

        // Normal Constructor
        public EWState(AbstractActor actor)
        {
            this.actor = actor;
            // Pilot effects; cache and only read once
            tacticsMod = actor.StatCollection.GetValue<int>(ModStats.TacticsMod);
            if (tacticsMod == 0 && actor.GetPilot() != null)
            {
                tacticsMod = SkillUtils.GetTacticsModifier(actor.GetPilot());
                actor.StatCollection.Set<int>(ModStats.TacticsMod, tacticsMod);
            }

            // Ephemeral round check
            ewCheck = actor.StatCollection.GetValue<int>(ModStats.CurrentRoundEWCheck);

            // ECM
            jammedByECMMod = actor.StatCollection.GetValue<int>(ModStats.ECMJamming);

            shieldedByECMMod = actor.StatCollection.GetValue<int>(ModStats.ECMShield);

            // Sensors
            advSensorsCarrierMod = actor.StatCollection.GetValue<int>(ModStats.AdvancedSensors);

            // Probes
            probeCarrierMod = actor.StatCollection.GetValue<int>(ModStats.ProbeCarrier);

            pingedByProbeMod = actor.StatCollection.GetValue<int>(ModStats.PingedByProbe);

            // Stealth - <signature_modifier>_<details_modifier>_<mediumAttackMod>_<longAttackmod>_<extremeAttackMod>
            string rawValue = actor.StatCollection.GetValue<string>(ModStats.StealthEffect);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 5)
                {
                    try
                    {
                        stealth = new Stealth
                        {
                            SignatureMulti = float.Parse(tokens[0]),
                            DetailsMod = Int32.Parse(tokens[1]),
                            MediumRangeAttackMod = Int32.Parse(tokens[2]),
                            LongRangeAttackMod = Int32.Parse(tokens[3]),
                            ExtremeRangeAttackMod = Int32.Parse(tokens[4])
                        };
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize StealthEffect value: ({rawValue}). Discarding!");
                        stealth = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid StealthEffect value: ({rawValue}) found. Discarding!");
                }
            }

            // Mimetic - <maxCharges>_<visibilityModPerCharge>_<attackModPerCharge>_<hexesUntilDecay>
            rawValue = actor.StatCollection.GetValue<string>(ModStats.MimeticEffect);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 4)
                {
                    try
                    {
                        mimetic = new Mimetic
                        {
                            MaxCharges = Int32.Parse(tokens[0]),
                            VisibilityMod = float.Parse(tokens[1]),
                            AttackMod = float.Parse(tokens[2]),
                            HexesUntilDecay = Int32.Parse(tokens[3]),
                        };
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize Mimetic value: ({rawValue}). Discarding!");
                        mimetic = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid Mimetic value: ({rawValue}) found. Discarding!");
                }
            }

            // ZoomVision - <initialAttackModifier>_<attackModifierCap>_<hexesUntilDecay>
            rawValue = actor.StatCollection.GetValue<string>(ModStats.ZoomVision);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 3)
                {
                    try
                    {
                        zoomVision = new ZoomVision(Int32.Parse(tokens[0]), Int32.Parse(tokens[1]), Int32.Parse(tokens[2]));
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize ZoomVision value: ({rawValue}). Discarding!");
                        zoomVision = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid ZoomVision value: ({rawValue}) found. Discarding!");
                }
            }

            // HeatVision - <initialAttackModifier>_<heatDivisorForStep>__<maximumRange>
            rawValue = actor.StatCollection.GetValue<string>(ModStats.HeatVision);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 3)
                {
                    try
                    {
                        heatVision = new HeatVision
                        {
                            AttackMod = Int32.Parse(tokens[0]),
                            HeatDivisor = float.Parse(tokens[1]),
                            MaximumRange = Int32.Parse(tokens[2])
                        };
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize HeatVision value: ({rawValue}). Discarding!");
                        heatVision = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid HeatVision value: ({rawValue}) found. Discarding!");
                }
            }


            // Narc effect - <signatureMod>_<detailsMod>_<attackMod>
            rawValue = actor.StatCollection.GetValue<string>(ModStats.NarcEffect);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 3)
                {
                    try
                    {
                        narcEffect = new NarcEffect
                        {
                            SignatureMod = float.Parse(tokens[0]),
                            DetailsMod = Int32.Parse(tokens[1]),
                            AttackMod = Int32.Parse(tokens[2]),
                        };
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize NarcEffect value: ({rawValue}). Discarding!");
                        stealth = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid NarcEffect value: ({rawValue}) found. Discarding!");
                }
            }


            // Tag effect - <signatureMod>_<detailsMod>_<attackMod>
            rawValue = actor.StatCollection.GetValue<string>(ModStats.TagEffect);
            if (!string.IsNullOrEmpty(rawValue))
            {
                string[] tokens = rawValue.Split('_');
                if (tokens.Length == 3)
                {
                    try
                    {
                        tagEffect = new TagEffect
                        {
                            SignatureMod = float.Parse(tokens[0]),
                            DetailsMod = Int32.Parse(tokens[1]),
                            AttackMod = Int32.Parse(tokens[2]),
                        };
                    }
                    catch (Exception)
                    {
                        Mod.Log.Info?.Write($"Failed to tokenize TagEffect value: ({rawValue}). Discarding!");
                        stealth = null;
                    }
                }
                else
                {
                    Mod.Log.Info?.Write($"WARNING: Invalid TagEffect value: ({rawValue}) found. Discarding!");
                }
            }

            // Vision Sharing
            sharesVision = actor.StatCollection.GetValue<bool>(ModStats.SharesVision);

            // Night Vision
            nightVision = actor.StatCollection.GetValue<bool>(ModStats.NightVision);
        }

        public static Dictionary<AbstractActor, EWState> EWStateCache = new Dictionary<AbstractActor, EWState>();

        public static bool InBatchProcess { get; set; }

        public static void ResetCache()
        {
            EWStateCache.Clear();
            InBatchProcess = false;
        }

        public int GetCurrentEWCheck() { return ewCheck + tacticsMod; }
        public int GetRawCheck() { return ewCheck; }
        public int GetRawTactics() { return tacticsMod; }

        // ECM
        public int ECMJammedAttackMod()
        {

            if (jammedByECMMod <= 0) { return 0; }

            int strength = (int)Math.Floor(jammedByECMMod * Mod.Config.Attack.JammedMulti);

            strength = Math.Max(0, strength);

            return strength;
        }
        public int GetRawECMJammed() 
        { 
            return jammedByECMMod >= 0 ? jammedByECMMod : 0; 
        }

        public float ECMSignatureMod(EWState attackerState)
        {

            if (shieldedByECMMod <= 0) { return 0f; }

            int strength = shieldedByECMMod - attackerState.ProbeCarrierMod();
            if (this.PingedByProbeMod() > 0) { strength -= this.PingedByProbeMod(); }
            if (attackerState.ProbeCarrierMod() > 0) { strength -= attackerState.ProbeCarrierMod(); }

            // Probe can reduce you to zero, but not further.
            strength = Math.Max(0, strength);

            float sigMod = strength * 0.1f;
            if (sigMod != 0) { Mod.Log.Trace?.Write($"Target:({CombatantUtils.Label(actor)}) has ECMSignatureMod:{sigMod}"); }

            return sigMod;
        }
        public int ECMDetailsMod(EWState attackerState)
        {

            if (shieldedByECMMod <= 0) { return 0; }

            int strength = shieldedByECMMod;
            if (this.PingedByProbeMod() > 0) { strength -= this.PingedByProbeMod(); }
            if (attackerState.ProbeCarrierMod() > 0) { strength -= attackerState.ProbeCarrierMod(); }

            strength = Math.Max(0, strength);

            return strength;
        }

        // Shield modifier
        public int ECMAttackMod(EWState attackerState)
        {

            if (shieldedByECMMod <= 0) { return 0; }

            int strength = (int)Math.Floor(shieldedByECMMod * Mod.Config.Attack.ShieldedMulti);

            if (this.PingedByProbeMod() > 0) { strength -= this.PingedByProbeMod(); }
            if (attackerState.ProbeCarrierMod() > 0) { strength -= attackerState.ProbeCarrierMod(); }

            strength = Math.Max(0, strength);

            return strength;
        }
        public int GetRawECMShield() 
        { 
            return shieldedByECMMod >= 0 ? shieldedByECMMod : 0; 
        }


        // Sensors
        public int AdvancedSensorsMod() { return advSensorsCarrierMod; }
        public float GetSensorsRangeMulti() { return ewCheck / 20.0f + tacticsMod / 10.0f; }
        public float GetSensorsBaseRange()
        {
            if (actor.GetType() == typeof(Mech))
            {
                return Mod.Config.Sensors.MechTypeRange * 30.0f;
            }
            else if (actor.GetType() == typeof(Vehicle))
            {
                return Mod.Config.Sensors.VehicleTypeRange * 30.0f;
            }
            else if (actor.GetType() == typeof(Turret))
            {
                return Mod.Config.Sensors.TurretTypeRange * 30.0f;
            }
            else
            {
                return Mod.Config.Sensors.UnknownTypeRange * 30.0f;
            }
        }

        // Probes
        public int ProbeCarrierMod() { return probeCarrierMod; }
        public int PingedByProbeMod() { return pingedByProbeMod; }

        // Stealth
        public float StealthSignatureMod(EWState attackerState)
        {
            float strength = this.stealth != null ? this.stealth.SignatureMulti : 0.0f;

            if (strength > 0)
            {
                // Probe only applies if stealth sig starts out positive
                if (this.PingedByProbeMod() > 0) { strength -= (this.PingedByProbeMod() * 0.05f); }
                if (attackerState.ProbeCarrierMod() > 0) { strength -= (attackerState.ProbeCarrierMod() * 0.05f); }
             
                strength = Math.Max(0, strength);
            }

            // Invert the value to be a signature reduction
            return strength * -1;
        }

        public int StealthDetailsMod() { return HasStealth() ? stealth.DetailsMod : 0; }
        // Defender modifier
        public int StealthAttackMod(EWState attackerState, Weapon weapon, float attackerDistance)
        {
            if (stealth == null) { return 0; }

            int strength = 0;
            if (attackerDistance <= weapon.ShortRange)
            {
                strength = 0;
            }
            else if (attackerDistance <= weapon.MediumRange)
            {
                strength = stealth.MediumRangeAttackMod;
            }
            else if (attackerDistance <= weapon.LongRange)
            {
                strength = stealth.LongRangeAttackMod;
            }
            else if (attackerDistance <= weapon.MaxRange)
            {
                strength = stealth.ExtremeRangeAttackMod;
            }

            if (strength > 0)
            {
                if (this.PingedByProbeMod() > 0) { strength -= this.PingedByProbeMod(); }
                if (attackerState.ProbeCarrierMod() > 0) { strength -= attackerState.ProbeCarrierMod(); }

                strength = Math.Max(0, strength);
            }

            // Positive strength is a negative attack modifier, so invert
            return strength * -1;
        }
        public bool HasStealth() { return stealth != null; }
        public Stealth GetRawStealth() { return stealth; }

        // Mimetic
        public float MimeticVisibilityMod(EWState attackerState)
        {
            // If no mimetic, return
            if (!HasMimetic()) return 1f;

            int charges = CurrentMimeticPips();
            if (this.PingedByProbeMod() > 0)
                charges -= this.PingedByProbeMod();
            if (attackerState.ProbeCarrierMod() != 0)
                charges -= attackerState.ProbeCarrierMod();

            float visibility = charges > 0 ? 1f - (charges * mimetic.VisibilityMod) : 1f;

            return visibility;
        }
        // Defender modifier
        public int MimeticAttackMod(EWState attackerState)
        {
            // If no mimetic, return
            if (!HasMimetic()) return 0;

            int charges = CurrentMimeticPips();
            if (this.PingedByProbeMod() > 0)
                charges -= this.PingedByProbeMod();
            if (attackerState.ProbeCarrierMod() != 0)
                charges -= attackerState.ProbeCarrierMod();

            int strength = charges > 0 ? (int)Math.Ceiling(charges * mimetic.AttackMod) : 0;

            return strength;
        }

        public int CurrentMimeticPips(float distance)
        {
            return MimeticCharges(distance);
        }
        public int CurrentMimeticPips()
        {
            float distance = Vector3.Distance(actor.PreviousPosition, actor.CurrentPosition);
            return MimeticCharges(distance);
        }
        public int MaxMimeticPips() { return mimetic != null ? mimetic.MaxCharges : 0; }
        public bool HasMimetic() { return mimetic != null && mimetic.MaxCharges > 0; }

        private int MimeticCharges(float distance)
        {
            if (!HasMimetic()) return 0;

            int hexesMoved = (int)Math.Ceiling(distance / 30f);
            int numDecays = (int)Math.Floor(hexesMoved / (float)mimetic.HexesUntilDecay);
            Mod.Log.Trace?.Write($"  -- decays = {numDecays} from currentSteps: {hexesMoved} / decayPerStep: {mimetic.HexesUntilDecay}");
            int chargesRemaining = Math.Max(mimetic.MaxCharges - numDecays, 0);
            Mod.Log.Trace?.Write($"  -- current: {chargesRemaining} = initial: {mimetic.AttackMod} - decays: {numDecays}");

            return chargesRemaining;
        }
        public Mimetic GetRawMimetic() { return mimetic; }

        // ZoomVision - Attacker
        public int GetZoomVisionAttackMod(Weapon weapon, float distance)
        {
            if (zoomVision == null || weapon.Type == WeaponType.Melee || weapon.Type == WeaponType.NotSet) { return 0; }

            int hexesBetween = (int)Math.Ceiling(distance / 30f);
            Mod.Log.Trace?.Write($"  hexesBetween: {hexesBetween} = distance: {distance} / 30");

            int numDecays = (int)Math.Floor(hexesBetween / (float)zoomVision.HexesUntilDecay);
            Mod.Log.Trace?.Write($"  -- decays = {numDecays} from currentSteps: {hexesBetween} / decayPerStep: {zoomVision.HexesUntilDecay}");
            int currentMod = HexUtils.DecayingModifier(zoomVision.AttackMod, zoomVision.AttackCap, zoomVision.HexesUntilDecay, distance);
            Mod.Log.Trace?.Write($"  -- current: {currentMod} = initial: {zoomVision.AttackMod} - decays: {numDecays}");

            return currentMod;
        }

        public bool HasZoomVisionToTarget(Weapon weapon, float distance, LineOfFireLevel lofLevel)
        {
            // If we're firing indirectly, zoom doesn't count
            if (weapon.IndirectFireCapable && lofLevel < LineOfFireLevel.LOFObstructed)
            {
                Mod.Log.Debug?.Write("Line of fire is indirect - cannot use zoom!");
                return false;
            }

            if (zoomVision == null || weapon.Type == WeaponType.Melee || weapon.Type == WeaponType.NotSet)
            {
                Mod.Log.Debug?.Write("Zoom vision is null, weaponType is melee or unset - cannot use zoom!");
                return false;
            }

            return distance < zoomVision.MaximumRange;
        }
        public ZoomVision GetRawZoomVision() { return zoomVision; }

        // HeatVision - Attacker
        public int GetHeatVisionAttackMod(AbstractActor target, float distance, Weapon weapon)
        {
            if (this.heatVision == null || weapon.Type == WeaponType.Melee || weapon.Type == WeaponType.NotSet)
	        {
		        return 0;
	        }
	    
            int result = 0;
	        Mech mech = target as Mech;
	        if (mech != null)
	        {
		        if (mech.CurrentHeat == 0)
		        {
			        return 0;
		        }
		
                	int num = (int)Math.Floor(((mech != null) ? ((double)mech.CurrentHeat) : 0.0) / (double)this.heatVision.HeatDivisor); // target heat divided by the mod heat treshold
			int num2 = (int)Math.Floor(((mech != null) ? ((double)distance) : 0.0) / (double)((float)this.heatVision.MaximumRange)); // target distance divided by the mod range bracket
		        result = Math.Min(Math.Max(this.heatVision.AttackMod * num, -5) + num2, 0); // Total bonus (capped between -5 and 0) = heat vision bonus - range decay
                }
                return result;
        }

        public bool HasHeatVisionToTarget(Weapon weapon, float distance)
        {
            if (heatVision == null || weapon.Type == WeaponType.Melee || weapon.Type == WeaponType.NotSet) 
            { 
                return false; 
            }
            return true;
        }
        
        public HeatVision GetRawHeatVision() { return heatVision; }

        // NARC effects
        public bool IsNarced(EWState attackerState)
        {
            return narcEffect != null;
        }
        public int NarcAttackMod(EWState attackerState)
        {
            int val = 0;
            if (narcEffect != null)
            {
                val = Math.Max(0, narcEffect.AttackMod - ECMAttackMod(attackerState));
            }
            return val * -1;
        }
        public int NarcDetailsMod(EWState attackerState)
        {
            int val = 0;
            if (narcEffect != null)
            {
                val = Math.Max(0, narcEffect.DetailsMod - ECMDetailsMod(attackerState));
            }
            return val;
        }
        public float NarcSignatureMod(EWState attackerState)
        {
            float val = 0;
            if (narcEffect != null)
            {
                val = (float)Math.Max(0.0f, narcEffect.SignatureMod - ECMDetailsMod(attackerState) * 0.1f);
            }
            return val;
        }
        public NarcEffect GetRawNarcEffect() { return narcEffect; }

        // TAG effects
        public bool IsTagged(EWState attackerState)
        {
            return tagEffect != null && Math.Max(0, tagEffect.AttackMod - MimeticAttackMod(attackerState)) > 0;
        }
        public int TagAttackMod(EWState attackerState)
        {
            int val = 0;
            if (tagEffect != null)
            {
                val = Math.Max(0, tagEffect.AttackMod - MimeticAttackMod(attackerState));
            }
            return val * -1;
        }
        public int TagDetailsMod(EWState attackerState)
        {
            int val = 0;
            if (tagEffect != null)
            {
                val = Math.Max(0, tagEffect.DetailsMod - MimeticAttackMod(attackerState));
            }
            return val;
        }
        public float TagSignatureMod(EWState attackerState)
        {
            float val = 0;
            if (tagEffect != null)
            {
                val = (float)Math.Max(0.0f, tagEffect.SignatureMod - MimeticVisibilityMod(attackerState));
            }
            return val;
        }
        public TagEffect GetRawTagEffect() { return tagEffect; }

        public bool SharesVision() { return sharesVision; }

        public bool HasNightVision() { return nightVision; }

        // Misc
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"Raw check: {ewCheck}  tacticsMod: {tacticsMod}");
            sb.Append($"  visionRange: {SharedState.Combat.LOS.GetSpotterRange(actor)}  sensorsRange: {SharedState.Combat.LOS.GetSensorRange(actor)}");
            sb.Append($"  ecmShieldMod: {shieldedByECMMod}  ecmJammedMod: {jammedByECMMod}");
            sb.Append($"  advSensors: {advSensorsCarrierMod}  probeCarrier: {probeCarrierMod}");
            sb.Append($"  stealth (detailsMod: {stealth?.DetailsMod} sigMulti: {stealth?.SignatureMulti} attack: {stealth?.MediumRangeAttackMod} / {stealth?.LongRangeAttackMod} / {stealth?.ExtremeRangeAttackMod})");
            sb.Append($"  mimetic: (visibilityMulti: {mimetic?.VisibilityMod}  attackMod: {mimetic?.AttackMod} hexesToDecay: {mimetic?.HexesUntilDecay})");
            sb.Append($"  zoomVision: (attackMod: {zoomVision?.AttackMod} hexesToDecay: {zoomVision?.HexesUntilDecay} attackCap: {zoomVision?.AttackCap})");
            sb.Append($"  heatVision: (attackMod: {heatVision?.AttackMod} heatDivisor: {heatVision?.HeatDivisor})");
            sb.Append($"  nightVision: {nightVision}  sharesVision: {sharesVision}");
            sb.Append($"  pingedByProbe: {pingedByProbeMod}");
            sb.Append($"  narcEffect: (detailsMod: {narcEffect?.DetailsMod} sigMod: {narcEffect?.SignatureMod} attackMod: {narcEffect?.AttackMod})");
            sb.Append($"  tagEffect: (detailsMod: {tagEffect?.DetailsMod} sigMod: {tagEffect?.SignatureMod} attackMod: {tagEffect?.AttackMod})");

            return sb.ToString();
        }

    };

}
