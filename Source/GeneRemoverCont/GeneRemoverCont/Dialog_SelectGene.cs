using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace GeneRemoverCont
{
    public class Dialog_SelectGene : Window
    {
        private const float HeaderHeight = 30f;

        private static readonly List<GeneDef> geneDefs = new List<GeneDef>();

        private static readonly List<Gene> xenogenes = new List<Gene>();

        private static readonly List<Gene> endogenes = new List<Gene>();

        private static float xenogenesHeight;

        private static float endogenesHeight;

        private static float scrollHeight;

        private static int gcx;

        private static int met;

        private static int arc;

        private static readonly Color CapsuleBoxColor = new Color(0.25f, 0.25f, 0.25f);

        private static readonly Color CapsuleBoxColorOverridden = new Color(0.15f, 0.15f, 0.15f);

        private static readonly CachedTexture GeneBackground_Archite = new CachedTexture("UI/Icons/Genes/GeneBackground_ArchiteGene");

        private static readonly CachedTexture GeneBackground_Xenogene = new CachedTexture("UI/Icons/Genes/GeneBackground_Xenogene");

        private static readonly CachedTexture GeneBackground_Endogene = new CachedTexture("UI/Icons/Genes/GeneBackground_Endogene");

        private readonly Action<Pawn, GeneDef> acceptAction;

        private readonly Action cancelAction;

        private Vector2 scrollPosition;

        private readonly Pawn target;

        public GeneDef SelectedGene { get; private set; }

        public override Vector2 InitialSize => new Vector2(736f, 700f);

        public Dialog_SelectGene(Pawn target, Action<Pawn, GeneDef> acceptAction = null, Action cancelAction = null)
        {
            this.target = target;
            this.acceptAction = acceptAction;
            this.cancelAction = cancelAction;
        }

        public override void PostOpen()
        {
            if (!ModLister.CheckBiotech("genes viewing"))
            {
                Close(doCloseSound: false);
            }
            else
            {
                base.PostOpen();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            inRect.yMax -= Window.CloseButSize.y;
            Rect rect = inRect;
            rect.xMin += 34f;
            Text.Font = GameFont.Medium;
            Widgets.Label(rect, "ViewGenes".Translate() + ": " + target.genes.XenotypeLabelCap);
            Text.Font = GameFont.Small;
            GUI.color = XenotypeDef.IconColor;
            GUI.DrawTexture(new Rect(inRect.x, inRect.y, 30f, 30f), target.genes.XenotypeIcon);
            GUI.color = Color.white;
            inRect.yMin += 34f;
            Vector2 size = Vector2.zero;
            DrawGenesInfo(inRect, target, InitialSize.y, ref size, ref scrollPosition);
            if (Widgets.ButtonText(new Rect(inRect.xMax - Window.CloseButSize.x, inRect.yMax, Window.CloseButSize.x, Window.CloseButSize.y), "Cancel".Translate()))
            {
                cancelAction?.Invoke();
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - Window.CloseButSize.x * 2f - 6f, inRect.yMax, Window.CloseButSize.x, Window.CloseButSize.y), "GeneRemover_Select".Translate(), drawBackground: true, doMouseoverSound: true, SelectedGene != null))
            {
                acceptAction?.Invoke(target, SelectedGene);
                Close();
            }
        }

        private void DrawGenesInfo(Rect rect, Thing target, float initialHeight, ref Vector2 size, ref Vector2 scrollPosition, GeneSet pregnancyGenes = null)
        {
            Rect position = rect.ContractedBy(10f);
            GUI.BeginGroup(position);
            float num = BiostatsTable.HeightForBiostats(arc);
            Rect rect2 = new Rect(0f, 0f, position.width, (float)((double)position.height - (double)num - 12.0));
            DrawGeneSections(rect2, target, pregnancyGenes, ref scrollPosition);
            Rect rect3 = new Rect(0f, rect2.yMax + 6f, (float)((double)position.width - 140.0 - 4.0), num);
            rect3.yMax = (float)((double)rect2.yMax + (double)num + 6.0);
            if (!(target is Pawn))
            {
                rect3.width = position.width;
            }
            if (Event.current.type == EventType.Layout)
            {
                float num2 = (float)((double)endogenesHeight + (double)xenogenesHeight + (double)num + 12.0 + 70.0);
                size.y = (((double)num2 <= (double)initialHeight) ? initialHeight : Mathf.Min(num2, (float)((double)(UI.screenHeight - 35) - 165.0 - 30.0)));
                xenogenesHeight = 0f;
                endogenesHeight = 0f;
            }
            GUI.EndGroup();
        }

        private void DrawGeneSections(Rect rect, Thing target, GeneSet genesOverride, ref Vector2 scrollPosition)
        {
            RecacheGenes(target, genesOverride);
            GUI.BeginGroup(rect);
            Rect rect2 = new Rect(0f, 0f, rect.width - 16f, scrollHeight);
            float curY = 0f;
            Widgets.BeginScrollView(rect.AtZero(), ref scrollPosition, rect2);
            Rect rect3 = rect2;
            rect3.y = scrollPosition.y;
            rect3.height = rect.height;
            Rect containingRect = rect3;
            if (target is Pawn)
            {
                if (endogenes.Any())
                {
                    DrawSection(rect, xeno: false, endogenes.Count, ref curY, ref endogenesHeight, delegate (int i, Rect r)
                    {
                        DrawGene(endogenes[i], r, GeneType.Endogene);
                    }, containingRect);
                    curY += 12f;
                }
                DrawSection(rect, xeno: true, xenogenes.Count, ref curY, ref xenogenesHeight, delegate (int i, Rect r)
                {
                    DrawGene(xenogenes[i], r, GeneType.Xenogene);
                }, containingRect);
            }
            else
            {
                GeneType geneType = ((genesOverride == null && !(target is HumanEmbryo)) ? GeneType.Xenogene : GeneType.Endogene);
                DrawSection(rect, geneType == GeneType.Xenogene, geneDefs.Count, ref curY, ref xenogenesHeight, delegate (int i, Rect r)
                {
                    DrawGeneDef(geneDefs[i], r, geneType, null);
                }, containingRect);
            }
            if (Event.current.type == EventType.Layout)
            {
                scrollHeight = curY;
            }
            Widgets.EndScrollView();
            GUI.EndGroup();
        }

        private void RecacheGenes(Thing target, GeneSet genesOverride)
        {
            geneDefs.Clear();
            xenogenes.Clear();
            endogenes.Clear();
            gcx = 0;
            met = 0;
            arc = 0;
            Pawn pawn = target as Pawn;
            GeneSet geneSet = ((target is GeneSetHolderBase geneSetHolderBase) ? geneSetHolderBase.GeneSet : null) ?? genesOverride;
            if (pawn != null)
            {
                foreach (Gene xenogene in pawn.genes.Xenogenes)
                {
                    if (!xenogene.Overridden)
                    {
                        AddBiostats(xenogene.def);
                    }
                    xenogenes.Add(xenogene);
                }
                foreach (Gene endogene in pawn.genes.Endogenes)
                {
                    if (endogene.def.endogeneCategory != EndogeneCategory.Melanin || !pawn.genes.Endogenes.Any<Gene>((Gene x) => x.def.skinColorOverride.HasValue))
                    {
                        if (!endogene.Overridden)
                        {
                            AddBiostats(endogene.def);
                        }
                        endogenes.Add(endogene);
                    }
                }
                xenogenes.SortGenes();
                endogenes.SortGenes();
            }
            else
            {
                if (geneSet == null)
                {
                    return;
                }
                foreach (GeneDef item in geneSet.GenesListForReading)
                {
                    geneDefs.Add(item);
                }
                gcx = geneSet.ComplexityTotal;
                met = geneSet.MetabolismTotal;
                arc = geneSet.ArchitesTotal;
                geneDefs.SortGeneDefs();
            }
            
        }
        static void AddBiostats(GeneDef gene)
        {
            gcx += gene.biostatCpx;
            met += gene.biostatMet;
            arc += gene.biostatArc;
        }
        private void DrawSection(Rect rect, bool xeno, int count, ref float curY, ref float sectionHeight, Action<int, Rect> drawer, Rect containingRect)
        {
            Widgets.Label(10f, ref curY, rect.width, (xeno ? "Xenogenes" : "Endogenes").Translate().CapitalizeFirst(), (xeno ? "XenogenesDesc" : "EndogenesDesc").Translate());
            float num = curY;
            Rect rect2 = new Rect(rect.x, curY, rect.width, sectionHeight);
            if (xeno && count == 0)
            {
                Text.Anchor = TextAnchor.UpperCenter;
                GUI.color = ColoredText.SubtleGrayColor;
                rect2.height = Text.LineHeight;
                Widgets.Label(rect2, "(" + "NoXenogermImplanted".Translate() + ")");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                curY += 90f;
            }
            else
            {
                Widgets.DrawMenuSection(rect2);
                float num2 = (float)(((double)rect.width - 12.0 - 630.0 - 36.0) / 2.0);
                curY += num2;
                int num3 = 0;
                int num4 = 0;
                for (int i = 0; i < count; i++)
                {
                    if (num4 >= 6)
                    {
                        num4 = 0;
                        num3++;
                    }
                    else if (i > 0)
                    {
                        num4++;
                    }
                    Rect rect3 = new Rect((float)((double)num2 + (double)num4 * 90.0 + (double)num4 * 6.0), (float)((double)curY + (double)num3 * 90.0 + (double)num3 * 6.0), 90f, 90f);
                    if (containingRect.Overlaps(rect3))
                    {
                        drawer(i, rect3);
                    }
                }
                curY += (float)((double)(num3 + 1) * 90.0 + (double)num3 * 6.0) + num2;
            }
            if (Event.current.type == EventType.Layout)
            {
                sectionHeight = curY - num;
            }
        }

        private void TryDrawXenotype(Thing target, float x, float y)
        {
            Pawn sourcePawn = target as Pawn;
            if (sourcePawn == null)
            {
                return;
            }
            Rect rect = new Rect(x, y, 140f, Text.LineHeight);
            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(rect, sourcePawn.genes.XenotypeLabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            Rect position = new Rect(rect.center.x - 17f, rect.yMax + 4f, 34f, 34f);
            GUI.color = XenotypeDef.IconColor;
            GUI.DrawTexture(position, sourcePawn.genes.XenotypeIcon);
            GUI.color = Color.white;
            rect.yMax = position.yMax;
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
                TooltipHandler.TipRegion(rect, () => ("Xenotype".Translate() + ": " + sourcePawn.genes.XenotypeLabelCap).Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + sourcePawn.genes.XenotypeDescShort, 883938493);
            }
            if (Widgets.ButtonInvisible(rect) && !sourcePawn.genes.UniqueXenotype)
            {
                Find.WindowStack.Add(new Dialog_InfoCard(sourcePawn.genes.Xenotype));
            }
        }

        public void DrawGene(Gene gene, Rect geneRect, GeneType geneType, bool doBackground = true)
        {
            DrawGeneBasics(gene.def, geneRect, geneType, doBackground, !gene.Active);
            if (Mouse.IsOver(geneRect))
            {
                string text = gene.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + gene.def.DescriptionFull;
                if (gene.Overridden)
                {
                    string text2 = text + "\n\n";
                    text = ((gene.overriddenByGene.def != gene.def) ? (text2 + ("OverriddenByGene".Translate() + ": " + gene.overriddenByGene.LabelCap).Colorize(ColorLibrary.RedReadable)) : (text2 + ("OverriddenByIdenticalGene".Translate() + ": " + gene.overriddenByGene.LabelCap).Colorize(ColorLibrary.RedReadable)));
                }
                TooltipHandler.TipRegion(geneRect, text);
            }
        }

        public void DrawGeneDef(GeneDef gene, Rect geneRect, GeneType geneType, string extraTooltip, bool doBackground = true, bool overridden = false)
        {
            GeneDef _gene = gene;
            string _extraTooltip = extraTooltip;
            DrawGeneBasics(_gene, geneRect, geneType, doBackground, overridden);
            if (!Mouse.IsOver(geneRect))
            {
                return;
            }
            TooltipHandler.TipRegion(geneRect, delegate
            {
                string text = _gene.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n\n" + _gene.DescriptionFull;
                if (!_extraTooltip.NullOrEmpty())
                {
                    text = text + "\n\n" + _extraTooltip.Colorize(ColorLibrary.RedReadable);
                }
                return text;
            }, 316238373);
        }

        private void DrawGeneBasics(GeneDef gene, Rect geneRect, GeneType geneType, bool doBackground, bool overridden)
        {
            GUI.BeginGroup(geneRect);
            Rect rect = geneRect.AtZero();
            if (doBackground)
            {
                Widgets.DrawHighlight(rect);
                GUI.color = new Color(1f, 1f, 1f, 0.05f);
                Widgets.DrawBox(rect);
                GUI.color = Color.white;
            }
            float num = rect.width - Text.LineHeight;
            Rect rect2 = new Rect((float)((double)geneRect.width / 2.0 - (double)num / 2.0), 0f, num, num);
            Color iconColor = gene.IconColor;
            if (overridden)
            {
                iconColor.a = 0.75f;
                GUI.color = ColoredText.SubtleGrayColor;
            }
            CachedTexture cachedTexture = GeneBackground_Archite;
            if (gene.biostatArc == 0)
            {
                switch (geneType)
                {
                    case GeneType.Endogene:
                        cachedTexture = GeneBackground_Endogene;
                        break;
                    case GeneType.Xenogene:
                        cachedTexture = GeneBackground_Xenogene;
                        break;
                }
            }
            GUI.DrawTexture(rect2, cachedTexture.Texture);
            Widgets.DefIcon(rect2, gene, null, 0.9f, null, drawPlaceholder: false, iconColor);
            Text.Font = GameFont.Tiny;
            float num2 = Text.CalcHeight(gene.LabelCap, rect.width);
            Rect rect3 = new Rect(0f, rect.yMax - num2, rect.width, num2);
            GUI.DrawTexture(new Rect(rect3.x, rect3.yMax - num2, rect3.width, num2), TexUI.GrayTextBG);
            Text.Anchor = TextAnchor.LowerCenter;
            if (overridden)
            {
                GUI.color = ColoredText.SubtleGrayColor;
            }
            if (doBackground && (double)num2 < ((double)Text.LineHeight - 2.0) * 2.0)
            {
                rect3.y -= 3f;
            }
            Widgets.Label(rect3, gene.LabelCap);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            if (Widgets.ButtonInvisible(rect))
            {
                SelectedGene = gene;
            }
            if (string.Equals(gene.defName, SelectedGene?.defName))
            {
                Widgets.DrawHighlight(rect);
            }
            GUI.EndGroup();
        }
    }
}
