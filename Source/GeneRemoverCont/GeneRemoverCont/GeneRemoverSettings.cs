using Verse;

namespace GeneRemoverCont
{
    public class GeneRemoverSettings : ModSettings
    {
        public int BaseRemovalHours = 12;
        
        public int BaseRecoveryDays = 6;

        public int MetabolicMod = 2;
        public int ExtractionTicks => BaseRemovalHours * 2500;


        public override void ExposeData()
        {
            Scribe_Values.Look(ref BaseRemovalHours, "BaseRemovalHours", 12);
            Scribe_Values.Look(ref BaseRecoveryDays, "BaseRecoveryDays", 6);
            Scribe_Values.Look(ref MetabolicMod, "MetabolicMod", 2);
            base.ExposeData();
        }
    }
}
