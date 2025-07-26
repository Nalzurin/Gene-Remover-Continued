using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;


namespace GeneRemoverCont
{

    [StaticConstructorOnStartup]
    public class Building_GeneRemover : Building_Enterable, IThingHolderWithDrawnPawn, IThingHolder
    {
        private static GeneRemoverSettings _settings;

        private const float WorkingPowerUsageFactor = 4f;

        private static readonly Texture2D CancelIcon = ContentFinder<Texture2D>.Get("UI/Designators/Cancel");

        [Unsaved(false)]
        private Texture2D cachedInsertPawnTex;

        [Unsaved(false)]
        private CompPowerTrader cachedPowerComp;

        [Unsaved(false)]
        private Effecter progressBar;

        private GeneDef selectedGene;

        [Unsaved(false)]
        private Sustainer sustainerWorking;

        private int ticksRemaining;

        private const int NoPowerEjectCumulativeTicks = 60000;

        private int powerCutTicks;

        private const float ProgressBarOffsetZ = -0.8f;

        private static GeneRemoverSettings Settings => _settings ?? (_settings = LoadedModManager.GetMod<GeneRemoverMod>().GetSettings<GeneRemoverSettings>());

        private Pawn ContainedPawn
        {
            get
            {
                if (innerContainer.Count <= 0)
                {
                    return null;
                }
                return (Pawn)innerContainer[0];
            }
        }

        public bool PowerOn => PowerTraderComp.PowerOn;

        private CompPowerTrader PowerTraderComp => cachedPowerComp ?? (cachedPowerComp = this.TryGetComp<CompPowerTrader>());

        public Texture2D InsertPawnTex
        {
            get
            {
                if (cachedInsertPawnTex == null)
                {
                    cachedInsertPawnTex = ContentFinder<Texture2D>.Get("UI/Gizmos/InsertPawn");
                }
                return cachedInsertPawnTex;
            }
        }   

        public float HeldPawnDrawPos_Y => DrawPos.y + 3f / 74f;

        public float HeldPawnBodyAngle => base.Rotation.AsAngle;

        public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundFaceUp;

        public override Vector3 PawnDrawOffset => Vector3.zero;

        public override void PostPostMake()
        {
            if (!ModLister.CheckBiotech("gene extractor"))
            {
                Destroy();
            }
            else
            {
                base.PostPostMake();
            }
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            sustainerWorking = null;
            if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
            base.DeSpawn(mode);
        }

        protected override void Tick()
        {
            base.Tick();
            innerContainer.DoTick();
            if (this.IsHashIntervalTick(250))
            {
                float num = (base.Working ? 4f : 1f);
                PowerTraderComp.PowerOutput = (0f - base.PowerComp.Props.PowerConsumption) * num;
            }
            if (base.Working)
            {
                if (ContainedPawn == null)
                {
                    Cancel();
                    return;
                }
                if (PowerTraderComp.PowerOn)
                {
                    TickEffects();
                    if (PowerOn)
                    {
                        ticksRemaining--;
                    }
                    if (ticksRemaining <= 0)
                    {
                        Finish();
                    }
                    return;
                }
                powerCutTicks++;
                if (powerCutTicks >= 60000)
                {
                    Pawn containedPawn = ContainedPawn;
                    if (containedPawn != null)
                    {
                        Messages.Message("GeneRemoverNoPowerEjectedMessage".Translate(containedPawn.Named("PAWN")), containedPawn, MessageTypeDefOf.NegativeEvent, historical: false);
                    }
                    Cancel();
                }
            }
            else if (progressBar != null)
            {
                progressBar.Cleanup();
                progressBar = null;
            }
        }

        private void TickEffects()
        {
            if (sustainerWorking == null || sustainerWorking.Ended)
            {
                sustainerWorking = SoundDefOf.GeneExtractor_Working.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            }
            else
            {
                sustainerWorking.Maintain();
            }
            if (progressBar == null)
            {
                progressBar = EffecterDefOf.ProgressBarAlwaysVisible.Spawn();
            }
            progressBar.EffectTick(new TargetInfo(base.Position + IntVec3.North.RotatedBy(base.Rotation), base.Map), TargetInfo.Invalid);
            MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBar.children[0]).mote;
            if (mote != null)
            {
                mote.progress = 1f - Mathf.Clamp01((float)ticksRemaining / (float)Settings.ExtractionTicks);
                mote.offsetZ = -0.8f;
            }
        }

        public override AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            if (!pawn.IsColonist && !pawn.IsSlaveOfColony && !pawn.IsPrisonerOfColony)
            {
                return false;
            }
            if (selectedPawn != null && selectedPawn != pawn)
            {
                return false;
            }
            if (!pawn.RaceProps.Humanlike || pawn.IsQuestLodger())
            {
                return false;
            }
            if (!PowerOn)
            {
                return "NoPower".Translate().CapitalizeFirst();
            }
            if (innerContainer.Count > 0)
            {
                return "Occupied".Translate();
            }
            if (pawn.genes == null || !pawn.genes.GenesListForReading.Any())
            {
                return "PawnHasNoGenes".Translate(pawn.Named("PAWN"));
            }
            return pawn.health.hediffSet.HasHediff(HediffDefOf.XenogermReplicating) ? ((AcceptanceReport)"GeneRemover_CurrentlyRegenerating".Translate()) : ((AcceptanceReport)true);
        }

        private void Cancel()
        {
            startTick = -1;
            powerCutTicks = 0;
            selectedPawn = null;
            selectedGene = null;
            sustainerWorking = null;
            if (ContainedPawn != null)
            {
                innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : base.Position, base.Map, ThingPlaceMode.Near);
            }
        }

        private void Finish()
        {
            powerCutTicks = 0;
            if (selectedGene == null)
            {
                Cancel();
                return;
            }
            startTick = -1;
            selectedPawn = null;
            sustainerWorking = null;
            if (ContainedPawn == null)
            {
                return;
            }
            Pawn containedPawn = ContainedPawn;
            foreach (Gene item in ContainedPawn.genes.Xenogenes.Concat(ContainedPawn.genes.Endogenes))
            {
                if (item.def == selectedGene)
                {
                    ContainedPawn.genes.RemoveGene(item);
                    break;
                }
            }
            int num = Mathf.RoundToInt(60000f * (float)Math.Max(0, _settings.BaseRecoveryDays + _settings.MetabolicMod * (selectedGene.biostatMet * -1)));
            Hediff hediff = HediffMaker.MakeHediff(HediffDefOf.XenogermReplicating, ContainedPawn);
            if (num > 0)
            {
                hediff.TryGetComp<HediffComp_Disappears>().ticksToDisappear = num;
            }
            ContainedPawn.health.AddHediff(hediff);
            innerContainer.TryDropAll(def.hasInteractionCell ? InteractionCell : base.Position, base.Map, ThingPlaceMode.Near);
            Messages.Message("GeneRemover_RemovalComplete".Translate(containedPawn.NameShortColored, selectedGene.label).CapitalizeFirst(), new LookTargets(new TargetInfo[1] { containedPawn }), MessageTypeDefOf.PositiveEvent);
            selectedGene = null;
        }

        public override void TryAcceptPawn(Pawn pawn)
        {
            if ((bool)CanAcceptPawn(pawn))
            {
                selectedPawn = pawn;
                int num = (pawn.DeSpawnOrDeselect() ? 1 : 0);
                if (innerContainer.TryAddOrTransfer(pawn))
                {
                    startTick = Find.TickManager.TicksGame;
                    ticksRemaining = Settings.ExtractionTicks;
                }
                if (num != 0)
                {
                    Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
                }
            }
        }

        protected override void SelectPawn(Pawn pawn)
        {
            Find.WindowStack.Add(new Dialog_SelectGene(pawn, delegate (Pawn p, GeneDef g)
            {
                selectedGene = g;
                base.SelectPawn(p);
            }));
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            Building_GeneRemover buildingGeneRemover = this;
            foreach (FloatMenuOption floatMenuOption in base.GetFloatMenuOptions(selPawn))
            {
                yield return floatMenuOption;
            }
            if (!selPawn.CanReach((LocalTargetInfo)buildingGeneRemover, PathEndMode.InteractionCell, Danger.Deadly))
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(buildingGeneRemover) + ": " + "NoPath".Translate().CapitalizeFirst(), null);
                yield break;
            }
            AcceptanceReport acceptanceReport = buildingGeneRemover.CanAcceptPawn(selPawn);
            if (acceptanceReport.Accepted)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(buildingGeneRemover), delegate
                {
                    SelectPawn(selPawn);
                }), selPawn, buildingGeneRemover);
            }
            else if (buildingGeneRemover.SelectedPawn == selPawn && !selPawn.IsPrisonerOfColony)
            {
                yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("EnterBuilding".Translate(buildingGeneRemover), delegate
                {
                    selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.EnterBuilding, this), JobTag.Misc);
                }), selPawn, buildingGeneRemover);
            }
            else if (!acceptanceReport.Reason.NullOrEmpty())
            {
                yield return new FloatMenuOption("CannotEnterBuilding".Translate(buildingGeneRemover) + ": " + acceptanceReport.Reason.CapitalizeFirst(), null);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            Building_GeneRemover buildingGeneRemover = this;
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (buildingGeneRemover.Working)
            {
                yield return new Command_Action
                {
                    defaultLabel = "CommandCancelExtraction".Translate(),
                    defaultDesc = "CommandCancelExtractionDesc".Translate(),
                    icon = CancelIcon,
                    action = buildingGeneRemover.Cancel,
                    activateSound = SoundDefOf.Designate_Cancel
                };
                if (DebugSettings.ShowDevGizmos)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Finish extraction",
                        action = buildingGeneRemover.Finish
                    };
                }
                yield break;
            }
            if (buildingGeneRemover.selectedPawn != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "CommandCancelLoad".Translate(),
                    defaultDesc = "CommandCancelLoadDesc".Translate(),
                    icon = CancelIcon,
                    activateSound = SoundDefOf.Designate_Cancel,
                    action = buildingGeneRemover.Cancel
                };
                yield break;
            }
            Command_Action commandAction = new Command_Action
            {
                defaultLabel = "InsertPerson".Translate() + "...",
                defaultDesc = "InsertPersonGeneExtractorDesc".Translate(),
                icon = (Texture)(object)buildingGeneRemover.InsertPawnTex,
                action = delegate
                {
                    List<FloatMenuOption> list = new List<FloatMenuOption>();
                    foreach (Pawn item in base.Map.mapPawns.AllPawnsSpawned)
                    {
                        Pawn pawn = item;
                        AcceptanceReport acceptanceReport = CanAcceptPawn(item);
                        if (!acceptanceReport.Accepted)
                        {
                            if (!acceptanceReport.Reason.NullOrEmpty())
                            {
                                list.Add(new FloatMenuOption(item.LabelShortCap + ": " + acceptanceReport.Reason, (Action)null, (Thing)pawn, Color.white, MenuOptionPriority.Default, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, playSelectionSound: true, 0));
                            }
                        }
                        else
                        {
                            list.Add(new FloatMenuOption(item.LabelShortCap + ", " + pawn.genes.XenotypeLabelCap, (Action)delegate
                            {
                                SelectPawn(pawn);
                            }, (Thing)pawn, Color.white, MenuOptionPriority.Default, (Action<Rect>)null, (Thing)null, 0f, (Func<Rect, bool>)null, (WorldObject)null, playSelectionSound: true, 0));
                        }
                    }
                    if (!list.Any())
                    {
                        list.Add(new FloatMenuOption("NoExtractablePawns".Translate(), null));
                    }
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            };
            if (!buildingGeneRemover.PowerOn)
            {
                commandAction.Disable("NoPower".Translate().CapitalizeFirst());
            }
            yield return commandAction;
        }

        public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            if (base.Working && ContainedPawn != null)
            {
                ContainedPawn.Drawer.renderer.DynamicDrawPhaseAt(phase, drawLoc + PawnDrawOffset, null, neverAimWeapon: true);
            }
        }

        public override string GetInspectString()
        {
            string text = base.GetInspectString();
            if (selectedPawn != null && innerContainer.Count == 0)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                text += "WaitingForPawn".Translate(selectedPawn.Named("PAWN")).Resolve();
            }
            else if (base.Working && ContainedPawn != null)
            {
                if (!text.NullOrEmpty())
                {
                    text += "\n";
                }
                string text2 = text + "RemovingGeneFrom".Translate(ContainedPawn.Named("PAWN")).Resolve() + "\n";
                text = (!PowerOn) ? (text2 + "RemovalPausedNoPower".Translate((60000 - powerCutTicks).ToStringTicksToPeriod().Named("TIME")).Colorize(ColorLibrary.RedReadable)) : (text2 + "DurationLeft".Translate(ticksRemaining.ToStringTicksToPeriod()).Resolve());
            }
            return text;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref powerCutTicks, "powerCutTicks", 0);
            Scribe_Defs.Look(ref selectedGene, "selectedGene");
        }
    }

}
