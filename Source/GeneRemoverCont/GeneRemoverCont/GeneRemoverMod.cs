using UnityEngine;
using Verse;

namespace GeneRemoverCont
{
    public class GeneRemoverMod : Mod
    {
        private readonly GeneRemoverSettings _settings;

        public GeneRemoverMod(ModContentPack content)
            : base(content)
        {
            _settings = GetSettings<GeneRemoverSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = inRect;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRemover_BaseRemovalHours".Translate());
            GeneRemoverSettings settings = _settings;
            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            settings.BaseRemovalHours = (int)Widgets.HorizontalSlider(rect, _settings.BaseRemovalHours, 1f, 72f, middleAlignment: true, $"{_settings.BaseRemovalHours} h", "1 h", "72 h", 1f);
            rect = inRect;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRemover_BaseRemovalHoursTooltip".Translate());
            rect = inRect;
            rect.y = inRect.y + 30f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRemover_BaseRecoveryDays".Translate());
            GeneRemoverSettings settings2 = _settings;
            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.y = inRect.y + 30f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            settings2.BaseRecoveryDays = (int)Widgets.HorizontalSlider(rect, _settings.BaseRecoveryDays, 0f, 30f, middleAlignment: true, $"{_settings.BaseRecoveryDays} d", "0 d", "30 d", 1f);
            rect = inRect;
            rect.y = inRect.y + 30f;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRemover_BaseRecoveryDaysTooltip".Translate());
            rect = inRect;
            rect.y = inRect.y + 60f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            Widgets.Label(rect, "GeneRemover_MetabolicModifier".Translate());
            GeneRemoverSettings settings3 = _settings;
            rect = inRect;
            rect.x = inRect.width / 2f;
            rect.y = inRect.y + 60f;
            rect.width = inRect.width / 2f;
            rect.height = 24f;
            settings3.MetabolicMod = (int)Widgets.HorizontalSlider(rect, _settings.MetabolicMod, 0f, 10f, middleAlignment: true, $"{_settings.MetabolicMod} d", "0 d", "10 d", 1f);
            rect = inRect;
            rect.y = inRect.y + 60f;
            rect.height = 24f;
            TooltipHandler.TipRegion(rect, "GeneRemover_MetabolicModifierTooltip".Translate());
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "GeneRemover".Translate();
        }
    }
}